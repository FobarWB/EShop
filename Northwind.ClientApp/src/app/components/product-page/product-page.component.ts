import { Component } from '@angular/core';
import { CategorySideBarComponent } from '../category-side-bar/category-side-bar.component';
import { CommonModule } from '@angular/common';
import { Product } from '../../models/product.model';
import { ProductService } from '../../services/products.service';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Category } from '../../models/category.model';

@Component({
  selector: 'app-product-page',
  standalone: true,
  imports: [CategorySideBarComponent, CommonModule, RouterModule],
  templateUrl: './product-page.component.html',
  styleUrl: './product-page.component.css'
})
export class ProductPageComponent
{
  products: Product[] = [];
  categoryId: number | undefined = undefined;
  constructor(
    private route: ActivatedRoute,
    private productService: ProductService) { }

  ngOnInit(): void
  {
    this.route.paramMap.subscribe(params =>
    {
      const id = params.get('id');
      this.categoryId = id ? +id : undefined;

      this.productService
        .getProducts(this.categoryId)
        .subscribe((products: Product[]) =>
        {
          this.products = products;
        });
    });
  }

  get categoryName(): string | null
  {
    return this.route.snapshot.paramMap.get('categoryName');
  }
}
