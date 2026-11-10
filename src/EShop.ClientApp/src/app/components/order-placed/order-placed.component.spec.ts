import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { OrderPlacedComponent } from './order-placed.component';
import { IOrderPlacedState } from '../../models/order-placed.model';

const placed: IOrderPlacedState = {
  email: 'jane@example.com',
  order: {
    orderId: 11088, orderDate: '2026-11-10T11:41:00Z', status: 'Pending', freight: 5, total: 41,
    items: [{ productId: 1, productName: 'Chai', unitPrice: 18, quantity: 2 }],
    shipName: 'Jane Doe', shipAddress: '1 Main St', shipCity: 'Berlin', shipRegion: null,
    shipPostalCode: '10115', shipCountry: 'Germany'
  }
};

describe('OrderPlacedComponent', () =>
{
  let harness: RouterTestingHarness;

  beforeEach(async () =>
  {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'order-placed', component: OrderPlacedComponent }])]
    });
    harness = await RouterTestingHarness.create();
  });

  it('shows the order and the email the checkout handed over', async () =>
  {
    // As the checkout navigates: the order in the navigation state.
    await TestBed.inject(Router).navigate(['/order-placed'], { state: placed });
    harness.detectChanges();

    const page = text(harness.routeNativeElement!);
    expect(page).toContain('Thank you for your order!');
    expect(page).toContain('Order #11088');
    expect(page).toContain('Chai × 2');
    expect(page).toMatch(/Total\s*€ 41\.00/);
    expect(page).toContain('We sent two emails to jane@example.com');
  });

  it('shows only a short note after a reload, when the state is gone', async () =>
  {
    await harness.navigateByUrl('/order-placed');

    const page = text(harness.routeNativeElement!);
    expect(page).toContain('Your order has been placed.');
    expect(page).not.toContain('Order #');
  });
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
