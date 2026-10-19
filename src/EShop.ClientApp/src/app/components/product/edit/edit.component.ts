import { Component, ChangeDetectionStrategy } from '@angular/core';
import { IProduct } from '../../../models/product.model';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ModalComponent } from '../../modal/modal.component';
import { ProductService } from '../../../services/products.service';

@Component({
  selector: 'app-edit',
  imports: [ReactiveFormsModule],
  templateUrl: './edit.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './edit.component.css'
})
export class EditComponent 
{

  protected product!: IProduct;
  protected productForm!: FormGroup;

  set data(newProduct: IProduct)
  {
    this.product = newProduct;
    this.productForm.patchValue(this.product);
  }

  constructor(
    private modalComponent: ModalComponent,
    private formBuilder: FormBuilder,
    private productService: ProductService
  ) { }

  ngOnInit(): void
  {
    this.productForm = this.formBuilder.group({
      productName: ['', [Validators.required, Validators.maxLength(25)]],
      supplierId: [null],
      categoryId: [null],
      quantityPerUnit: [''],
      unitPrice: [null],
      unitsInStock: [null],
      unitsOnOrder: [null],
      reorderLevel: [null],
      discontinued: [false]
    });

    this.productForm.statusChanges.subscribe(() =>
      this.modalComponent.disabled = this.productForm.invalid
    );
    this.modalComponent.disabled = this.productForm.invalid;
  }

  update()
  {
    const product = {
      productId: this.product.productId,
      productName: this.productForm.get('productName')?.value,
      supplierId: this.productForm.get('supplierId')?.value,
      categoryId: this.productForm.get('categoryId')?.value,
      quantityPerUnit: this.productForm.get('quantityPerUnit')?.value,
      unitPrice: this.productForm.get('unitPrice')?.value,
      unitsInStock: this.productForm.get('unitsInStock')?.value,
      unitsOnOrder: this.productForm.get('unitsOnOrder')?.value,
      reorderLevel: this.productForm.get('reorderLevel')?.value,
      discontinued: this.productForm.get('discontinued')?.value,
    };

    this.productService.updateProduct(this.product?.productId, product).subscribe();
  }

  create()
  {
    const product = {
      productName: this.productForm.get('productName')?.value,
      supplierId: this.productForm.get('supplierId')?.value,
      categoryId: this.productForm.get('categoryId')?.value,
      quantityPerUnit: this.productForm.get('quantityPerUnit')?.value,
      unitPrice: this.productForm.get('unitPrice')?.value,
      unitsInStock: this.productForm.get('unitsInStock')?.value,
      unitsOnOrder: this.productForm.get('unitsOnOrder')?.value,
      reorderLevel: this.productForm.get('reorderLevel')?.value,
      discontinued: this.productForm.get('discontinued')?.value,
    };

    this.productService.createProduct(product).subscribe();
  }

  delete()
  {
    if (this.product)
    {
      this.productService.deleteProduct(this.product.productId).subscribe();
      this.modalComponent.close();
    }
  }
}
