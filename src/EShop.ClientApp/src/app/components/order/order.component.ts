import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { exhaustMap, map, Subscription, switchMap, take, takeWhile, timer } from 'rxjs';
import { OrdersService } from '../../services/orders.service';
import { CartService } from '../../services/cart.service';
import { cancellableStatuses, IOrder } from '../../models/order.model';

// ProductApi reserves the stock a few seconds after the checkout (#76): the page asks again until it is decided.
export const statusPollInterval = 2000;
export const statusPollLimit = 15;
// ShoppingCart empties the cart on order.confirmed, which leaves OrderApi's outbox up to 2 s after the status
// changed: the counter is read again a little later than that.
export const cartRefreshDelay = 3000;

// Loaded by id, not handed over from the checkout: the page also works after a reload or from a link.
@Component({
  selector: 'app-order',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './order.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrderComponent implements OnInit
{
  private readonly route = inject(ActivatedRoute);
  private readonly ordersService = inject(OrdersService);
  private readonly cartService = inject(CartService);
  private readonly destroyRef = inject(DestroyRef);
  private polling?: Subscription;

  protected readonly order = signal<IOrder | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  // The polling gave up or failed: the status shown may be out of date.
  protected readonly statusMessage = signal<string | null>(null);
  protected readonly cancelling = signal(false);
  protected readonly cancelError = signal<string | null>(null);
  // Set by the checkout; the order list opens the same page without it.
  protected readonly justPlaced = this.route.snapshot.queryParamMap.get('placed') === 'true';
  protected readonly canCancel = computed(() =>
  {
    const order = this.order();
    return order !== null && cancellableStatuses.includes(order.status);
  });
  protected readonly thanks = computed(() => this.justPlaced && this.order()?.status !== 'Rejected');

  ngOnInit(): void
  {
    this.loadOrder();
  }

  private loadOrder(): void
  {
    this.route.paramMap.pipe(
      map(params => Number(params.get('id'))),
      switchMap(id => this.ordersService.getOrder(id)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: order =>
      {
        this.order.set(order);
        if (order.status === 'Confirmed' && this.justPlaced)
        {
          // Decided before the page loaded: the cart is being emptied now.
          this.refreshCartCountLater();
        }
        this.followStatus(order);
      },
      // 404 also for another user's order: the API does not reveal that it exists.
      error: (error: HttpErrorResponse) =>
        this.errorMessage.set(error.status === 404 ? 'Order not found.' : 'Could not load the order.')
    });
  }

  private followStatus(order: IOrder): void
  {
    if (order.status !== 'Pending')
    {
      return;
    }
    this.polling = timer(statusPollInterval, statusPollInterval).pipe(
      take(statusPollLimit),
      // A slow answer is not cancelled by the next tick: that tick is skipped.
      exhaustMap(() => this.ordersService.getOrder(order.orderId)),
      // true: the first decided order passes too, then the polling stops.
      takeWhile(current => current.status === 'Pending', true),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: current =>
      {
        this.order.set(current);
        if (current.status === 'Confirmed')
        {
          this.refreshCartCountLater();
        }
      },
      error: () => this.statusMessage.set('Could not check the status. Reload the page to try again.'),
      complete: () =>
      {
        if (this.order()?.status === 'Pending')
        {
          this.statusMessage.set('Checking the stock takes longer than usual. Reload the page in a minute.');
        }
      }
    });
  }

  private refreshCartCountLater(): void
  {
    timer(cartRefreshDelay).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.cartService.refreshItemCount());
  }

  protected cancelOrder(): void
  {
    const order = this.order();
    // Cancelling cannot be undone: ask first.
    if (order === null || !confirm(`Cancel order #${order.orderId}?`))
    {
      return;
    }

    this.cancelling.set(true);
    this.cancelError.set(null);
    // An answer still on its way would show the order as Pending again.
    this.polling?.unsubscribe();
    this.ordersService.cancelOrder(order.orderId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: cancelled =>
      {
        this.order.set(cancelled);
        this.cancelling.set(false);
      },
      // 409: shipped meanwhile, or cancelled in another tab.
      error: (error: HttpErrorResponse) =>
      {
        this.cancelError.set(error.status === 409
          ? 'The order can no longer be cancelled.'
          : 'Could not cancel the order.');
        this.cancelling.set(false);
      }
    });
  }
}
