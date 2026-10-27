import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { OrderComponent } from './order.component';
import { IOrder } from '../../models/order.model';

const order: IOrder = {
  orderId: 7,
  orderDate: '2026-10-27T12:00:00Z',
  shipName: 'Jane Doe',
  shipAddress: '1 Main St',
  shipCity: 'Berlin',
  shipRegion: null,
  shipPostalCode: '10115',
  shipCountry: 'Germany',
  freight: 5,
  total: 41,
  items: [{ productId: 1, productName: 'Chai', unitPrice: 18, quantity: 2 }]
};

describe('OrderComponent', () =>
{
  let fixture: ComponentFixture<OrderComponent>;
  let httpTesting: HttpTestingController;

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [OrderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: '7' })) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OrderComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('shows the order number, the items, the total and the address', () =>
  {
    fixture.detectChanges();
    httpTesting.expectOne('/api/orders/7').flush(order);
    fixture.detectChanges();

    const page = text(fixture.nativeElement);
    expect(page).toContain('Order #7');
    const cells = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.order-item td'));
    expect(cells.map(cell => text(cell).trim())).toEqual(['Chai', '€ 18.00', '2']);
    expect(page).toContain('Total: € 41.00');
    expect(page).toContain('10115 Berlin');
  });

  it('says the order was not found on 404', () =>
  {
    fixture.detectChanges();
    httpTesting.expectOne('/api/orders/7').flush('Order not found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(text(fixture.nativeElement)).toContain('Order not found.');
  });
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
