import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable } from 'rxjs';

import { CartService } from './cart.service';
import { ICart } from '../models/cart.model';

const apiUrl = 'https://localhost:5004/api/carts';

describe('CartService', () =>
{
  let service: CartService;
  let httpTesting: HttpTestingController;

  beforeEach(() =>
  {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
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
      const cart: ICart = { cartId: 5, userId: '1', createdAt: new Date(), cartItems: [] };
      let result: ICart | undefined;

      service.getCartByUserId().subscribe(c => result = c);

      const req = httpTesting.expectOne(`${apiUrl}/1`);
      expect(req.request.method).toBe('GET');
      req.flush(cart);
      expect(result?.cartId).toBe(5);
    });

    it('sends nothing until somebody subscribes', () =>
    {
      // HttpClient observables are cold: the request starts on subscribe().
      service.removeItemFromCart(7);

      httpTesting.expectNone(`${apiUrl}/1/remove/7`);
    });

    it('passes API errors to the subscriber', () =>
    {
      let status: number | undefined;

      service.getCartByUserId().subscribe({ error: (e: HttpErrorResponse) => status = e.status });

      httpTesting.expectOne(`${apiUrl}/1`).flush('Cart not found', { status: 404, statusText: 'Not Found' });
      expect(status).toBe(404);
    });

    it.each([
      { name: 'createCart', call: (s: CartService) => s.createCart(), method: 'POST', url: `${apiUrl}/create`, body: { userId: '1' } },
      { name: 'addItemToCart', call: (s: CartService) => s.addItemToCart(7, 2), method: 'POST', url: `${apiUrl}/1/add`, body: { productId: 7, quantity: 2 } },
      { name: 'updateItemQuantity', call: (s: CartService) => s.updateItemQuantity(7, 3), method: 'PUT', url: `${apiUrl}/1/update/7`, body: 3 },
      { name: 'removeItemFromCart', call: (s: CartService) => s.removeItemFromCart(7), method: 'DELETE', url: `${apiUrl}/1/remove/7`, body: null },
      { name: 'clearCart', call: (s: CartService) => s.clearCart(), method: 'DELETE', url: `${apiUrl}/1/clear`, body: null },
      { name: 'deleteCart', call: (s: CartService) => s.deleteCart(), method: 'DELETE', url: `${apiUrl}/1`, body: null },
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
    beforeEach(() => service.setUserAuthentication(false));

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

      service.getCartByUserId().subscribe(c => result = c);

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
  });
});

function storedCart(): ICart
{
  return JSON.parse(localStorage.getItem('cart') ?? 'null');
}
