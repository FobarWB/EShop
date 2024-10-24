import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { ICart } from '../models/cart.model';
import { Observable, of } from 'rxjs';
import { ICartItem } from '../models/cart-item.model';

@Injectable({
  providedIn: 'root'
})
export class CartService
{
  private apiUrl = 'https://localhost:5004/api/carts';
  private localStorageKey = 'cart';
  private isAuthenticated = false;

  constructor(private apiService: ApiService) { }

  setUserAuthentication(status: boolean): void
  {
    this.isAuthenticated = status;
  }

  // Check if user is logged in or anonymous
  private isUserAuthenticated(): boolean
  {
    return this.isAuthenticated;
  }
  //plus
  getCartByUserId(): Observable<ICart>
  {
    if (this.isUserAuthenticated())
    {
      const userId = this.getUserId();
      const url = `${this.apiUrl}/${userId}`;
      return this.apiService.get<ICart>(url);
    }
    const cart = this.getCartFromLocalStorage();
    return of(cart);
  }
  //plus
  createCart(): Observable<ICart>
  {
    if (this.isUserAuthenticated())
    {
      const userId = this.getUserId();
      return this.apiService.post<ICart>(`${this.apiUrl}/create`, { userId });
    }
    return of(); // No need to create a cart for anonymous users, handled via localStorage
  }

  //plus
  addItemToCart(productId: number, quantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      const userId = this.getUserId();
      const item: Omit<ICartItem, 'cartId'> = { productId, quantity };  // Send only productId and quantity
      return this.apiService.post<void>(`${this.apiUrl}/${userId}/add`, item);
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
      return of();
    }
  }
  updateItemQuantity(productId: number, newQuantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      const userId = this.getUserId();
      return this.apiService.put<void>(`${this.apiUrl}/${userId}/update/${productId}`, { newQuantity });
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
      return of();
    }
  }

  removeItemFromCart(productId: number): Observable<void>
  {
    if (this.isUserAuthenticated()) 
    {
      const userId = this.getUserId();
      return this.apiService.delete<void>(`${this.apiUrl}/${userId}/remove/${productId}`);
    }
    else
    {
      const cart = this.getCartFromLocalStorage();
      cart.cartItems = cart.cartItems.filter(i => i.productId !== productId);
      this.saveCartToLocalStorage(cart);
      return of();
    }
  }

  clearCart(): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      const userId = this.getUserId();
      return this.apiService.delete<void>(`${this.apiUrl}/${userId}/clear`);
    } else
    {
      localStorage.removeItem(this.localStorageKey);
      return of();
    }
  }

  deleteCart(): Observable<any>
  {
    const userId = this.getUserId();
    return this.apiService.delete<void>(`${this.apiUrl}/${userId}`);
  }

  // Helper to get cart from localStorage
  private getCartFromLocalStorage(): ICart
  {
    const cartJson = localStorage.getItem(this.localStorageKey);
    return cartJson ? JSON.parse(cartJson) : { cartId: 0, userId: null, createdAt: new Date(), cartItems: [] };
  }
  // Helper to save cart to localStorage
  private saveCartToLocalStorage(cart: ICart): void
  {
    localStorage.setItem(this.localStorageKey, JSON.stringify(cart));
  }

  // Mock method to get user ID (for authenticated users)
  private getUserId(): string
  {
    // Replace in future with actual method of getting the authenticated user ID
    return '1';
  }
}
