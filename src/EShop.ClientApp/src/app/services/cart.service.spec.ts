import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable, of } from 'rxjs';

import { CartService } from './cart.service';
import { AuthService } from './auth.service';
import { LocalCartService } from './local-cart.service';
import { ICart } from '../models/cart.model';

const apiUrl = '/api/carts/me';

describe('CartService', () =>
{
  let service: CartService;
  let httpTesting: HttpTestingController;
  const auth = { isAuthenticated: signal(true) };
  const localCart = {
    getCart: vi.fn(),
    addItem: vi.fn(),
    updateQuantity: vi.fn(),
    removeItem: vi.fn(),
    clear: vi.fn(),
    readItems: vi.fn(),
    quantityOf: vi.fn(),
    itemCount: vi.fn()
  };

  beforeEach(() =>
  {
    auth.isAuthenticated.set(true);
    vi.clearAllMocks();
    localCart.readItems.mockReturnValue([]);
    localCart.quantityOf.mockReturnValue(0);
    localCart.itemCount.mockReturnValue(0);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: LocalCartService, useValue: localCart }
      ]
    });
    service = TestBed.inject(CartService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() =>
  {
    // Fails the test if it sent a request nobody expected.
    httpTesting.verify();
  });

  describe('authenticated user (API)', () =>
  {
    it('loads the cart of the current user', () =>
    {
      const cart: ICart = { cartId: 5, cartItems: [] };
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
      { name: 'addItemToCart', call: (s: CartService) => s.addItemToCart(7, 2, 10), method: 'POST', url: `${apiUrl}/items`, body: { productId: 7, quantity: 2 } },
      { name: 'updateItemQuantity', call: (s: CartService) => s.updateItemQuantity(7, 3), method: 'PUT', url: `${apiUrl}/items/7`, body: { quantity: 3 } },
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
      expect(service.itemCount()).toBe(0);

      // Every change re-reads the cart, so the counter follows the server.
      httpTesting.expectOne(apiUrl).flush(cartWith(4));
      expect(service.itemCount()).toBe(4);
    });

    it('keeps the counter when the refresh fails', () =>
    {
      service.getCart().subscribe();
      httpTesting.expectOne(apiUrl).flush(cartWith(3));

      service.refreshItemCount();
      httpTesting.expectOne(apiUrl).flush('Server error', { status: 500, statusText: 'Server Error' });

      expect(service.itemCount()).toBe(3);
    });

    it('never touches the local cart', () =>
    {
      service.addItemToCart(7, 2, 10).subscribe();
      httpTesting.expectOne(`${apiUrl}/items`).flush(null);
      httpTesting.expectOne(apiUrl).flush(cartWith(2));

      expect(localCart.addItem).not.toHaveBeenCalled();
    });
  });

  describe('moving the anonymous cart to the server', () =>
  {
    const items = [{ productId: 7, quantity: 2 }];

    beforeEach(() => localCart.readItems.mockReturnValue(items));

    it('merges the local items before it reads the server cart, then clears them', () =>
    {
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      httpTesting.expectNone(apiUrl);
      const merge = httpTesting.expectOne(`${apiUrl}/merge`);
      expect(merge.request.method).toBe('POST');
      expect(merge.request.body).toEqual({ items });
      merge.flush(null, { status: 204, statusText: 'No Content' });
      expect(localCart.clear).toHaveBeenCalledOnce();

      httpTesting.expectOne(apiUrl).flush(cartWith(2));
      expect(result?.cartItems).toHaveLength(1);
    });

    it('sends no merge when there is nothing local', () =>
    {
      localCart.readItems.mockReturnValue([]);

      service.getCart().subscribe();

      httpTesting.expectNone(`${apiUrl}/merge`);
      httpTesting.expectOne(apiUrl).flush(cartWith(0));
    });

    it('merges once when the header and a page read the cart at the same moment', () =>
    {
      const results: ICart[] = [];

      service.getCart().subscribe(c => results.push(c));
      service.getCart().subscribe(c => results.push(c));

      // expectOne fails on a second merge, which would double the quantities.
      httpTesting.expectOne(`${apiUrl}/merge`).flush(null, { status: 204, statusText: 'No Content' });
      httpTesting.match(apiUrl).forEach(req => req.flush(cartWith(2)));
      expect(results).toHaveLength(2);
    });

    it('keeps the local items and shows the server cart when the merge fails, and tries again on the next read', () =>
    {
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);
      httpTesting.expectOne(`${apiUrl}/merge`).flush('Server error', { status: 500, statusText: 'Server Error' });
      httpTesting.expectOne(apiUrl).flush(cartWith(1));

      expect(result?.cartItems).toHaveLength(1);
      expect(localCart.clear).not.toHaveBeenCalled();

      service.getCart().subscribe();

      httpTesting.expectOne(`${apiUrl}/merge`).flush(null, { status: 204, statusText: 'No Content' });
      httpTesting.expectOne(apiUrl).flush(cartWith(3));
      expect(localCart.clear).toHaveBeenCalledOnce();
    });
  });

  // Storage and loading of product details are tested in local-cart.service.spec.ts.
  describe('anonymous user (LocalCartService)', () =>
  {
    beforeEach(() => auth.isAuthenticated.set(false));

    it('returns the local cart', () =>
    {
      const cart: ICart = { cartId: 0, cartItems: [] };
      localCart.getCart.mockReturnValue(of(cart));
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      expect(result).toBe(cart);
    });

    it.each([
      { name: 'addItemToCart', call: (s: CartService) => s.addItemToCart(7, 2, 10), method: 'addItem', args: [7, 2] },
      { name: 'updateItemQuantity', call: (s: CartService) => s.updateItemQuantity(7, 3), method: 'updateQuantity', args: [7, 3] },
      { name: 'removeItemFromCart', call: (s: CartService) => s.removeItemFromCart(7), method: 'removeItem', args: [7] },
      { name: 'clearCart', call: (s: CartService) => s.clearCart(), method: 'clear', args: [] },
    ] as { name: string; call: (s: CartService) => Observable<void>; method: keyof typeof localCart; args: unknown[] }[])(
      '$name calls LocalCartService.$method and emits once', ({ call, method, args }) =>
    {
      // Anonymous changes emit once, like the API calls, so callers can refresh.
      const next = vi.fn();

      call(service).subscribe(next);

      expect(localCart[method]).toHaveBeenCalledWith(...args);
      expect(next).toHaveBeenCalledTimes(1);
    });

    it.each([
      { name: 'addItemToCart', call: (s: CartService) => s.addItemToCart(7, 2, 10) },
      { name: 'updateItemQuantity', call: (s: CartService) => s.updateItemQuantity(7, 3) },
      { name: 'removeItemFromCart', call: (s: CartService) => s.removeItemFromCart(7) },
      { name: 'clearCart', call: (s: CartService) => s.clearCart() },
    ] as { name: string; call: (s: CartService) => Observable<void> }[])(
      '$name updates the counter without a request', ({ call }) =>
    {
      localCart.itemCount.mockReturnValue(6);

      call(service).subscribe();

      expect(service.itemCount()).toBe(6);
    });

    it('refuses more than the stock, like the API', () =>
    {
      localCart.quantityOf.mockReturnValue(9);
      let status: number | undefined;

      service.addItemToCart(7, 2, 10).subscribe({ error: (e: HttpErrorResponse) => status = e.status });

      expect(status).toBe(409);
      expect(localCart.addItem).not.toHaveBeenCalled();
    });

    it('allows adding up to the stock', () =>
    {
      localCart.quantityOf.mockReturnValue(8);

      service.addItemToCart(7, 2, 10).subscribe();

      expect(localCart.addItem).toHaveBeenCalledWith(7, 2);
    });

    it('switches to the server as soon as the user signs in', () =>
    {
      localCart.getCart.mockReturnValue(of({ cartId: 0, cartItems: [] }));
      service.getCart().subscribe();
      httpTesting.expectNone(apiUrl);

      auth.isAuthenticated.set(true);
      service.getCart().subscribe();

      httpTesting.expectOne(apiUrl).flush({ cartId: 5, cartItems: [] });
    });
  });

  // One line with this quantity; an empty cart for 0.
  function cartWith(quantity: number): ICart
  {
    const item = {
      productId: 7, productName: 'Chai', categoryName: 'Beverages', unitPrice: 18, quantity,
      unitsInStock: 39, discontinued: false
    };
    return { cartId: 5, cartItems: quantity > 0 ? [item] : [] };
  }
});
