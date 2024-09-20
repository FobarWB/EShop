import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Category } from '../models/category.model';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class CategoryService
{
  private apiUrl = 'https://localhost:5003/api/categories';

  constructor(private apiService: ApiService) { }

  getCategories(categoryName?: string): Observable<Category[]>
  {
    const url = categoryName ? `${this.apiUrl}?categoryName=${categoryName}` : this.apiUrl;
    return this.apiService.get(url);
  }
}
