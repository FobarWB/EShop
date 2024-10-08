import { Component } from '@angular/core';
import { AsyncPipe, NgFor } from '@angular/common';
import { CategoryService } from '../../services/categories.service';
import { ICategory } from '../../models/category.model';
import { RouterModule } from '@angular/router';
import { EditComponent } from '../category/edit/edit.component';
import { ModalService } from '../../services/modal.service';
import { DefaultImageDirective } from '../../directives/default-image.directive';
import { SpinnerComponent } from "../spinner/spinner.component";

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, NgFor, RouterModule, DefaultImageDirective, SpinnerComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent
{
  isLoading: boolean = false;
  categories: ICategory[] = [];

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
    this.isLoading = true;
    this.categoryService
      .getCategories()
      .subscribe(categories => 
      {
        this.categories = categories;
        setTimeout(() => this.isLoading = false, 500);
      });
  }

  updateCategory(category: ICategory)
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

  encodeCategoryName(name: string): string
  {
    return encodeURIComponent(name);
  }

  encodeCategoryId(id: number): string
  {
    return `c${id}`;
  }
}
