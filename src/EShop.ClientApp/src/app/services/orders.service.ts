import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IOrder, IOrderSummary } from '../models/order.model';
import { IPage } from '../models/page.model';
import { IShippingAddress } from '../models/shipping-address.model';

@Injectable({
  providedIn: 'root'
})
export class OrdersService
{
  // HttpClient directly: ApiService cannot send headers.
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/orders';

  // The items come from the user's server cart; the key makes a retried request return the same order.
  placeOrder(address: IShippingAddress, idempotencyKey: string): Observable<IOrder>
  {
    return this.http.post<IOrder>(this.apiUrl, address, { headers: { 'Idempotency-Key': idempotencyKey } });
  }

  // The user's orders, newest first.
  getOrders(page: number, pageSize: number): Observable<IPage<IOrderSummary>>
  {
    return this.http.get<IPage<IOrderSummary>>(this.apiUrl, { params: { page, pageSize } });
  }

  getOrder(orderId: number): Observable<IOrder>
  {
    return this.http.get<IOrder>(`${this.apiUrl}/${orderId}`);
  }

  // A command without a body; the answer is the cancelled order.
  cancelOrder(orderId: number): Observable<IOrder>
  {
    return this.http.post<IOrder>(`${this.apiUrl}/${orderId}/cancel`, null);
  }
}
