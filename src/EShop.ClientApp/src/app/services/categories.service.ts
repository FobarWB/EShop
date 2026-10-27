import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { ICategory } from '../models/category.model';
import { IPage, maxPageSize } from '../models/page.model';
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

  // The home page shows every category: one page of the largest size the API allows.
  getCategories(categoryName?: string): Observable<IPage<ICategory>>
  {
    const cacheKey = `categories_${categoryName || 'all'}`;
    const cachedCategories = this.cacheService.get(cacheKey);
    if (cachedCategories)
    {
      return cachedCategories;
    }
    const name = categoryName ? `categoryName=${encodeURIComponent(categoryName)}&` : '';
    return this.apiService.get<IPage<ICategory>>(`${this.apiUrl}?${name}pageSize=${maxPageSize}`).pipe(tap(page =>
    {
      this.cacheService.set(cacheKey, page);
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
