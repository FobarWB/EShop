import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Category } from '../models/category.model';
import { map, Observable, of, tap } from 'rxjs';
import { CacheService } from './cache.service';

@Injectable({
  providedIn: 'root'
})
export class CategoryService
{
  private apiUrl = 'https://localhost:5003/api/categories';

  constructor(
    private apiService: ApiService,
    private cacheService: CacheService
  ) { }

  getCategories(categoryName?: string): Observable<Category[]>
  {
    const cacheKey = `categories_${categoryName || 'all'}`;
    const cachedCategories = this.cacheService.get(cacheKey);
    if (cachedCategories)
    {
      return cachedCategories;
    }
    const url = categoryName ? `${this.apiUrl}?categoryName=${categoryName}` : this.apiUrl;
    return this.apiService.get<Category[]>(url).pipe(tap(categories =>
    {
      this.cacheService.set(cacheKey, categories);
    }));
  }
}
