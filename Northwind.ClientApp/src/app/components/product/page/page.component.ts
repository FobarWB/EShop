import { Component } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ProductService } from '../../../services/products.service';
import { Product } from '../../../models/product.model';

@Component({
  selector: 'app-page',
  standalone: true,
  imports: [],
  templateUrl: './page.component.html',
  styleUrl: './page.component.css'
})
export class PageComponent
{
  product?: Product;
  productId?: number;
  productName?: string;

  constructor(private router: Router,
    private activatedRoute: ActivatedRoute,
    private productService: ProductService) { }

  ngOnInit(): void
  {
    this.activatedRoute.paramMap.subscribe(params =>
    {
      const id = params.get('id');
      const name = params.get('productName');
      this.productId = id ? +id : undefined;
      this.productName = name ? decodeURIComponent(name) : undefined;
      this.validateProduct()
        .then(_ =>
        {
          this.productService
            .getProduct(this.productId!)
            .subscribe(product => this.product = product);
        })
        .catch(_ =>
        {
          this.router.navigate(["/"]);
        });
    });
  }

  validateProduct(): Promise<boolean>
  {
    return new Promise<boolean>((resolve, reject) =>
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
            resolve(true);
          }
          else
          {
            reject();
          }
        });
    });
  }
}
