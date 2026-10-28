import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IOrder } from '../models/order.model';
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
