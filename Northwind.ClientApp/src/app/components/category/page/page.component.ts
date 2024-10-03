import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { SideBarComponent } from '../side-bar/side-bar.component';
import { Product } from '../../../models/product.model';
import { ProductService } from '../../../services/products.service';
import { CategoryService } from '../../../services/categories.service';
import { Category } from '../../../models/category.model';

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
  filteredProducts: Product[] = [];
  categoryId?: number = undefined;
  categoryName?: string = undefined;

  constructor(
    private router: Router,
    private activatedRoute: ActivatedRoute,
    private productService: ProductService,
    private categoryService: CategoryService) { }

  ngOnInit(): void
  {
    this.activatedRoute.paramMap.subscribe(params =>
    {
      const id = params.get('id');
      const name = params.get('categoryName');
      this.categoryId = id ? +id : undefined;
      this.categoryName = name ? decodeURIComponent(name) : "All products";
      this.validateCategory().then((isValid) =>
      {
        if (!isValid)
        {
          this.router.navigate(["/"]);
        }
      });
      this.productService
        .getProducts(this.categoryId)
        .subscribe((products: Product[]) =>
        {
          this.products = products;
          this.filteredProducts = products;
        });
    });
  }

  validateCategory(): Promise<boolean>
  {
    return new Promise<boolean>((resolve, reject) =>
    {
      this.categoryService
        .getCategories()
        .subscribe((categories: Category[]) =>
        {
          for (let c of categories)
          {
            if (c.categoryId == this.categoryId && c.categoryName == this.categoryName)
            {
              resolve(true);
            }
          }
          resolve(false);
        });
    });
  }

  public sortProductsAsc(): void
  {
    this.products = this.products.sort((a, b) => a.unitPrice! - b.unitPrice!);
  }

  public sortProductsDesc(): void
  {
    this.products = this.products.sort((a, b) => b.unitPrice! - a.unitPrice!);
  }

  public sortByName(): void
  {
    this.products = this.products.sort((a, b) => a.productName!.localeCompare(b.productName!));
  }

  onFiltersApplied(filters: any)
  {
    this.filteredProducts = this.products.filter(product =>
    {
      const meetsPriceCriteria = product.unitPrice! >= filters.priceRange.min && product.unitPrice! <= filters.priceRange.max;
      const meetsStockCriteria = filters.unitsInStock ? product.unitsInStock! > 0 : true;
      const meetsDiscountCriteria = filters.discounted ? product.discontinued! === true : true;

      return meetsPriceCriteria && meetsStockCriteria && meetsDiscountCriteria;
    });
  }
}
