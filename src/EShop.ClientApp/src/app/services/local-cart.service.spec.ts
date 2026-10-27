import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { LocalCartService } from './local-cart.service';
import { ICart } from '../models/cart.model';

describe('LocalCartService', () =>
{
  let service: LocalCartService;
  let httpTesting: HttpTestingController;

  beforeEach(() =>
  {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(LocalCartService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() =>
  {
    httpTesting.verify();
    localStorage.clear();
  });

  describe('storage', () =>
  {
    it('stores only the product id and the quantity', () =>
    {
      service.addItem(7, 2);

      expect(stored()).toEqual({ cartItems: [{ productId: 7, quantity: 2 }] });
    });

    it('adds up the quantity of an item that is already in the cart', () =>
    {
      service.addItem(7, 2);
      service.addItem(7, 3);

      expect(stored().cartItems).toEqual([{ productId: 7, quantity: 5 }]);
    });

    it('updates the quantity of an item', () =>
    {
      service.addItem(7, 2);

      service.updateQuantity(7, 4);

      expect(stored().cartItems).toEqual([{ productId: 7, quantity: 4 }]);
    });

    it('removes an item', () =>
    {
      service.addItem(7, 2);
      service.addItem(9, 1);

      service.removeItem(7);

      expect(stored().cartItems.map(i => i.productId)).toEqual([9]);
    });

    it('clears the cart', () =>
    {
      service.addItem(7, 2);

      service.clear();

      expect(localStorage.getItem('cart')).toBeNull();
    });

    it('reads a cart stored in the older format with cart ids', () =>
    {
      localStorage.setItem('cart', JSON.stringify({ cartId: 0, userId: null, cartItems: [{ cartId: 0, productId: 7, quantity: 2 }] }));

      service.addItem(9, 1);

      expect(stored()).toEqual({ cartItems: [{ productId: 7, quantity: 2 }, { productId: 9, quantity: 1 }] });
    });
  });

  describe('getCart', () =>
  {
    it('returns an empty cart without any request', () =>
    {
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      expect(result).toEqual({ cartId: 0, cartItems: [] });
    });

    it('completes the items with product, category and current price', () =>
    {
      service.addItem(7, 2);
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      httpTesting.expectOne('/api/products/7').flush({ productId: 7, productName: 'Chai', unitPrice: 18, categoryId: 1 });
      httpTesting.expectOne('/api/categories/1').flush({ categoryId: 1, categoryName: 'Beverages' });
      expect(result?.cartItems).toEqual([
        { productId: 7, productName: 'Chai', categoryName: 'Beverages', unitPrice: 18, quantity: 2 }
      ]);
    });

    it('leaves the category empty for a product without one', () =>
    {
      service.addItem(7, 1);
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      httpTesting.expectOne('/api/products/7').flush({ productId: 7, productName: 'Chai', unitPrice: 18, categoryId: null });
      expect(result?.cartItems[0].categoryName).toBeNull();
    });

    it('drops a product that no longer exists', () =>
    {
      service.addItem(7, 1);
      service.addItem(9, 1);
      let result: ICart | undefined;

      service.getCart().subscribe(c => result = c);

      httpTesting.expectOne('/api/products/7').flush('Not found', { status: 404, statusText: 'Not Found' });
      httpTesting.expectOne('/api/products/9').flush({ productId: 9, productName: 'Aniseed Syrup', unitPrice: 10, categoryId: null });
      expect(result?.cartItems.map(i => i.productId)).toEqual([9]);
    });
  });
});

function stored(): { cartItems: { productId: number; quantity: number }[] }
{
  return JSON.parse(localStorage.getItem('cart') ?? 'null');
}
