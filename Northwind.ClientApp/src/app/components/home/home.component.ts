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
    this.categoryService
      .getCategories()
      .subscribe((categories: Category[]) => this.categories = categories);
  }

  encodeCategoryName(categoryName: string): string
  {
    return encodeURIComponent(categoryName);
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
}
