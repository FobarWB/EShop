import { Component, TemplateRef, ViewChild, ViewContainerRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { SideBarComponent } from '../side-bar/side-bar.component';
import { IProduct } from '../../../models/product.model';
import { ProductService } from '../../../services/products.service';
import { CategoryService } from '../../../services/categories.service';
import { ModalService } from '../../../services/modal.service';
import { EditComponent } from '../../product/edit/edit.component';
import { SpinnerComponent } from "../../spinner/spinner.component";
import { IFilters } from '../../../models/filters.model';

@Component({
  selector: 'app-category-page',
  standalone: true,
  imports: [SideBarComponent, CommonModule, RouterModule, SpinnerComponent],
  templateUrl: './page.component.html',
  styleUrl: './page.component.css'
})
export class PageComponent
{
  @ViewChild("clearFilters", { read: TemplateRef, static: true }) clearFiltersButton!: TemplateRef<any>;
  @ViewChild("filterFlags", { read: ViewContainerRef, static: true }) filtersViewContainerRef!: ViewContainerRef;
  @ViewChild(SideBarComponent, { static: true }) sideBarComponent!: SideBarComponent;
  products: IProduct[] = [];
  minPrice: number = 0;
  maxPrice: number = 0;
  filteredProducts: IProduct[] = [];
  categoryId?: number = undefined;
  categoryName?: string = undefined;
  isLoading: boolean = false;
  filters = new Map<string, any>();

  constructor(
    private router: Router,
    private activatedRoute: ActivatedRoute,
    private productService: ProductService,
    private categoryService: CategoryService,
    private modalService: ModalService,
  ) { }

  ngOnInit(): void
  {
    this.activatedRoute.paramMap.subscribe(params =>
    {
      this.isLoading = true;
      const id = params.get('id');
      const name = params.get('categoryName');
      this.categoryId = id ? +id : undefined;
      this.categoryName = name ? decodeURIComponent(name) : "All products";
      this.validateCategory()
        .then(_ =>
        {
          this.getAllProducts();
          this.productService.refreshRequired.subscribe(_ => this.getAllProducts());
        })
        .catch(_ =>
        {
          this.router.navigate(["/"]);
        });
    });
  }

  getAllProducts()
  {
    this.productService
      .getProducts(this.categoryId)
      .subscribe((products: IProduct[]) =>
      {
        this.products = products;
        this.filteredProducts = products;
        const prices = this.products
          .map(product => product.unitPrice)
          .filter(price => price !== null);
        this.minPrice = Math.floor(Math.min(...prices));
        this.maxPrice = Math.ceil(Math.max(...prices));
        setTimeout(() => this.isLoading = false, 500);
      });
  }

  createProduct()
  {
    this.modalService
      .open(EditComponent, { title: "Create new Product" })
      .subscribe((component): EditComponent => component.create());
  }

  updateProduct(product: IProduct)
  {
    this.modalService
      .open(EditComponent, { title: `Edit product «${product.productName}»`, data: product })
      .subscribe((component): EditComponent => component.update());
  }

  validateCategory(): Promise<void>
  {
    return new Promise<void>((resolve, reject) =>
    {
      if (!this.categoryId)
      {
        reject();
      }
      this.categoryService
        .getCategory(this.categoryId!)
        .subscribe(category =>
        {
          if (category == null)
          {
            reject();
          }
          if (category.categoryName == this.categoryName)
          {
            resolve();
          }
          else
          {
            reject();
          }
        });
    });
  }

  encodeProductName(name: string): any | string
  {
    return encodeURIComponent(name);
  }

  encodeProductId(id: number): any | string
  {
    return `p${id}`;
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

  filtersAppliedHandler(filters: IFilters)
  {
    this.filteredProducts = this.products.filter(product =>
    {
      const meetsPriceCriteria = filters.priceRange ? product.unitPrice! >= filters.priceRange.min && product.unitPrice! <= filters.priceRange.max : true;
      const meetsStockCriteria = filters.unitsInStock ? product.unitsInStock !== null && product.unitsInStock! > 0 : true;
      const meetsDiscountCriteria = filters.discounted ? product.discontinued === true : true;

      return meetsPriceCriteria && meetsStockCriteria && meetsDiscountCriteria;
    });

    this.filters.clear();
    for (const [key, value] of Object.entries(filters))
    {
      if (value)
      {
        this.filters.set(key, () => this.sideBarComponent.removeFilter(<keyof IFilters>key));
      }
    }
  }

  removeAllFilters()
  {
    this.sideBarComponent.removeAllFilters();
  }
}
