import { Component } from '@angular/core';
import { ProductsService } from '../services/products.service';
import { Product } from '../models/product.model';
import { Observable } from 'rxjs';
import { AsyncPipe, NgFor } from '@angular/common';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, NgFor],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent
{
  products$: Product[] = [];

  constructor(private productService: ProductsService) { }

  ngOnInit()
  {
    this.productService
      .getProducts("https://localhost:5002/api/products", { page: 0, perPage: 5 })
      .subscribe((products: Product[]) => this.products$ = products);
  }
}
