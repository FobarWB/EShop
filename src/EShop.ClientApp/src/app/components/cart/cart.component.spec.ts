import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { CartComponent } from './cart.component';
import { ICart } from '../../models/cart.model';

const cartUrl = 'https://localhost:5004/api/carts/1';
const productsUrl = 'https://localhost:5002/api/products';
const categoriesUrl = 'https://localhost:5003/api/categories';

// Chai 18 € × 2 + Aniseed Syrup 10 € × 1 = 46 €; total with 5 € shipping = 51 €.
const cart: ICart = {
  cartId: 5,
  userId: '1',
  createdAt: new Date(),
  cartItems: [
    { cartId: 5, productId: 7, quantity: 2 },
    { cartId: 5, productId: 9, quantity: 1 },
  ]
};

describe('CartComponent', () =>
{
  let fixture: ComponentFixture<CartComponent>;
  let httpTesting: HttpTestingController;

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [CartComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(CartComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('shows each item with its product and category', () =>
  {
    loadCart(cart);

    const rows = itemRows();
    expect(rows).toHaveLength(2);
    expect(text(rows[0])).toContain('Beverages');
    expect(text(rows[0])).toContain('Chai');
    expect(text(rows[1])).toContain('Condiments');
    expect(text(rows[1])).toContain('Aniseed Syrup');
  });

  it('shows the item count, the subtotal and the total with shipping', () =>
  {
    loadCart(cart);

    const page = text(fixture.nativeElement);
    expect(page).toContain('2 items');
    expect(page).toContain('ITEMS 2 € 46');
    expect(page).toContain('TOTAL PRICE € 51');
  });

  it('shows an empty cart without loading any products', () =>
  {
    fixture.detectChanges();
    httpTesting.expectOne(cartUrl).flush({ ...cart, cartItems: [] });
    fixture.detectChanges();

    expect(itemRows()).toHaveLength(0);
    expect(text(fixture.nativeElement)).toContain('0 items');
  });

  it('disables "-" when the quantity is 1', () =>
  {
    loadCart(cart);

    expect(minusButton(1).disabled).toBe(true);
    expect(minusButton(0).disabled).toBe(false);
  });

  it('"+" sends the new quantity', () =>
  {
    loadCart(cart);

    plusButton(0).click();

    const req = httpTesting.expectOne(`${cartUrl}/update/7`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toBe(3);
    req.flush(null);
  });

  it('the remove button sends DELETE for that item', () =>
  {
    loadCart(cart);

    removeButton(0).click();

    expect(httpTesting.expectOne(`${cartUrl}/remove/7`).request.method).toBe('DELETE');
  });

  // #26: the request succeeds, but the page keeps showing the old state until it is reloaded.
  it.skip('"+" updates the quantity and totals on the page', () =>
  {
    loadCart(cart);

    plusButton(0).click();
    httpTesting.expectOne(`${cartUrl}/update/7`).flush(null);
    fixture.detectChanges();

    expect(text(fixture.nativeElement)).toContain('ITEMS 2 € 64'); // 18 × 3 + 10
  });

  // #26
  it.skip('"-" updates the quantity and totals on the page', () =>
  {
    loadCart(cart);

    minusButton(0).click();
    httpTesting.expectOne(`${cartUrl}/update/7`).flush(null);
    fixture.detectChanges();

    expect(text(fixture.nativeElement)).toContain('ITEMS 2 € 28'); // 18 × 1 + 10
  });

  // #26
  it.skip('removing an item removes it from the page', () =>
  {
    loadCart(cart);

    removeButton(0).click();
    httpTesting.expectOne(`${cartUrl}/remove/7`).flush(null);
    fixture.detectChanges();

    expect(itemRows()).toHaveLength(1);
    expect(text(fixture.nativeElement)).toContain('1 items');
  });

  // #26: clearCurrentCart() reloads the whole page instead of updating it.
  it.skip('clearing the cart empties the page without a reload', () =>
  {
    loadCart(cart);

    trashButton().click();
    httpTesting.expectOne(`${cartUrl}/clear`).flush(null);
    fixture.detectChanges();

    expect(itemRows()).toHaveLength(0);
  });

  // Answers the requests the page makes on load: the cart, then each product and its category.
  function loadCart(value: ICart): void
  {
    fixture.detectChanges(); // runs ngOnInit
    httpTesting.expectOne(cartUrl).flush(value);
    httpTesting.expectOne(`${productsUrl}/7`).flush({ productId: 7, productName: 'Chai', unitPrice: 18, categoryId: 1 });
    httpTesting.expectOne(`${productsUrl}/9`).flush({ productId: 9, productName: 'Aniseed Syrup', unitPrice: 10, categoryId: 2 });
    httpTesting.expectOne(`${categoriesUrl}/1`).flush({ categoryId: 1, categoryName: 'Beverages' });
    httpTesting.expectOne(`${categoriesUrl}/2`).flush({ categoryId: 2, categoryName: 'Condiments' });
    fixture.detectChanges();
  }

  function itemRows(): HTMLElement[]
  {
    return Array.from(fixture.nativeElement.querySelectorAll('.row.main'));
  }

  function minusButton(row: number): HTMLButtonElement
  {
    return itemRows()[row].querySelectorAll<HTMLButtonElement>('.cart-counter-button')[0];
  }

  function plusButton(row: number): HTMLButtonElement
  {
    return itemRows()[row].querySelectorAll<HTMLButtonElement>('.cart-counter-button')[1];
  }

  function removeButton(row: number): HTMLButtonElement
  {
    return itemRows()[row].querySelector<HTMLButtonElement>('.btn-close')!;
  }

  function trashButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.btn-trash');
  }
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
