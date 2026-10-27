import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { OrdersService } from './orders.service';
import { IShippingAddress } from '../models/shipping-address.model';

const address: IShippingAddress = {
  shipName: 'Jane Doe',
  shipAddress: '1 Main St',
  shipCity: 'Berlin',
  shipRegion: null,
  shipPostalCode: '10115',
  shipCountry: 'Germany'
};

describe('OrdersService', () =>
{
  let service: OrdersService;
  let httpTesting: HttpTestingController;

  beforeEach(() =>
  {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(OrdersService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('places an order with the address and the idempotency key', () =>
  {
    service.placeOrder(address, 'key-1').subscribe();

    const req = httpTesting.expectOne('/api/orders');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(address);
    expect(req.request.headers.get('Idempotency-Key')).toBe('key-1');
  });

  it('loads an order by its id', () =>
  {
    service.getOrder(7).subscribe();

    expect(httpTesting.expectOne('/api/orders/7').request.method).toBe('GET');
  });
});
