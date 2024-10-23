import { Component, OnInit } from '@angular/core';
import { CartService } from '../../services/cart.service';
import { ProductService } from '../../services/products.service'; // Correct import path
import { IProduct } from '../../models/product.model';
import { ICartItem } from '../../models/cart-item.model';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.css'
})

export class CartComponent implements OnInit
{
  cartItems: ICartItem[] = [];
  products: { [productId: number]: IProduct } = {};

  constructor(
    private cartService: CartService,
    private productService: ProductService
  ) { }

  ngOnInit(): void
  {
    // Get cart items
    this.cartService.getCartByUserId().subscribe(cart =>
    {
      this.cartItems = cart.cartItems;

      // Fetch product details for each item in the cart
      this.cartItems.forEach(cartItem =>
      {
        this.productService.getProduct(cartItem.productId).subscribe(product =>
        {
          this.products[cartItem.productId] = product; // Map productId to product details
        });
      });
    });

  }

  getProduct(productId: number): IProduct | null
  {
    return this.products[productId] || null;
  }
}
