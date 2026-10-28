import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';

import { CheckoutComponent } from './checkout.component';
import { ICart } from '../../models/cart.model';
import { IOrder } from '../../models/order.model';
import { AuthService } from '../../services/auth.service';

const cartUrl = '/api/carts/me';
const ordersUrl = '/api/orders';

// Chai 18 € × 2 + Aniseed Syrup 10 € × 1 = 46 €; total with 5 € shipping = 51 €.
const cart: ICart = {
  cartId: 5,
  cartItems: [
    {
      productId: 7, productName: 'Chai', categoryName: 'Beverages', unitPrice: 18, quantity: 2,
      unitsInStock: 39, discontinued: false
    },
    {
      productId: 9, productName: 'Aniseed Syrup', categoryName: 'Condiments', unitPrice: 10, quantity: 1,
      unitsInStock: 13, discontinued: false
    },
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
  orderId: 7, orderDate: '2026-10-27T12:00:00Z', status: 'Pending', freight: 5, total: 51, items: [],
  ...address, shipRegion: null
};

describe('CheckoutComponent', () =>
{
  let fixture: ComponentFixture<CheckoutComponent>;
  let httpTesting: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutComponent);
    httpTesting = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => httpTesting.verify());

  it('shows the items, the shipping and the total', () =>
  {
    loadCart(cart);

    const page = text(fixture.nativeElement);
    expect(page).toContain('Chai × 2');
    expect(page).toContain('Aniseed Syrup × 1');
    expect(page).toMatch(/Total\s*€ 51\.00/);
  });

  it('keeps "Place order" disabled until the address is complete', () =>
  {
    loadCart(cart);
    expect(placeOrderButton().disabled).toBe(true);

    fillAddress();

    expect(placeOrderButton().disabled).toBe(false);
  });

  it('places the order with the address and opens the confirmation', async () =>
  {
    loadCart(cart);
    fillAddress();

    placeOrderButton().click();

    const req = httpTesting.expectOne(ordersUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ ...address, shipRegion: null });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(order, { status: 201, statusText: 'Created' });
    expect(navigate).toHaveBeenCalledWith(['/orders', 7]);
  });

  it('sends one request when the button is clicked twice', () =>
  {
    loadCart(cart);
    fillAddress();

    placeOrderButton().click();
    fixture.detectChanges();
    placeOrderButton().click();

    // expectOne fails if a second POST was sent.
    httpTesting.expectOne(ordersUrl).flush(order);
  });

  it('retries with the same idempotency key after an error', () =>
  {
    loadCart(cart);
    fillAddress();
    placeOrderButton().click();
    const first = httpTesting.expectOne(ordersUrl);
    first.flush('Server error', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(errorAlert()?.textContent).toContain('Could not place the order');

    placeOrderButton().click();

    const retry = httpTesting.expectOne(ordersUrl);
    expect(retry.request.headers.get('Idempotency-Key')).toBe(first.request.headers.get('Idempotency-Key'));
    retry.flush(order);
  });

  it('on 409 shows the message, reloads the cart and marks the item with its current stock', () =>
  {
    loadCart(cart);
    fillAddress();
    placeOrderButton().click();

    conflict(httpTesting.expectOne(ordersUrl), [7]);
    const reloaded = structuredClone(cart);
    reloaded.cartItems[0].unitsInStock = 1;
    httpTesting.expectOne(cartUrl).flush(reloaded);
    fixture.detectChanges();

    expect(errorAlert()?.textContent).toContain('no longer available in the requested quantity');
    expect(text(itemRows()[0])).toContain('Only 1 left');
    expect(placeOrderButton().disabled).toBe(true);
  });

  it('on 409 marks the items the API named even if the reloaded cart looks fine', () =>
  {
    loadCart(cart);
    fillAddress();
    placeOrderButton().click();

    conflict(httpTesting.expectOne(ordersUrl), [9]);
    httpTesting.expectOne(cartUrl).flush(structuredClone(cart));
    fixture.detectChanges();

    expect(text(itemRows()[1])).toContain('Not available in this quantity');
    expect(itemRows()[0].querySelector('.item-problem')).toBeNull();
  });

  it('marks a discontinued item from the start and does not allow ordering', () =>
  {
    const discontinued = structuredClone(cart);
    discontinued.cartItems[1].discontinued = true;
    loadCart(discontinued);
    fillAddress();

    expect(text(itemRows()[1])).toContain('No longer available');
    expect(placeOrderButton().disabled).toBe(true);
  });

  it('says the cart is empty and does not allow ordering', () =>
  {
    loadCart({ ...cart, cartItems: [] });
    fillAddress();

    expect(text(fixture.nativeElement)).toContain('Your cart is empty');
    expect(placeOrderButton().disabled).toBe(true);
  });

  function loadCart(value: ICart): void
  {
    fixture.detectChanges(); // runs ngOnInit
    httpTesting.expectOne(cartUrl).flush(structuredClone(value));
    fixture.detectChanges();
  }

  function fillAddress(): void
  {
    for (const [name, value] of Object.entries(address))
    {
      const input = fixture.nativeElement.querySelector(`#${name}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
  }

  function conflict(req: TestRequest, productIds: number[]): void
  {
    req.flush(
      { status: 409, title: 'Not enough units in stock', productIds },
      { status: 409, statusText: 'Conflict' }
    );
  }

  function placeOrderButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.place-order');
  }

  function itemRows(): HTMLElement[]
  {
    return Array.from(fixture.nativeElement.querySelectorAll('.order-item'));
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
