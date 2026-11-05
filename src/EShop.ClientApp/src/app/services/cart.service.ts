import { inject, Injectable, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { LocalCartService } from './local-cart.service';
import { ICart } from '../models/cart.model';
import { catchError, finalize, Observable, of, shareReplay, switchMap, tap, throwError } from 'rxjs';

// The one cart for the components: signed-in users keep it on the server,
// anonymous users in localStorage (LocalCartService); it moves to the server after the sign-in.
@Injectable({
  providedIn: 'root'
})
export class CartService
{
  // The API takes the user from the access token, so the URLs carry no user id.
  private apiUrl = '/api/carts/me';
  private readonly auth = inject(AuthService);
  private readonly localCart = inject(LocalCartService);
  private readonly count = signal(0);
  // Units, not distinct products: a second click on the same product must change the number too.
  readonly itemCount = this.count.asReadonly();
  private pendingMerge: Observable<void> | null = null;

  constructor(private apiService: ApiService) { }

  private isUserAuthenticated(): boolean
  {
    return this.auth.isAuthenticated();
  }

  getCart(): Observable<ICart>
  {
    if (this.isUserAuthenticated())
    {
      // Merging here, not on a sign-in event: a guarded page reads the cart right after the redirect
      // from Keycloak, and it must not see the server cart before the local items are in it.
      return this.mergeLocalCart().pipe(
        switchMap(() => this.apiService.get<ICart>(this.apiUrl)),
        // The server creates the cart with the first item; until then 404 means an empty cart.
        catchError((error: HttpErrorResponse) =>
          error.status === 404 ? of({ cartId: 0, cartItems: [] }) : throwError(() => error)),
        tap(cart => this.setCount(cart))
      );
    }
    return this.localCart.getCart().pipe(tap(cart => this.setCount(cart)));
  }

  // The header calls this at start; every change below calls it too.
  refreshItemCount(): void
  {
    if (this.isUserAuthenticated())
    {
      // A failed refresh keeps the last number: the counter must not break the page.
      this.getCart().subscribe({ error: () => undefined });
      return;
    }
    this.count.set(this.localCart.itemCount());
  }

  addItemToCart(productId: number, quantity: number, unitsInStock: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.post<void>(`${this.apiUrl}/items`, { productId, quantity })
        .pipe(tap(() => this.refreshItemCount()));
    }
    // The same refusal as the API's, so the pages handle both modes alike.
    if (this.localCart.quantityOf(productId) + quantity > unitsInStock)
    {
      return throwError(() => new HttpErrorResponse({ status: 409 }));
    }
    this.localCart.addItem(productId, quantity);
    this.refreshItemCount();
    return of(undefined);
  }

  // For the pages with an add button.
  addItemErrorMessage(error: HttpErrorResponse): string
  {
    return error.status === 409 ? 'No more available' : 'Could not add the item';
  }

  updateItemQuantity(productId: number, newQuantity: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.put<void>(`${this.apiUrl}/items/${productId}`, { quantity: newQuantity })
        .pipe(tap(() => this.refreshItemCount()));
    }
    this.localCart.updateQuantity(productId, newQuantity);
    this.refreshItemCount();
    return of(undefined);
  }

  removeItemFromCart(productId: number): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.delete<void>(`${this.apiUrl}/items/${productId}`)
        .pipe(tap(() => this.refreshItemCount()));
    }
    this.localCart.removeItem(productId);
    this.refreshItemCount();
    return of(undefined);
  }

  clearCart(): Observable<void>
  {
    if (this.isUserAuthenticated())
    {
      return this.apiService.delete<void>(`${this.apiUrl}/items`)
        .pipe(tap(() => this.refreshItemCount()));
    }
    this.localCart.clear();
    this.refreshItemCount();
    return of(undefined);
  }

  // A failed merge keeps the local items: the next read tries again.
  private mergeLocalCart(): Observable<void>
  {
    const items = this.localCart.readItems();
    if (items.length === 0)
    {
      return of(undefined);
    }
    // Shared: the header and a page can read the cart at the same moment, and a second merge would double the items.
    this.pendingMerge ??= this.apiService.post<void>(`${this.apiUrl}/merge`, { items }).pipe(
      tap(() => this.localCart.clear()),
      catchError(() => of(undefined)),
      finalize(() => this.pendingMerge = null),
      shareReplay(1)
    );
    return this.pendingMerge;
  }

  private setCount(cart: ICart): void
  {
    this.count.set(cart.cartItems.reduce((sum, item) => sum + item.quantity, 0));
  }
}
