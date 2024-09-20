import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Product } from '../models/product.model';

@Injectable({
  providedIn: 'root'
})
export class ProductService
{
  private apiUrl = 'https://localhost:5002/api/products';

  constructor(private apiService: ApiService) { }

  getProducts = (categoryId?: number): Observable<Product[]> =>
  {
    const url = categoryId ? `${this.apiUrl}?categoryId=${categoryId}` : this.apiUrl;
    return this.apiService.get(url);
  };

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
