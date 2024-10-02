import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Category } from '../models/category.model';
import { map, Observable, of, Subject, tap } from 'rxjs';
import { CacheService } from './cache.service';

@Injectable({
  providedIn: 'root'
})
export class CategoryService
{
  private apiUrl = 'https://localhost:5003/api/categories';
  public refreshRequired = new Subject<void>;

  constructor(
    private apiService: ApiService,
    private cacheService: CacheService
  )
  {
    this.refreshRequired.subscribe(_ => this.cacheService.clear());
  }

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

  updateCategory(id?: number, body?: any): Observable<Category>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.put<Category>(url, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  createCategory(body?: any): Observable<Category>
  {
    return this.apiService.post<Category>(this.apiUrl, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  deleteCategory(id?: number): Observable<Category>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.delete<Category>(url).pipe(tap(_ => this.refreshRequired.next()));
  }
}
