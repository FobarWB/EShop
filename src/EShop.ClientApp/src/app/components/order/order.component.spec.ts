import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { cartRefreshDelay, OrderComponent, statusPollInterval, statusPollLimit } from './order.component';
import { IOrder } from '../../models/order.model';
import { CartService } from '../../services/cart.service';

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
  let route: { paramMap: unknown; snapshot: { queryParamMap: ParamMap } };
  let cartService: { refreshItemCount: ReturnType<typeof vi.fn> };

  beforeEach(async () =>
  {
    route = {
      paramMap: of(convertToParamMap({ id: '7' })),
      snapshot: { queryParamMap: convertToParamMap({}) }
    };
    cartService = { refreshItemCount: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [OrderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: route },
        { provide: CartService, useValue: cartService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OrderComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() =>
  {
    // Global state first: a failed verify() must not leave fake timers or spies to the tests that follow.
    vi.useRealTimers();
    vi.restoreAllMocks();
    httpTesting.verify();
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

  it('thanks the buyer when coming from the checkout', () =>
  {
    route.snapshot.queryParamMap = convertToParamMap({ placed: 'true' });
    fixture = TestBed.createComponent(OrderComponent);

    load(order);

    expect(text(fixture.nativeElement.querySelector('h4'))).toContain('Thank you for your order!');
  });

  it('shows only the order number when opened from the order list', () =>
  {
    load(order);

    const heading = text(fixture.nativeElement.querySelector('h4'));
    expect(heading).toContain('Order #7');
    expect(heading).not.toContain('Thank you');
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

  it('offers a cancel button for a confirmed order', () =>
  {
    load({ ...order, status: 'Confirmed' });

    expect(cancelButton()).not.toBeNull();
  });

  it.each(['Shipped', 'Rejected'] as const)('offers no cancel button for a %s order', status =>
  {
    load({ ...order, status });

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

  it('says the stock is being checked while the order is pending', () =>
  {
    load(order);

    expect(text(fixture.nativeElement.querySelector('.status-note'))).toContain('checking that everything is in stock');
  });

  it('asks again until the order is decided, then stops', () =>
  {
    vi.useFakeTimers();
    load(order);

    vi.advanceTimersByTime(statusPollInterval);
    httpTesting.expectOne('/api/orders/7').flush(order);
    vi.advanceTimersByTime(statusPollInterval);
    httpTesting.expectOne('/api/orders/7').flush({ ...order, status: 'Confirmed' });
    fixture.detectChanges();

    expect(text(fixture.nativeElement.querySelector('.order-status'))).toContain('Confirmed');
    vi.advanceTimersByTime(statusPollInterval);
    httpTesting.expectNone('/api/orders/7');
  });

  it('reads the cart counter again a little after the confirmation', () =>
  {
    vi.useFakeTimers();
    load(order);
    vi.advanceTimersByTime(statusPollInterval);
    httpTesting.expectOne('/api/orders/7').flush({ ...order, status: 'Confirmed' });

    expect(cartService.refreshItemCount).not.toHaveBeenCalled();
    vi.advanceTimersByTime(cartRefreshDelay);
    expect(cartService.refreshItemCount).toHaveBeenCalledOnce();
  });

  it('reads the cart counter again when the order was confirmed before the page opened', () =>
  {
    vi.useFakeTimers();
    route.snapshot.queryParamMap = convertToParamMap({ placed: 'true' });
    fixture = TestBed.createComponent(OrderComponent);
    load({ ...order, status: 'Confirmed' });

    vi.advanceTimersByTime(cartRefreshDelay);

    expect(cartService.refreshItemCount).toHaveBeenCalledOnce();
  });

  it('does not ask again for an order that is already decided', () =>
  {
    vi.useFakeTimers();
    load({ ...order, status: 'Confirmed' });

    vi.advanceTimersByTime(statusPollInterval * 2);

    httpTesting.expectNone('/api/orders/7');
  });

  it('gives up after the limit and says it takes longer', () =>
  {
    vi.useFakeTimers();
    load(order);

    for (let i = 0; i < statusPollLimit; i++)
    {
      vi.advanceTimersByTime(statusPollInterval);
      httpTesting.expectOne('/api/orders/7').flush(order);
    }
    fixture.detectChanges();

    expect(text(fixture.nativeElement.querySelector('.status-message'))).toContain('takes longer than usual');
    vi.advanceTimersByTime(statusPollInterval);
    httpTesting.expectNone('/api/orders/7');
  });

  it('stops asking once the order is cancelled on the page', () =>
  {
    vi.useFakeTimers();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    load(order);

    cancelButton().click();
    httpTesting.expectOne('/api/orders/7/cancel').flush({ ...order, status: 'Cancelled' });
    vi.advanceTimersByTime(statusPollInterval);

    httpTesting.expectNone('/api/orders/7');
  });

  it('explains a rejected order and does not thank for it', () =>
  {
    route.snapshot.queryParamMap = convertToParamMap({ placed: 'true' });
    fixture = TestBed.createComponent(OrderComponent);

    load({ ...order, status: 'Rejected' });

    expect(text(fixture.nativeElement.querySelector('h4'))).not.toContain('Thank you');
    const note = fixture.nativeElement.querySelector('.status-note');
    expect(text(note)).toContain('could not be accepted');
    expect(note.querySelector('a').getAttribute('href')).toBe('/cart');
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
