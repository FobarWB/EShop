import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { PageComponent } from './page.component';
import { routes } from '../../../app.routes';
import { AuthService } from '../../../services/auth.service';

describe('Product PageComponent', () =>
{
  let harness: RouterTestingHarness;
  let httpTesting: HttpTestingController;
  const auth = { isAdmin: signal(false), isAuthenticated: signal(false) };

  beforeEach(async () =>
  {
    auth.isAuthenticated.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // The real routes: the page reads productName and id from the URL /Chai/p7.
        provideRouter(routes),
        { provide: AuthService, useValue: auth }
      ]
    });
    httpTesting = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() =>
  {
    httpTesting.verify();
    localStorage.clear();
  });

  it('shows how many units are available', async () =>
  {
    await loadPage(39);

    expect(harness.routeNativeElement?.textContent).toContain('In stock: 39');
    expect(buyButton().disabled).toBe(false);
  });

  it('says a product is out of stock and disables Buy', async () =>
  {
    await loadPage(0);

    expect(harness.routeNativeElement?.textContent).toContain('Out of stock');
    expect(buyButton().disabled).toBe(true);
  });

  it('Buy adds one unit to the cart of a signed-in user', async () =>
  {
    auth.isAuthenticated.set(true);
    await loadPage(39);

    buyButton().click();

    const req = httpTesting.expectOne('/api/carts/me/items');
    expect(req.request.body).toEqual({ productId: 7, quantity: 1 });
    req.flush(null);
    httpTesting.expectOne('/api/carts/me').flush({ cartId: 5, cartItems: [] });
  });

  it('shows why Buy failed', async () =>
  {
    auth.isAuthenticated.set(true);
    await loadPage(39);

    buyButton().click();
    httpTesting.expectOne('/api/carts/me/items')
      .flush({ title: 'Not enough units in stock' }, { status: 409, statusText: 'Conflict' });
    harness.detectChanges();

    expect(harness.routeNativeElement?.textContent).toContain('No more available');
  });

  // The page validates the name in the URL with one request and loads the product with a second one.
  async function loadPage(unitsInStock: number): Promise<void>
  {
    const product = {
      productId: 7, productName: 'Chai', supplierId: null, categoryId: 1, quantityPerUnit: null, unitPrice: 18,
      unitsInStock, unitsOnOrder: null, reorderLevel: null, discontinued: false
    };
    await harness.navigateByUrl('/Chai/p7', PageComponent);
    httpTesting.expectOne('/api/products/7').flush(product);
    await Promise.resolve(); // the validation resolves a promise before the second request
    httpTesting.expectOne('/api/products/7').flush(product);
    harness.detectChanges();
  }

  function buyButton(): HTMLButtonElement
  {
    return harness.routeNativeElement!.querySelector('.btn-danger') as HTMLButtonElement;
  }
});
