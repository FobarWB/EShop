import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';

import { CheckoutComponent } from './checkout.component';
import { ICart } from '../../models/cart.model';
import { IOrder } from '../../models/order.model';
import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';

const guestUrl = '/api/orders/guest';

// The browser's cart of a visitor who is not signed in (LocalCartService is tested on its own).
const cart: ICart = {
  cartId: 0,
  cartItems: [
    {
      productId: 7, productName: 'Chai', categoryName: 'Beverages', unitPrice: 18, quantity: 2,
      unitsInStock: 39, discontinued: false
    }
  ]
};

const address = {
  shipName: 'Jane Doe',
  shipAddress: '1 Main St',
  shipCity: 'Berlin',
  shipPostalCode: '10115',
  shipCountry: 'Germany'
};

const order: IOrder = {
  orderId: 11088, orderDate: '2026-11-10T11:41:00Z', status: 'Pending', freight: 5, total: 41, items: [],
  ...address, shipRegion: null
};

describe('CheckoutComponent (guest)', () =>
{
  let fixture: ComponentFixture<CheckoutComponent>;
  let httpTesting: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  const auth = { isAuthenticated: signal(false), signIn: vi.fn() };
  const cartService = {
    getCart: vi.fn(() => of(structuredClone(cart))),
    clearCart: vi.fn(() => of(undefined)),
    refreshItemCount: vi.fn()
  };

  beforeEach(async () =>
  {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: CartService, useValue: cartService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutComponent);
    httpTesting = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.detectChanges(); // runs ngOnInit
  });

  // Also fails a test that asked for saved addresses: a guest has none, and the request would get 401.
  afterEach(() => httpTesting.verify());

  it('asks for an email and offers the sign-in', () =>
  {
    expect(input('email')).not.toBeNull();
    expect(text(fixture.nativeElement)).toContain('Already have an account?');
  });

  it('keeps "Place order" disabled until the email is valid', () =>
  {
    fillAddress();
    fill('email', 'not-an-email');
    expect(placeOrderButton().disabled).toBe(true);

    fill('email', 'jane@example.com');

    expect(placeOrderButton().disabled).toBe(false);
  });

  it('places the guest order with the email, the address and the cart items', () =>
  {
    fillGuest();

    placeOrderButton().click();

    const req = httpTesting.expectOne(guestUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      email: 'jane@example.com',
      address: { ...address, shipRegion: null },
      items: [{ productId: 7, quantity: 2 }]
    });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(order, { status: 201, statusText: 'Created' });
  });

  it('empties the browser cart and opens the confirmation with the order', () =>
  {
    fillGuest();
    placeOrderButton().click();

    httpTesting.expectOne(guestUrl).flush(order, { status: 201, statusText: 'Created' });

    expect(cartService.clearCart).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/order-placed'], { state: { order, email: 'jane@example.com' } });
  });

  it('asks to sign in when the email has an account, and keeps the cart', () =>
  {
    fillGuest();
    placeOrderButton().click();

    conflict(httpTesting.expectOne(guestUrl), { code: 'account-exists' });
    fixture.detectChanges();

    expect(errorAlert()?.textContent).toContain('An account with this email already exists');
    expect(cartService.clearCart).not.toHaveBeenCalled();
    signInButton().click();
    expect(auth.signIn).toHaveBeenCalledOnce();
  });

  it.each([
    { status: 429, statusText: 'Too Many Requests', message: 'Too many attempts' },
    { status: 503, statusText: 'Service Unavailable', message: 'temporarily unavailable' },
  ])('says what happened on $status', ({ status, statusText, message }) =>
  {
    fillGuest();
    placeOrderButton().click();

    httpTesting.expectOne(guestUrl).flush({ status }, { status, statusText });
    fixture.detectChanges();

    expect(errorAlert()?.textContent).toContain(message);
  });

  it('retries with the same idempotency key after an error', () =>
  {
    fillGuest();
    placeOrderButton().click();
    const first = httpTesting.expectOne(guestUrl);
    first.flush({ status: 503 }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    placeOrderButton().click();

    const retry = httpTesting.expectOne(guestUrl);
    expect(retry.request.headers.get('Idempotency-Key')).toBe(first.request.headers.get('Idempotency-Key'));
    retry.flush(order, { status: 201, statusText: 'Created' });
  });

  function fillGuest(): void
  {
    fill('email', 'jane@example.com');
    fillAddress();
  }

  function fillAddress(): void
  {
    for (const [name, value] of Object.entries(address))
    {
      fill(name, value);
    }
  }

  function fill(name: string, value: string): void
  {
    input(name).value = value;
    input(name).dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function conflict(req: TestRequest, body: object): void
  {
    req.flush({ status: 409, ...body }, { status: 409, statusText: 'Conflict' });
  }

  function input(name: string): HTMLInputElement
  {
    return fixture.nativeElement.querySelector(`#${name}`);
  }

  function placeOrderButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.place-order');
  }

  function signInButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.alert .sign-in');
  }

  function errorAlert(): HTMLElement | null
  {
    return fixture.nativeElement.querySelector('.alert-danger');
  }
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
