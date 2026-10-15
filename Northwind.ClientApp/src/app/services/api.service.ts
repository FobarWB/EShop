import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ApiService
{
  constructor(private httpClient: HttpClient) { }

  get<T>(url: string): Observable<T>
  {
    return this.httpClient.get<T>(url);
  }

  put<T>(url: string, body?: any): Observable<T>
  {
    return this.httpClient.put<T>(url, body);
  }

  post<T>(url: string, body?: any): Observable<T>
  {
    return this.httpClient.post<T>(url, body, { responseType: "json" });
  }

  delete<T>(url: string): Observable<T>
  {
    return this.httpClient.delete<T>(url);
  }
}
