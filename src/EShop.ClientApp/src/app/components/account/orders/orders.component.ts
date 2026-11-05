import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { IOrderSummary } from '../../../models/order.model';
import { IPage } from '../../../models/page.model';
import { OrdersService } from '../../../services/orders.service';

const pageSize = 10;

@Component({
  selector: 'app-account-orders',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './orders.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountOrdersComponent implements OnInit
{
  private readonly ordersService = inject(OrdersService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly orders = signal<IPage<IOrderSummary> | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hasPrevious = computed(() => (this.orders()?.page ?? 1) > 1);
  protected readonly hasNext = computed(() =>
  {
    const orders = this.orders();
    return orders !== null && orders.page * orders.pageSize < orders.totalCount;
  });

  ngOnInit(): void
  {
    this.loadPage(1);
  }

  protected loadPage(page: number): void
  {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.ordersService.getOrders(page, pageSize).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: orders =>
      {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () =>
      {
        this.loading.set(false);
        this.errorMessage.set('Could not load your orders.');
      }
    });
  }
}
