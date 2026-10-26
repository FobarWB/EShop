import { ChangeDetectorRef, Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CartService } from '../../services/cart.service';
import { ProductService } from '../../services/products.service';
import { ICartItem } from '../../models/cart-item.model';
import { CategoryService } from '../../services/categories.service';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';

export interface ICartItemDTO
{
  categoryName?: string | null;
  productName?: string | null;
  unitPrice?: number | null;
}

@Component({
  selector: 'app-cart',
  imports: [FormsModule],
  templateUrl: './cart.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './cart.component.css'
})
export class CartComponent implements OnInit
{
  cartItems: ICartItem[] = [];
  cartItemsDTO: { [productId: number]: ICartItemDTO } = {};
  protected errorMessage: string | null = null;
  // Quantities the server has confirmed; the input's ngModel changes cartItem.quantity before the request.
  private confirmedQuantities: { [productId: number]: number } = {};

  constructor(
    private cartService: CartService,
    private productService: ProductService,
    private categoryService: CategoryService,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void
  {
    this.loadCartItems();
  }

  private loadCartItems(): void
  {
    this.cartService.getCart().subscribe(cart =>
    {
      this.cartItems = cart.cartItems;
      this.confirmedQuantities = {};
      this.cartItemsDTO = {};
      this.cartItems.forEach(cartItem =>
      {
        this.confirmedQuantities[cartItem.productId] = cartItem.quantity;
        this.cartItemsDTO[cartItem.productId] = {};
        this.productService.getProduct(cartItem.productId).subscribe(product =>
        {
          const categoryId = product.categoryId ? product.categoryId : 0;
          this.categoryService.getCategory(categoryId).subscribe(category =>
          {
            this.cartItemsDTO[cartItem.productId].categoryName = category.categoryName;
          });
          this.cartItemsDTO[cartItem.productId].productName = product.productName;
          this.cartItemsDTO[cartItem.productId].unitPrice = product.unitPrice;
        });
      });
    });
  }

  getProductName(cartItem: ICartItem)
  {
    return this.cartItemsDTO[cartItem.productId].productName;
  }

  getProductCategoryName(cartItem: ICartItem)
  {
    return this.cartItemsDTO[cartItem.productId].categoryName;
  }

  getPriceForCartItem(cartItem: ICartItem)
  {
    return this.cartItemsDTO[cartItem.productId].unitPrice;
  }

  getSummaryPriceForCartItem(cartItem: ICartItem)
  {
    const priceForOne = this.getPriceForCartItem(cartItem);
    return priceForOne ? priceForOne * cartItem.quantity : priceForOne;
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
