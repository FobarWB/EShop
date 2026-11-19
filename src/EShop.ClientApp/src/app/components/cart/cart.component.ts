import { Component, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartService } from '../../services/cart.service';
import { CategoryService } from '../../services/categories.service';
import { cartItemProblem, ICartItem, isUnavailable } from '../../models/cart-item.model';
import { standardFreight } from '../../models/order.model';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cart',
  imports: [FormsModule, RouterLink],
  templateUrl: './cart.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './cart.component.css'
})
export class CartComponent implements OnInit
{
  cartItems: ICartItem[] = [];
  protected readonly freight = standardFreight;
  protected errorMessage: string | null = null;
  // Quantities the server has confirmed; the input's ngModel changes cartItem.quantity before the request.
  private confirmedQuantities: { [productId: number]: number } = {};
  private readonly categoryService = inject(CategoryService);
  // Category names by id for the item rows: the cart has only the ids (#75).
  private categoryNames = new Map<number, string>();

  constructor(private cartService: CartService) { }

  ngOnInit(): void
  {
    this.loadCartItems();
  }

  // The cart comes with product names, category ids and current prices: no request per item.
  private loadCartItems(): void
  {
    this.cartService.getCart().subscribe(cart =>
    {
      this.cartItems = cart.cartItems;
      this.confirmedQuantities = {};
      this.cartItems.forEach(cartItem => this.confirmedQuantities[cartItem.productId] = cartItem.quantity);
      this.loadCategoryNames();
    });
  }

  // One request for all names, cached by CategoryService, and only when an item has a category. A failure leaves
  // the names empty: the cart works without them.
  private loadCategoryNames(): void
  {
    if (!this.cartItems.some(cartItem => cartItem.categoryId !== null))
    {
      return;
    }
    this.categoryService.getCategories().subscribe({
      next: page => this.categoryNames = new Map(page.items.map(c => [c.categoryId, c.categoryName])),
      error: () => undefined
    });
  }

  protected categoryNameOf(cartItem: ICartItem): string | null
  {
    return cartItem.categoryId === null ? null : this.categoryNames.get(cartItem.categoryId) ?? null;
  }

  getSummaryPriceForCartItem(cartItem: ICartItem)
  {
    return cartItem.unitPrice ? cartItem.unitPrice * cartItem.quantity : cartItem.unitPrice;
  }

  getSummaryPriceForAllCartItems()
  {
    let sum: number = 0;
    this.cartItems.forEach(cartItem =>
    {
      const price = this.getSummaryPriceForCartItem(cartItem);
      sum += price ? price : 0;
    });
    return sum;
  }

  protected problemOf(cartItem: ICartItem): string | null
  {
    return cartItemProblem(cartItem);
  }

  protected isUnavailable(cartItem: ICartItem): boolean
  {
    return isUnavailable(cartItem);
  }

  // The cart shows the current stock: an order the API would refuse is not started.
  protected canCheckout(): boolean
  {
    return this.cartItems.length > 0 && this.cartItems.every(cartItem => cartItemProblem(cartItem) === null);
  }

  protected clearCart(): void
  {
    this.cartService.clearCart().subscribe({
      next: () =>
      {
        this.cartItems = [];
        this.errorMessage = null;
      },
      error: () => this.errorMessage = 'Could not clear the cart. Please try again.'
    });
  }

  protected removeItem(cartItem: ICartItem): void
  {
    this.cartService.removeItemFromCart(cartItem.productId).subscribe({
      next: () =>
      {
        this.cartItems = this.cartItems.filter(i => i.productId !== cartItem.productId);
        this.errorMessage = null;
      },
      error: () => this.errorMessage = 'Could not remove the item. Please try again.'
    });
  }

  protected changeQuantity(cartItem: ICartItem, newQuantity: number): void
  {
    const confirmedQuantity = this.confirmedQuantities[cartItem.productId];
    // Also rejects NaN from an emptied input.
    if (!(newQuantity >= 1))
    {
      cartItem.quantity = confirmedQuantity;
      return;
    }

    this.cartService.updateItemQuantity(cartItem.productId, newQuantity).subscribe({
      next: () =>
      {
        cartItem.quantity = newQuantity;
        this.confirmedQuantities[cartItem.productId] = newQuantity;
        this.errorMessage = null;
      },
      error: () =>
      {
        cartItem.quantity = confirmedQuantity;
        this.errorMessage = 'Could not change the quantity. Please try again.';
      }
    });
  }
}
