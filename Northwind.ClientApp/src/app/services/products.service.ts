import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PaginationParams } from '../../types';
import { ApiService } from './api.service';
import { Product } from '../models/product.model';

@Injectable({
  providedIn: 'root'
})
export class ProductsService
{

  constructor(private apiService: ApiService) { }

  getProducts = (url: string, params: PaginationParams): Observable<Product[]> =>
  {
    return this.apiService.get(url, {
      params,
      responseType: 'json',
    });
  };
}
