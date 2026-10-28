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
  status: 'Pending',
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

  afterEach(() =>
  {
    httpTesting.verify();
    vi.restoreAllMocks();
  });

  it('shows the order number, the items, the total and the address', () =>
  {
    load(order);

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

  it('shows the status', () =>
  {
    load(order);

    expect(text(fixture.nativeElement.querySelector('.order-status'))).toContain('Pending');
  });

  it('cancels the order after confirmation and shows the new status', () =>
  {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    load(order);

    cancelButton().click();
    const req = httpTesting.expectOne('/api/orders/7/cancel');
    expect(req.request.method).toBe('POST');
    req.flush({ ...order, status: 'Cancelled' });
    fixture.detectChanges();

    expect(text(fixture.nativeElement.querySelector('.order-status'))).toContain('Cancelled');
    expect(cancelButton()).toBeNull();
  });

  it('does not cancel when the user declines', () =>
  {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    load(order);

    cancelButton().click();

    httpTesting.expectNone('/api/orders/7/cancel');
  });

  it('offers no cancel button for a shipped order', () =>
  {
    load({ ...order, status: 'Shipped' });

    expect(cancelButton()).toBeNull();
  });

  it('explains a 409 and keeps the order', () =>
  {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    load(order);

    cancelButton().click();
    httpTesting.expectOne('/api/orders/7/cancel').flush('Conflict', { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(text(fixture.nativeElement.querySelector('.cancel-error'))).toContain('can no longer be cancelled');
    expect(text(fixture.nativeElement.querySelector('.order-status'))).toContain('Pending');
  });

  function load(value: IOrder): void
  {
    fixture.detectChanges();
    httpTesting.expectOne('/api/orders/7').flush(value);
    fixture.detectChanges();
  }

  function cancelButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.cancel-order');
  }
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
