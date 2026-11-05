import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AccountOrdersComponent } from './orders.component';
import { IOrderSummary } from '../../../models/order.model';
import { IPage } from '../../../models/page.model';

const summary: IOrderSummary = {
  orderId: 7,
  orderDate: '2026-10-27T12:00:00Z',
  status: 'Shipped',
  total: 41,
  itemCount: 2
};

function page(number: number, totalCount: number, items: IOrderSummary[] = [summary]): IPage<IOrderSummary>
{
  return { items, page: number, pageSize: 10, totalCount };
}

describe('AccountOrdersComponent', () =>
{
  let fixture: ComponentFixture<AccountOrdersComponent>;
  let httpTesting: HttpTestingController;

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [AccountOrdersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountOrdersComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('lists the orders with a link to each one', () =>
  {
    load(page(1, 1));

    const row = fixture.nativeElement.querySelector('.order-row') as HTMLElement;
    const cells = Array.from(row.querySelectorAll('td')).map(cell => text(cell).trim());
    expect(cells).toEqual(['#7', 'Oct 27, 2026', 'Shipped', '2', '€ 41.00']);
    expect(row.querySelector('a')?.getAttribute('href')).toBe('/orders/7');
  });

  it('says there are no orders yet', () =>
  {
    load(page(1, 0, []));

    expect(text(fixture.nativeElement)).toContain('You have no orders yet');
  });

  it('allows only the next page on the first of several', () =>
  {
    load(page(1, 15));

    expect(previousButton().disabled).toBe(true);
    expect(nextButton().disabled).toBe(false);
  });

  it('loads the next page and stops on the last one', () =>
  {
    load(page(1, 15));

    nextButton().click();
    httpTesting.expectOne('/api/orders?page=2&pageSize=10').flush(page(2, 15));
    fixture.detectChanges();

    expect(previousButton().disabled).toBe(false);
    expect(nextButton().disabled).toBe(true);
  });

  it('allows no paging when everything fits one page', () =>
  {
    load(page(1, 10));

    expect(previousButton().disabled).toBe(true);
    expect(nextButton().disabled).toBe(true);
  });

  it('says so when the orders could not be loaded', () =>
  {
    fixture.detectChanges();

    httpTesting.expectOne('/api/orders?page=1&pageSize=10')
      .flush('Server error', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text(fixture.nativeElement.querySelector('.alert-danger'))).toContain('Could not load your orders');
  });

  function load(value: IPage<IOrderSummary>): void
  {
    fixture.detectChanges(); // runs ngOnInit
    httpTesting.expectOne('/api/orders?page=1&pageSize=10').flush(value);
    fixture.detectChanges();
  }

  function previousButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.previous-page');
  }

  function nextButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.next-page');
  }
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
