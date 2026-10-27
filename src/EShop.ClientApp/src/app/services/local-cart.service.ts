import { inject, Injectable } from '@angular/core';
import { catchError, forkJoin, map, Observable, of, switchMap } from 'rxjs';
import { ICart } from '../models/cart.model';
import { ICartItem } from '../models/cart-item.model';
import { ProductService } from './products.service';
import { CategoryService } from './categories.service';

type StoredItem = Pick<ICartItem, 'productId' | 'quantity'>;

// The cart of an anonymous user: only product ids and quantities are stored, so prices never go stale;
// names, categories and current prices are loaded when the cart is read (moving it to the server: #24).
@Injectable({
  providedIn: 'root'
})
export class LocalCartService
{
  private readonly productService = inject(ProductService);
  private readonly categoryService = inject(CategoryService);
  private readonly storageKey = 'cart';

  getCart(): Observable<ICart>
  {
    const items = this.read();
    if (items.length === 0)
    {
      // forkJoin([]) would complete without emitting.
      return of({ cartId: 0, cartItems: [] });
    }
    return forkJoin(items.map(item => this.complete(item))).pipe(
      map(completed => ({ cartId: 0, cartItems: completed.filter(item => item !== null) }))
    );
  }

  addItem(productId: number, quantity: number): void
  {
    const items = this.read();
    const existingItem = items.find(item => item.productId === productId);
    if (existingItem)
    {
      existingItem.quantity += quantity;
    }
    else
    {
      items.push({ productId, quantity });
    }
    this.save(items);
  }

  updateQuantity(productId: number, quantity: number): void
  {
    const items = this.read();
    const item = items.find(i => i.productId === productId);
    if (item)
    {
      item.quantity = quantity;
      this.save(items);
    }
  }

  removeItem(productId: number): void
  {
    this.save(this.read().filter(item => item.productId !== productId));
  }

  clear(): void
  {
    localStorage.removeItem(this.storageKey);
  }

  // A product deleted since it was added drops out of the cart instead of failing the whole page.
  private complete(item: StoredItem): Observable<ICartItem | null>
  {
    return this.productService.getProduct(item.productId).pipe(
      switchMap(product =>
      {
        const categoryName$: Observable<string | null> = product.categoryId
          ? this.categoryService.getCategory(product.categoryId).pipe(map(category => category.categoryName))
          : of(null);
        return categoryName$.pipe(map(categoryName => ({
          productId: item.productId,
          productName: product.productName,
          categoryName,
          unitPrice: product.unitPrice,
          quantity: item.quantity,
          unitsInStock: product.unitsInStock,
          discontinued: product.discontinued
        })));
      }),
      catchError(() => of(null))
    );
  }

  // Reads only productId and quantity, so carts stored in an older format (with cartId) still work.
  private read(): StoredItem[]
  {
    const json = localStorage.getItem(this.storageKey);
    const stored: { cartItems?: StoredItem[] } = json ? JSON.parse(json) : {};
    return (stored.cartItems ?? []).map(({ productId, quantity }) => ({ productId, quantity }));
  }

  private save(items: StoredItem[]): void
  {
    localStorage.setItem(this.storageKey, JSON.stringify({ cartItems: items }));
  }
}
