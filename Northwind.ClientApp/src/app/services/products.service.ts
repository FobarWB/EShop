import { Injectable } from '@angular/core';
import { Observable, Subject, tap } from 'rxjs';
import { ApiService } from './api.service';
import { Product } from '../models/product.model';
import { CacheService } from './cache.service';

@Injectable({
  providedIn: 'root'
})
export class ProductService
{
  private apiUrl = 'https://localhost:5002/api/products';
  public refreshRequired = new Subject<void>;

  constructor(
    private apiService: ApiService,
    private cacheService: CacheService
  ) 
  {
    this.refreshRequired.subscribe(_ => this.cacheService.clear());
  }

  getProducts = (productId?: number): Observable<Product[]> =>
  {
    const url = productId ? `${this.apiUrl}?categoryId=${productId}` : this.apiUrl;
    return this.apiService.get(url);
  };

  getProduct(id: number): Observable<Product>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.get(url);
  }

  createProduct(body?: any): Observable<Product>
  {
    return this.apiService.post<Product>(this.apiUrl, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  updateProduct(id?: number, body?: any): Observable<Product>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.put<Product>(url, body).pipe(tap(_ => this.refreshRequired.next()));
  }

  deleteProduct(id?: number): Observable<Product>
  {
    const url = `${this.apiUrl}/${id}`;
    return this.apiService.delete<Product>(url).pipe(tap(_ => this.refreshRequired.next()));
  }

  // getProducts(categoryId?: number): Observable<Product[]> {
  //   const url = categoryId ? `${this.apiUrl}?categoryId=${categoryId}` : this.apiUrl;
  //   return this.http.get<Product[]>(url).pipe(catchError(this.handleError));
  // }

  // // Получение одного продукта по ID
  // getProduct(id: number): Observable<Product> {
  //   const url = `${this.apiUrl}/${id}`;
  //   return this.http.get<Product>(url).pipe(catchError(this.handleError));
  // }

  // // Создание продукта
  // createProduct(product: Product): Observable<Product> {
  //   return this.http.post<Product>(this.apiUrl, product).pipe(catchError(this.handleError));
  // }

  // // Обновление продукта
  // updateProduct(id: number, product: Product): Observable<void> {
  //   const url = `${this.apiUrl}/${id}`;
  //   return this.http.put<void>(url, product).pipe(catchError(this.handleError));
  // }

  // // Удаление продукта
  // deleteProduct(id: number): Observable<void> {
  //   const url = `${this.apiUrl}/${id}`;
  //   return this.http.delete<void>(url).pipe(catchError(this.handleError));
  // }
}
