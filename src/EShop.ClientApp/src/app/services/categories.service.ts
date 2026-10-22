import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { ICategory } from '../models/category.model';
import { Observable, Subject, tap } from 'rxjs';
import { CacheService } from './cache.service';

@Injectable({
  providedIn: 'root'
})
export class CategoryService
{
  private apiUrl = '/api/categories';
  public refreshRequired = new Subject<void>;

  constructor(
    private apiService: ApiService,
    private cacheService: CacheService
  )
  {
    this.refreshRequired.subscribe(_ => this.cacheService.clear());
  }

  getCategories(categoryName?: string): Observable<ICategory[]>
  {
    const cacheKey = `categories_${categoryName || 'all'}`;
    const cachedCategories = this.cacheService.get(cacheKey);
    if (cachedCategories)
    {
      return cachedCategories;
    }
    const url = categoryName ? `${this.apiUrl}?categoryName=${categoryName}` : this.apiUrl;
    return this.apiService.get<ICategory[]>(url).pipe(tap(categories =>
    {
      this.cacheService.set(cacheKey, categories);
    }));
  }

  getCategory(id: number): Observable<ICategory>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.get(url);
  }

  updateCategory(id?: number, body?: any): Observable<ICategory>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.put<ICategory>(url, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  createCategory(body?: any): Observable<ICategory>
  {
    return this.apiService.post<ICategory>(this.apiUrl, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  deleteCategory(id?: number): Observable<ICategory>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.delete<ICategory>(url).pipe(tap(_ => this.refreshRequired.next()));
  }
}
