import { Injectable } from '@angular/core';
import { Observable, Subject, tap } from 'rxjs';
import { ApiService } from './api.service';
import { IProduct } from '../models/product.model';
import { IPage, maxPageSize } from '../models/page.model';
import { CacheService } from './cache.service';

@Injectable({
  providedIn: 'root'
})
export class ProductService
{
  private apiUrl = '/api/products';
  public refreshRequired = new Subject<void>;

  constructor(
    private apiService: ApiService,
    private cacheService: CacheService
  ) 
  {
    this.refreshRequired.subscribe(_ => this.cacheService.clear());
  }

  // The category page filters and sorts in the browser, so it asks for the largest page the API allows
  // and expects the whole category in it (server-side filters and paging: #61).
  getProducts = (categoryId?: number): Observable<IPage<IProduct>> =>
  {
    const category = categoryId ? `categoryId=${categoryId}&` : '';
    return this.apiService.get(`${this.apiUrl}?${category}pageSize=${maxPageSize}`);
  };

  getProduct(id: number): Observable<IProduct>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.get(url);
  }

  createProduct(body?: any): Observable<IProduct>
  {
    return this.apiService.post<IProduct>(this.apiUrl, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  updateProduct(id?: number, body?: any): Observable<IProduct>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.put<IProduct>(url, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  deleteProduct(id?: number): Observable<IProduct>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.delete<IProduct>(url).pipe(tap(_ => this.refreshRequired.next()));
  }
}
