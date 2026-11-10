import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { catchError, map, of } from 'rxjs';
import { CartService } from '../../services/cart.service';
import { CustomersService } from '../../services/customers.service';
import { OrdersService } from '../../services/orders.service';
import { IAddress } from '../../models/address.model';
import { cartItemProblem, ICartItem } from '../../models/cart-item.model';
import { IOrder, standardFreight } from '../../models/order.model';
import { IOrderPlacedState } from '../../models/order-placed.model';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-checkout',
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe],
  templateUrl: './checkout.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './checkout.component.css'
})
export class CheckoutComponent implements OnInit
{
  private readonly cartService = inject(CartService);
  private readonly ordersService = inject(OrdersService);
  private readonly customersService = inject(CustomersService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  // Not signed in: the order is placed with an email, and an account is created for it (#45).
  protected readonly isGuest = computed(() => !this.auth.isAuthenticated());
  // The last 409 said the email has an account: the alert offers the sign-in instead of the cart.
  protected readonly accountExists = signal(false);

  // One key per checkout attempt: a retry after an error sends it again, and if the first request did place
  // the order (the response was lost), the API returns that order instead of placing a second one.
  private readonly idempotencyKey = crypto.randomUUID();

  // The lengths of the Northwind orders columns, as in OrderApi.
  protected readonly fields = [
    { name: 'shipName', label: 'Name', maxLength: 40 },
    { name: 'shipAddress', label: 'Address', maxLength: 60 },
    { name: 'shipCity', label: 'City', maxLength: 15 },
    { name: 'shipRegion', label: 'Region (optional)', maxLength: 15 },
    { name: 'shipPostalCode', label: 'Postal code', maxLength: 10 },
    { name: 'shipCountry', label: 'Country', maxLength: 15 },
  ] as const;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    // Only for a guest (disabled for a signed-in user, whose email comes from the token); 254 as in OrderApi.
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    shipName: ['', [Validators.required, Validators.maxLength(40)]],
    shipAddress: ['', [Validators.required, Validators.maxLength(60)]],
    shipCity: ['', [Validators.required, Validators.maxLength(15)]],
    shipRegion: ['', [Validators.maxLength(15)]],
    shipPostalCode: ['', [Validators.required, Validators.maxLength(10)]],
    shipCountry: ['', [Validators.required, Validators.maxLength(15)]],
  });
  private readonly formValid = toSignal(
    this.form.statusChanges.pipe(map(status => status === 'VALID')),
    { initialValue: this.form.valid }
  );

  protected readonly freight = standardFreight;
  protected readonly items = signal<ICartItem[]>([]);
  protected readonly loaded = signal(false);
  protected readonly placing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  // Items named by the API's last 409: marked even if the reloaded cart no longer shows the problem.
  private readonly rejectedProductIds = signal<number[]>([]);

  protected readonly subtotal = computed(() =>
    this.items().reduce((sum, item) => sum + (item.unitPrice ?? 0) * item.quantity, 0));
  protected readonly total = computed(() => this.subtotal() + this.freight);
  protected readonly hasProblems = computed(() => this.items().some(item => this.problemOf(item) !== null));
  protected readonly canPlaceOrder = computed(() =>
    this.items().length > 0 && !this.hasProblems() && this.formValid() && !this.placing());

  ngOnInit(): void
  {
    this.loadCart();
    if (this.isGuest())
    {
      // A guest has no saved addresses, and the request would only get 401.
      return;
    }
    // A disabled control does not count for the form's validity.
    this.form.controls.email.disable();
    this.prefillAddress();
  }

  protected problemOf(item: ICartItem): string | null
  {
    return cartItemProblem(item)
      ?? (this.rejectedProductIds().includes(item.productId) ? 'Not available in this quantity' : null);
  }

  protected lineTotal(item: ICartItem): number
  {
    return (item.unitPrice ?? 0) * item.quantity;
  }

  protected placeOrder(): void
  {
    // The button is disabled too; this also stops a second submit (Enter) while the first is on its way.
    if (!this.canPlaceOrder())
    {
      return;
    }
    this.placing.set(true);
    this.errorMessage.set(null);
    this.accountExists.set(false);
    const { email, ...value } = this.form.getRawValue();
    const address = { ...value, shipRegion: value.shipRegion || null };
    const guest = this.isGuest();
    const request = guest
      ? this.ordersService.placeGuestOrder(
        { email, address, items: this.items().map(({ productId, quantity }) => ({ productId, quantity })) },
        this.idempotencyKey)
      : this.ordersService.placeOrder(address, this.idempotencyKey);
    request.subscribe({
      next: order => guest ? this.showGuestOrder(order, email) : this.showOrder(order),
      error: (error: HttpErrorResponse) =>
      {
        this.placing.set(false);
        this.showError(error);
      }
    });
  }

  // The API has emptied the server cart; the confirmation page loads the order by its id.
  private showOrder(order: IOrder): void
  {
    this.cartService.refreshItemCount();
    this.router.navigate(['/orders', order.orderId], { queryParams: { placed: true } });
  }

  // The browser's cart is the guest's: emptied here (this also updates the counter). The order goes along in the
  // navigation state, as a guest cannot load it.
  private showGuestOrder(order: IOrder, email: string): void
  {
    this.cartService.clearCart().subscribe();
    const state: IOrderPlacedState = { order, email };
    this.router.navigate(['/order-placed'], { state });
  }

  private loadCart(): void
  {
    this.cartService.getCart().subscribe(cart =>
    {
      this.items.set(cart.cartItems);
      this.loaded.set(true);
    });
  }

  // The first address is the default one. Saved addresses are a convenience: without them the visitor types the
  // address, so a failed request is not an error, and what the visitor already typed is not overwritten.
  private prefillAddress(): void
  {
    this.customersService.getAddresses().pipe(
      catchError(() => of<IAddress[]>([]))
    ).subscribe(([address]) =>
    {
      if (address && this.form.pristine)
      {
        this.form.patchValue({
          shipName: address.recipientName,
          shipAddress: address.address,
          shipCity: address.city,
          shipRegion: address.region ?? '',
          shipPostalCode: address.postalCode,
          shipCountry: address.country,
        });
      }
    });
  }

  private showError(error: HttpErrorResponse): void
  {
    if (error.status === 409 && error.error?.code === 'account-exists')
    {
      // The cart stays: after the sign-in it moves to the server cart (#24), and the order is placed from there.
      this.accountExists.set(true);
      this.errorMessage.set('An account with this email already exists. Please sign in, '
        + 'or use "Forgot password" on the sign-in page if you have not set a password yet.');
    }
    else if (error.status === 409)
    {
      // Another buyer was faster, or the product changed: show the current stock and what to fix.
      this.rejectedProductIds.set(error.error?.productIds ?? []);
      this.errorMessage.set('Unfortunately, some products are no longer available in the requested quantity.');
      this.loadCart();
    }
    else if (error.status === 400)
    {
      this.errorMessage.set('Your cart is empty.');
      this.loadCart();
    }
    else if (error.status === 429)
    {
      this.errorMessage.set('Too many attempts. Please wait a minute and try again.');
    }
    else if (error.status === 503)
    {
      this.errorMessage.set('Ordering without an account is temporarily unavailable. Please try again later.');
    }
    else
    {
      this.errorMessage.set('Could not place the order. Please try again.');
    }
  }
}
