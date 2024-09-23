import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { SideBarComponent } from '../side-bar/side-bar.component';
import { Product } from '../../../models/product.model';
import { ProductService } from '../../../services/products.service';

@Component({
  selector: 'app-category-page',
  standalone: true,
  imports: [SideBarComponent, CommonModule, RouterModule],
  templateUrl: './page.component.html',
  styleUrl: './page.component.css'
})
export class PageComponent
{
  products: Product[] = [];
  categoryId: number | undefined = undefined;
  categoryName: string | undefined = undefined;

  constructor(
    private route: ActivatedRoute,
    private productService: ProductService) { }

  ngOnInit(): void
  {
    this.route.paramMap.subscribe(params =>
    {
      const id = params.get('id');
      const name = params.get('categoryName');
      this.categoryId = id ? +id : undefined;
      this.categoryName = name ? decodeURIComponent(name) : "All products";
      this.productService
        .getProducts(this.categoryId)
        .subscribe((products: Product[]) => this.products = products);
    });
  }
}
