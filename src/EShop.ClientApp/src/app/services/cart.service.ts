import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { LocalCartService } from './local-cart.service';
import { ICart } from '../models/cart.model';
import { catchError, Observable, of, throwError } from 'rxjs';

// The one cart for the components: signed-in users keep it on the server,
// anonymous users in localStorage (LocalCartService; moving it on sign-in: #24).
@Injectable({
  providedIn: 'root'
})
export class CartService
{
  // The API takes the user from the access token, so the URLs carry no user id.
  private apiUrl = '/api/carts/me';
  private readonly auth = inject(AuthService);
  private readonly localCart = inject(LocalCartService);

  constructor(private apiService: ApiService) { }

  private isUserAuthenticated(): boolean
  {
    return this.auth.isAuthenticated();
  }

  getCart(): Observable<ICart>
  {
    if (this.isUserAuthenticated())
    {
      // The server creates the cart with the first item; until then 404 means an empty cart.
      return this.apiService.get<ICart>(this.apiUrl).pipe(
        catchError((error: HttpErrorResponse) =>
          error.status === 404 ? of({ cartId: 0, cartItems: [] }) : throwError(() => error))
      );
    }
    return this.localCart.getCart();
  }

  addItemToCart(productId: number, quantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.post<void>(`${this.apiUrl}/items`, { productId, quantity });
    }
    this.localCart.addItem(productId, quantity);
    return of(undefined);
  }

  updateItemQuantity(productId: number, newQuantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.put<void>(`${this.apiUrl}/items/${productId}`, { quantity: newQuantity });
    }
    this.localCart.updateQuantity(productId, newQuantity);
    return of(undefined);
  }

  removeItemFromCart(productId: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.delete<void>(`${this.apiUrl}/items/${productId}`);
    }
    this.localCart.removeItem(productId);
    return of(undefined);
  }

  clearCart(): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.delete<void>(`${this.apiUrl}/items`);
    }
    this.localCart.clear();
    return of(undefined);
  }
}
