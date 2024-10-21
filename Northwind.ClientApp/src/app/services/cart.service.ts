import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { ICart } from '../models/cart.model';
import { Observable } from 'rxjs';
import { ICartItem } from '../models/cart-item.model';

@Injectable({
  providedIn: 'root'
})
export class CartService
{
  private apiUrl = 'https://localhost:5004/api/carts';
  constructor(private apiService: ApiService) { }

  getCartByUserId(userId: string): Observable<ICart>
  {
    const url = `${this.apiUrl}/${userId}`;
    return this.apiService.get<ICart>(url);
  }

  createCart(body?: any): Observable<ICart>
  {
    return this.apiService.post<ICart>(this.apiUrl, body);
  }

  addItemToCart(userId: string, body?: ICartItem): Observable<ICartItem>
  {
    return this.apiService.post<ICartItem>(this.apiUrl, body);
  }

  updateItemQuantity(userId: string, productId: number, newQuantity: number): Observable<any>
  {
    //const url = `${this.apiUrl}/${userId}/update/${productId}`;
    return this.apiService.put<number>(this.apiUrl, newQuantity);
  }

  removeItemFromCart(userId: string, productId: number): Observable<any>
  {
    return this.apiService.delete<any>(this.apiUrl);
  }

  clearCart(userId: string): Observable<any>
  {
    return this.apiService.delete<any>(this.apiUrl);
  }

  deleteCart(userId: string): Observable<any>
  {
    return this.apiService.delete<any>(this.apiUrl);
  }
}
