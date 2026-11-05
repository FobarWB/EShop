import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { PageComponent } from './page.component';
import { routes } from '../../../app.routes';
import { AuthService } from '../../../services/auth.service';

describe('Category PageComponent', () =>
{
  let harness: RouterTestingHarness;
  let httpTesting: HttpTestingController;
  const auth = { isAdmin: signal(false), isAuthenticated: signal(false) };

  beforeEach(async () =>
  {
    auth.isAuthenticated.set(false);
    // The price slider in the side bar watches its size; jsdom has no ResizeObserver.
    vi.stubGlobal('ResizeObserver', class { observe() { } unobserve() { } disconnect() { } });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // The real routes: the page reads categoryName and id from the URL /Beverages/c1.
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
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('shows the edit and create buttons to an admin', async () =>
  {
    auth.isAdmin.set(true);

    await loadPage();

    expect(buttonLabels()).toContain('Edit');
    expect(buttonLabels()).toContain('Add new product');
  });

  it('hides the edit and create buttons from other users', async () =>
  {
    auth.isAdmin.set(false);

    await loadPage();

    expect(harness.routeNativeElement?.textContent).toContain('Chai');
    expect(harness.routeNativeElement?.querySelector('.btn-cart')).not.toBeNull();
    expect(buttonLabels()).not.toContain('Edit');
    expect(buttonLabels()).not.toContain('Add new product');
  });

  it('says so when the category has more products than one page', async () =>
  {
    // Filters and sorting run in the browser on one page of 100 (#61): the rest must not vanish silently.
    await loadPage(250);

    expect(harness.routeNativeElement?.textContent).toContain('Showing the first 1 of 250 products');
  });

  it('shows no paging note when the whole category fits into the page', async () =>
  {
    await loadPage();

    expect(harness.routeNativeElement?.textContent).not.toContain('Showing the first');
  });

  it('shows how many units are available', async () =>
  {
    await loadPage();

    expect(harness.routeNativeElement?.textContent).toContain('In stock: 39');
  });

  it('says a product is out of stock and disables its cart button', async () =>
  {
    await loadPage(1, 0);

    expect(harness.routeNativeElement?.textContent).toContain('Out of stock');
    expect(cartButton().disabled).toBe(true);
  });

  it('keeps the cart button enabled while the product is in stock', async () =>
  {
    await loadPage();

    expect(cartButton().disabled).toBe(false);
  });

  it('says so on the card when the stock has run out', async () =>
  {
    auth.isAuthenticated.set(true);
    await loadPage();

    cartButton().click();
    httpTesting.expectOne('/api/carts/me/items')
      .flush({ title: 'Not enough units in stock' }, { status: 409, statusText: 'Conflict' });
    harness.detectChanges();

    expect(harness.routeNativeElement?.textContent).toContain('No more available');
  });

  it('says so on the card when adding fails', async () =>
  {
    auth.isAuthenticated.set(true);
    await loadPage();

    cartButton().click();
    httpTesting.expectOne('/api/carts/me/items').flush('Server error', { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(harness.routeNativeElement?.textContent).toContain('Could not add the item');
  });

  it('removes the message after a successful add', async () =>
  {
    auth.isAuthenticated.set(true);
    await loadPage();
    cartButton().click();
    httpTesting.expectOne('/api/carts/me/items').flush('Server error', { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    cartButton().click();
    httpTesting.expectOne('/api/carts/me/items').flush(null);
    httpTesting.expectOne('/api/carts/me').flush({ cartId: 5, cartItems: [] });
    harness.detectChanges();

    expect(harness.routeNativeElement?.textContent).not.toContain('Could not add the item');
  });

  it('refuses an anonymous add above the stock with the same message', async () =>
  {
    await loadPage(1, 1);

    cartButton().click();
    cartButton().click();
    harness.detectChanges();

    expect(harness.routeNativeElement?.textContent).toContain('No more available');
    expect(JSON.parse(localStorage.getItem('cart') ?? '{}').cartItems).toEqual([{ productId: 7, quantity: 1 }]);
  });

  function cartButton(): HTMLButtonElement
  {
    return harness.routeNativeElement!.querySelector('.btn-cart') as HTMLButtonElement;
  }

  // The page checks the category first and asks for its products only after that check has resolved.
  async function loadPage(totalCount = 1, unitsInStock = 39): Promise<void>
  {
    await harness.navigateByUrl('/Beverages/c1', PageComponent);
    httpTesting.expectOne('/api/categories/1')
      .flush({ categoryId: 1, categoryName: 'Beverages', description: null });
    await harness.fixture.whenStable();
    httpTesting.expectOne('/api/products?categoryId=1&pageSize=100').flush({
      items: [{
        productId: 7, productName: 'Chai', supplierId: null, categoryId: 1, quantityPerUnit: null, unitPrice: 18,
        unitsInStock, unitsOnOrder: null, reorderLevel: null, discontinued: false
      }],
      page: 1,
      pageSize: 100,
      totalCount
    });
    harness.detectChanges();
  }

  function buttonLabels(): string[]
  {
    return Array.from<HTMLButtonElement>(harness.routeNativeElement?.querySelectorAll('button') ?? [])
      .map(b => b.textContent?.trim() ?? '');
  }
});
