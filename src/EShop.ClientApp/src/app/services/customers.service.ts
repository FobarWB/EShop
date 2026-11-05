import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IAddress, IAddressRequest } from '../models/address.model';
import { ICustomerProfile } from '../models/customer-profile.model';
import { ApiService } from './api.service';

@Injectable({
  providedIn: 'root'
})
export class CustomersService
{
  private readonly api = inject(ApiService);
  private readonly apiUrl = '/api/customers/me';

  getProfile(): Observable<ICustomerProfile>
  {
    return this.api.get<ICustomerProfile>(this.apiUrl);
  }

  // Null or empty removes the phone.
  updatePhone(phone: string | null): Observable<ICustomerProfile>
  {
    return this.api.put<ICustomerProfile>(this.apiUrl, { phone });
  }

  // The default address comes first.
  getAddresses(): Observable<IAddress[]>
  {
    return this.api.get<IAddress[]>(`${this.apiUrl}/addresses`);
  }

  addAddress(address: IAddressRequest): Observable<IAddress>
  {
    return this.api.post<IAddress>(`${this.apiUrl}/addresses`, address);
  }

  updateAddress(addressId: number, address: IAddressRequest): Observable<IAddress>
  {
    return this.api.put<IAddress>(`${this.apiUrl}/addresses/${addressId}`, address);
  }

  deleteAddress(addressId: number): Observable<void>
  {
    return this.api.delete<void>(`${this.apiUrl}/addresses/${addressId}`);
  }
}
