import { Component, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ProductService } from '../../../services/products.service';
import { IProduct } from '../../../models/product.model';
import { SpinnerComponent } from "../../spinner/spinner.component";

@Component({
  selector: 'app-page',
  imports: [SpinnerComponent],
  templateUrl: './page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './page.component.css'
})
export class PageComponent
{
  product?: IProduct;
  productId?: number;
  productName?: string;
  isLoading: boolean = false;

  constructor(private router: Router,
    private activatedRoute: ActivatedRoute,
    private productService: ProductService) { }

  ngOnInit(): void
  {
    this.activatedRoute.paramMap.subscribe(params =>
    {
      this.isLoading = true;
      const id = params.get('id');
      const name = params.get('productName');
      this.productId = id ? +id : undefined;
      this.productName = name ? decodeURIComponent(name) : undefined;
      this.validateProduct()
        .then(_ =>
        {
          this.getProduct()
        })
        .catch(_ =>
        {
          this.router.navigate(["/"]);
        });
    });
  }

  getProduct()
  {
    this.productService
      .getProduct(this.productId!)
      .subscribe(product => 
      {
        this.product = product;
        setTimeout(() => this.isLoading = false, 500);
      });
  }

  validateProduct(): Promise<void>
  {
    return new Promise<void>((resolve, reject) =>
    {
      if (!this.productId)
      {
        reject();
      }
      this.productService
        .getProduct(this.productId!)
        .subscribe(product =>
        {
          if (product == null)
          {
            reject();
          }
          if (product.productName == this.productName)
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
}
