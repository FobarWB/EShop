import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { CacheContent } from '../models/cache.model';

@Injectable({
  providedIn: 'root'
})
export class CacheService
{
  private cache = new Map<string, CacheContent>();

  constructor() { }

  set(key: string, value: any, ttl: number = 300000): void
  {
    const expiration = ttl ? Date.now() + ttl : 0;
    this.cache.set(key, { expiration, value });
  }

  get(key: string): Observable<any> | undefined
  {
    const data = this.cache.get(key);
    if (!data)
    {
      return undefined;
    }
    if (data.expiration < Date.now())
    {
      this.cache.delete(key);
      return undefined;
    }
    return of(data.value);
  }

  remove(key: string): void
  {
    this.cache.delete(key);
  }

  clear(): void
  {
    this.cache.clear();
  }
}
