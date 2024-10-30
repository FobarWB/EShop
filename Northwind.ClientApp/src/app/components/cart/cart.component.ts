import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
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
  standalone: true,
  imports: [FormsModule],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.css'
})
export class CartComponent implements OnInit
{
  cartItems: ICartItem[] = [];
  cartItemsDTO: { [productId: number]: ICartItemDTO } = {};

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
    this.cartService.getCartByUserId().subscribe(cart =>
    {
      this.cartItems = cart.cartItems;
      this.cartItemsDTO = {};
      this.cartItems.forEach(cartItem =>
      {
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

  clearCurrentCart()
  {
    this.cartService.clearCart().subscribe(() =>
    {
      this.loadCartItems();
    });
    window.location.reload();
  }

  deleteCurrentItem(cartItem: ICartItem)
  {
    this.cartService.removeItemFromCart(cartItem.productId).subscribe();
    // .subscribe(() =>
    // {
    //   this.loadCartItems();
    // });
    //window.location.reload();
  }

  updateQuantity(productId: number, newQuantity: number): void
  {
    if (newQuantity < 1) return;

    this.cartService.updateItemQuantity(productId, newQuantity).subscribe();
    // .subscribe(() =>
    // {
    //   this.loadCartItems();
    // });
    //window.location.reload();
  }

  onQuantityChange(cartItem: ICartItem): void
  {
    const newQuantity = cartItem.quantity < 1 ? 1 : cartItem.quantity;
    this.updateQuantity(cartItem.productId, newQuantity);
    //window.location.reload();
  }

}
