import { Injectable } from '@angular/core';
import { Observable, Subject, tap } from 'rxjs';
import { ApiService } from './api.service';
import { IProduct } from '../models/product.model';
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

  getProducts = (productId?: number): Observable<IProduct[]> =>
  {
    const url = productId ? `${this.apiUrl}?categoryId=${productId}` : this.apiUrl;
    return this.apiService.get(url);
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
