import { Component, TemplateRef, ViewContainerRef } from '@angular/core';
import { Observable } from 'rxjs';
import { AsyncPipe, NgFor } from '@angular/common';
import { Product } from '../../models/product.model';
import { CategoryService } from '../../services/categories.service';
import { Category } from '../../models/category.model';
import { RouterModule } from '@angular/router';
import { EditComponent } from '../category/edit/edit.component';
import { ModalService } from '../../services/modal.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, NgFor, RouterModule, EditComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent
{
  categories: Category[] = [];

  constructor(
    private categoryService: CategoryService,
    private modalService: ModalService,
    private viewContainerRef: ViewContainerRef) { }

  ngOnInit()
  {
    this.categoryService
      .getCategories()
      .subscribe((categories: Category[]) => this.categories = categories);
  }

  encodeCategoryName(categoryName: string): string
  {
    return encodeURIComponent(categoryName);
  }

  openModal(modalTemplate: TemplateRef<any>)
  {
    this.modalService
      .open(this.viewContainerRef, modalTemplate, { size: 'lg', title: 'Foo' })
      .subscribe(action =>
      {
        console.log('modalAction', action);
      });
  }
}
