import { Component } from '@angular/core';
import { AsyncPipe, NgFor } from '@angular/common';
import { CategoryService } from '../../services/categories.service';
import { Category } from '../../models/category.model';
import { RouterModule } from '@angular/router';
import { EditComponent } from '../category/edit/edit.component';
import { ModalService } from '../../services/modal.service';
import { DefaultImageDirective } from '../../directives/default-image.directive';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, NgFor, RouterModule, DefaultImageDirective],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent
{
  categories: Category[] = [];

  constructor(
    private categoryService: CategoryService,
    private modalService: ModalService) { }

  ngOnInit()
  {
    this.getAllCategories();
    this.categoryService.refreshRequired.subscribe(_ => this.getAllCategories())
  }

  getAllCategories()
  {
    this.categoryService
      .getCategories()
      .subscribe(categories => this.categories = categories);
  }

  updateCategory(category: Category)
  {
    this.modalService
      .open(EditComponent, { title: `Edit category «${category.categoryName}»`, data: category })
      .subscribe((component): EditComponent => component.update());
  }

  createCategory()
  {
    this.modalService
      .open(EditComponent, { title: "Create new category" })
      .subscribe((component): EditComponent => component.create());
  }

  encodeCategoryName(categoryName: string): string
  {
    return encodeURIComponent(categoryName);
  }
}
