import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { map } from 'rxjs';
import { CartService } from '../../services/cart.service';
import { OrdersService } from '../../services/orders.service';
import { cartItemProblem, ICartItem } from '../../models/cart-item.model';
import { standardFreight } from '../../models/order.model';

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
  private readonly router = inject(Router);

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
    const value = this.form.getRawValue();
    this.ordersService.placeOrder({ ...value, shipRegion: value.shipRegion || null }, this.idempotencyKey).subscribe({
      // The API has emptied the cart; the confirmation page loads the order by its id.
      next: order => this.router.navigate(['/orders', order.orderId]),
      error: (error: HttpErrorResponse) =>
      {
        this.placing.set(false);
        this.showError(error);
      }
    });
  }

  private loadCart(): void
  {
    this.cartService.getCart().subscribe(cart =>
    {
      this.items.set(cart.cartItems);
      this.loaded.set(true);
    });
  }

  private showError(error: HttpErrorResponse): void
  {
    if (error.status === 409)
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
    else
    {
      this.errorMessage.set('Could not place the order. Please try again.');
    }
  }
}
