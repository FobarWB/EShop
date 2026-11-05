import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { CustomersService } from './customers.service';
import { IAddressRequest } from '../models/address.model';

const profileUrl = '/api/customers/me';
const addressesUrl = '/api/customers/me/addresses';

const request: IAddressRequest = {
  recipientName: 'Jane Doe',
  address: '1 Main St',
  city: 'Berlin',
  region: null,
  postalCode: '10115',
  country: 'Germany',
  isDefault: false
};

describe('CustomersService', () =>
{
  let service: CustomersService;
  let httpTesting: HttpTestingController;

  beforeEach(() =>
  {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CustomersService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('reads the profile', () =>
  {
    service.getProfile().subscribe();

    expect(httpTesting.expectOne(profileUrl).request.method).toBe('GET');
  });

  it('sends the phone', () =>
  {
    service.updatePhone('+49 30 1234567').subscribe();

    const req = httpTesting.expectOne(profileUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ phone: '+49 30 1234567' });
  });

  it('sends null to remove the phone', () =>
  {
    service.updatePhone(null).subscribe();

    expect(httpTesting.expectOne(profileUrl).request.body).toEqual({ phone: null });
  });

  it('reads the addresses', () =>
  {
    service.getAddresses().subscribe();

    expect(httpTesting.expectOne(addressesUrl).request.method).toBe('GET');
  });

  it('adds an address', () =>
  {
    service.addAddress(request).subscribe();

    const req = httpTesting.expectOne(addressesUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
  });

  it('updates an address', () =>
  {
    service.updateAddress(5, request).subscribe();

    const req = httpTesting.expectOne(`${addressesUrl}/5`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
  });

  it('deletes an address', () =>
  {
    service.deleteAddress(5).subscribe();

    expect(httpTesting.expectOne(`${addressesUrl}/5`).request.method).toBe('DELETE');
  });
});
