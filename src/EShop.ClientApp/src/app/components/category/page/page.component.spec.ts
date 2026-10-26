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
  const auth = { isAdmin: signal(false) };

  beforeEach(async () =>
  {
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

  // The page checks the category first and asks for its products only after that check has resolved.
  async function loadPage(): Promise<void>
  {
    await harness.navigateByUrl('/Beverages/c1', PageComponent);
    httpTesting.expectOne('/api/categories/1')
      .flush({ categoryId: 1, categoryName: 'Beverages', description: null, picture: null, products: [] });
    await harness.fixture.whenStable();
    httpTesting.expectOne('/api/products?categoryId=1').flush([{
      productId: 7, productName: 'Chai', supplierId: null, categoryId: 1, quantityPerUnit: null, unitPrice: 18,
      unitsInStock: 39, unitsOnOrder: null, reorderLevel: null, discontinued: false, category: null
    }]);
    harness.detectChanges();
  }

  function buttonLabels(): string[]
  {
    return Array.from<HTMLButtonElement>(harness.routeNativeElement?.querySelectorAll('button') ?? [])
      .map(b => b.textContent?.trim() ?? '');
  }
});
