import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { OrdersService } from '../../services/orders.service';
import { IOrder } from '../../models/order.model';

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
}
