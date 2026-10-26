import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { ICart } from '../models/cart.model';
import { catchError, Observable, of, throwError } from 'rxjs';
import { ICartItem } from '../models/cart-item.model';

@Injectable({
  providedIn: 'root'
})
export class CartService
{
  // The API takes the user from the access token, so the URLs carry no user id.
  private apiUrl = '/api/carts/me';
  private localStorageKey = 'cart';
  private readonly auth = inject(AuthService);

  constructor(private apiService: ApiService) { }

  // Signed-in users keep the cart on the server, anonymous users in localStorage (moving it on sign-in: #24).
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
          error.status === 404 ? of(this.emptyCart()) : throwError(() => error))
      );
    }
    return of(this.getCartFromLocalStorage());
  }

  addItemToCart(productId: number, quantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      const item: Omit<ICartItem, 'cartId'> = { productId, quantity };  // Send only productId and quantity
      return this.apiService.post<void>(`${this.apiUrl}/items`, item);
    }
    else
    {
      const cart = this.getCartFromLocalStorage();
      const existingItem = cart.cartItems.find(item => item.productId === productId);

      if (existingItem)
      {
        existingItem.quantity += quantity;
      } else
      {
        cart.cartItems.push({ cartId: cart.cartId, productId, quantity });
      }

      this.saveCartToLocalStorage(cart);
      return of(undefined);
    }
  }
  updateItemQuantity(productId: number, newQuantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.put<void>(`${this.apiUrl}/items/${productId}`, newQuantity);
    }
    else
    {
      const cart = this.getCartFromLocalStorage();
      const item = cart.cartItems.find(i => i.productId === productId);
      if (item)
      {
        item.quantity = newQuantity;
        this.saveCartToLocalStorage(cart);
      }
      return of(undefined);
    }
  }
  removeItemFromCart(productId: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.delete<void>(`${this.apiUrl}/items/${productId}`);
    }
    else
    {
      const cart = this.getCartFromLocalStorage();
      cart.cartItems = cart.cartItems.filter(i => i.productId !== productId);
      this.saveCartToLocalStorage(cart);
      return of(undefined);
    }
  }
  clearCart(): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.delete<void>(`${this.apiUrl}/items`);
    } else
    {
      localStorage.removeItem(this.localStorageKey);
      return of(undefined);
    }
  }

  // Helper to get cart from localStorage
  private getCartFromLocalStorage(): ICart
  {
    const cartJson = localStorage.getItem(this.localStorageKey);
    return cartJson ? JSON.parse(cartJson) : this.emptyCart();
  }
  // Helper to save cart to localStorage
  private saveCartToLocalStorage(cart: ICart): void
  {
    localStorage.setItem(this.localStorageKey, JSON.stringify(cart));
  }

  private emptyCart(): ICart
  {
    return { cartId: 0, userId: null, createdAt: new Date(), cartItems: [] };
  }
}
