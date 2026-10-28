import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { OrdersService } from '../../services/orders.service';
import { cancellableStatuses, IOrder } from '../../models/order.model';

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
  private readonly destroyRef = inject(DestroyRef);

  protected readonly order = signal<IOrder | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly cancelling = signal(false);
  protected readonly cancelError = signal<string | null>(null);
  protected readonly canCancel = computed(() =>
  {
    const order = this.order();
    return order !== null && cancellableStatuses.includes(order.status);
  });

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
      next: order => this.order.set(order),
      // 404 also for another user's order: the API does not reveal that it exists.
      error: (error: HttpErrorResponse) =>
        this.errorMessage.set(error.status === 404 ? 'Order not found.' : 'Could not load the order.')
    });
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
