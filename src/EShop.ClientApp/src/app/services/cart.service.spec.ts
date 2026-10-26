import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable } from 'rxjs';

import { CartService } from './cart.service';
import { AuthService } from './auth.service';
import { ICart } from '../models/cart.model';

const apiUrl = '/api/carts/me';

describe('CartService', () =>
{
  let service: CartService;
  let httpTesting: HttpTestingController;
  const auth = { isAuthenticated: signal(true) };

  beforeEach(() =>
  {
    auth.isAuthenticated.set(true);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: auth }]
    });
    service = TestBed.inject(CartService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() =>
  {
    // Fails the test if it sent a request nobody expected.
    httpTesting.verify();
    localStorage.clear();
  });

  describe('authenticated user (API)', () =>
  {
    it('loads the cart of the current user', () =>
    {
      const cart: ICart = { cartId: 5, userId: 'user-1', createdAt: new Date(), cartItems: [] };
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      const req = httpTesting.expectOne(apiUrl);
      expect(req.request.method).toBe('GET');
      req.flush(cart);
      expect(result?.cartId).toBe(5);
    });

    it('returns an empty cart when the user has none yet (404)', () =>
    {
      let result: ICart | undefined;
      const error = vi.fn();

      service.getCart().subscribe({ next: c => result = c, error });

      httpTesting.expectOne(apiUrl).flush('Cart not found', { status: 404, statusText: 'Not Found' });
      expect(result?.cartItems).toEqual([]);
      expect(error).not.toHaveBeenCalled();
    });

    it('sends nothing until somebody subscribes', () =>
    {
      // HttpClient observables are cold: the request starts on subscribe().
      service.removeItemFromCart(7);

      httpTesting.expectNone(`${apiUrl}/items/7`);
    });

    it('passes API errors to the subscriber', () =>
    {
      let status: number | undefined;

      service.getCart().subscribe({ error: (e: HttpErrorResponse) => status = e.status });

      httpTesting.expectOne(apiUrl).flush('Server error', { status: 500, statusText: 'Server Error' });
      expect(status).toBe(500);
    });

    it.each([
      { name: 'addItemToCart', call: (s: CartService) => s.addItemToCart(7, 2), method: 'POST', url: `${apiUrl}/items`, body: { productId: 7, quantity: 2 } },
      { name: 'updateItemQuantity', call: (s: CartService) => s.updateItemQuantity(7, 3), method: 'PUT', url: `${apiUrl}/items/7`, body: 3 },
      { name: 'removeItemFromCart', call: (s: CartService) => s.removeItemFromCart(7), method: 'DELETE', url: `${apiUrl}/items/7`, body: null },
      { name: 'clearCart', call: (s: CartService) => s.clearCart(), method: 'DELETE', url: `${apiUrl}/items`, body: null },
    ] as { name: string; call: (s: CartService) => Observable<unknown>; method: string; url: string; body: unknown }[])(
      '$name sends $method $url', ({ call, method, url, body }) =>
    {
      call(service).subscribe();

      const req = httpTesting.expectOne(url);
      expect(req.request.method).toBe(method);
      expect(req.request.body).toEqual(body);
      req.flush(null);
    });
  });

  describe('anonymous user (localStorage)', () =>
  {
    beforeEach(() => auth.isAuthenticated.set(false));

    it('adds a new item to the stored cart without calling the API', () =>
    {
      service.addItemToCart(7, 2).subscribe();

      expect(storedCart().cartItems).toEqual([{ cartId: 0, productId: 7, quantity: 2 }]);
    });

    it('adds up the quantity of an item that is already in the cart', () =>
    {
      service.addItemToCart(7, 2).subscribe();
      service.addItemToCart(7, 3).subscribe();

      expect(storedCart().cartItems).toEqual([{ cartId: 0, productId: 7, quantity: 5 }]);
    });

    it('returns the stored cart', () =>
    {
      service.addItemToCart(7, 2).subscribe();
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      expect(result?.cartItems).toEqual([{ cartId: 0, productId: 7, quantity: 2 }]);
    });

    it('updates the quantity of an item', () =>
    {
      service.addItemToCart(7, 2).subscribe();

      service.updateItemQuantity(7, 4).subscribe();

      expect(storedCart().cartItems[0].quantity).toBe(4);
    });

    it('removes an item', () =>
    {
      service.addItemToCart(7, 2).subscribe();
      service.addItemToCart(9, 1).subscribe();

      service.removeItemFromCart(7).subscribe();

      expect(storedCart().cartItems.map(i => i.productId)).toEqual([9]);
    });

    it('clears the cart', () =>
    {
      service.addItemToCart(7, 2).subscribe();

      service.clearCart().subscribe();

      expect(localStorage.getItem('cart')).toBeNull();
    });

    // Anonymous changes emit once, like the API calls, so callers can refresh.
    it('emits after a change so callers can refresh', () =>
    {
      const next = vi.fn();

      service.removeItemFromCart(7).subscribe(next);

      expect(next).toHaveBeenCalled();
    });

    it('switches to the server as soon as the user signs in', () =>
    {
      service.getCart().subscribe();
      httpTesting.expectNone(apiUrl);

      auth.isAuthenticated.set(true);
      service.getCart().subscribe();

      httpTesting.expectOne(apiUrl).flush({ cartId: 5, userId: 'user-1', createdAt: new Date(), cartItems: [] });
    });
  });
});

function storedCart(): ICart
{
  return JSON.parse(localStorage.getItem('cart') ?? 'null');
}
