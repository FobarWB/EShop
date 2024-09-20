import { Component } from '@angular/core';
import { Observable } from 'rxjs';
import { AsyncPipe, NgFor } from '@angular/common';
import { Product } from '../../models/product.model';
import { CategoryService } from '../../services/categories.service';
import { Category } from '../../models/category.model';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, NgFor],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent
{
  categories: Category[] = [];

  constructor(private categoryService: CategoryService) { }

  ngOnInit()
  {
    this.categoryService
      .getCategories()
      .subscribe((categories: Category[]) => this.categories = categories);
  }
}
