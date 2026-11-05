import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AddressesComponent } from './addresses.component';
import { IAddress, maxAddresses } from '../../../models/address.model';

const addressesUrl = '/api/customers/me/addresses';

const home: IAddress = {
  addressId: 1,
  recipientName: 'Jane Doe',
  address: '1 Main St',
  city: 'Berlin',
  region: null,
  postalCode: '10115',
  country: 'Germany',
  isDefault: true
};
const work: IAddress = { ...home, addressId: 2, address: '2 Office Rd', city: 'Paris', region: 'IDF', isDefault: false };

describe('AddressesComponent', () =>
{
  let fixture: ComponentFixture<AddressesComponent>;
  let httpTesting: HttpTestingController;

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [AddressesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(AddressesComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() =>
  {
    httpTesting.verify();
    vi.restoreAllMocks();
  });

  it('lists the addresses and marks the default one', () =>
  {
    load([home, work]);

    expect(text(rows()[0])).toContain('Jane Doe');
    expect(text(rows()[1])).toContain('2 Office Rd');
    expect(rows()[0].querySelector('.address-default')).not.toBeNull();
    expect(rows()[1].querySelector('.address-default')).toBeNull();
  });

  it('says there are no addresses yet', () =>
  {
    load([]);

    expect(text(fixture.nativeElement)).toContain('No saved addresses yet');
  });

  it('adds an address with an empty region as null and reloads the list', () =>
  {
    load([]);
    click('.add-address');
    fill({ recipientName: 'Jane Doe', address: '1 Main St', city: 'Berlin', postalCode: '10115', country: 'Germany' });

    click('.save-address');

    const req = httpTesting.expectOne(addressesUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      recipientName: 'Jane Doe',
      address: '1 Main St',
      city: 'Berlin',
      region: null,
      postalCode: '10115',
      country: 'Germany',
      isDefault: false
    });
    req.flush(home, { status: 201, statusText: 'Created' });
    flushList([home]);

    expect(fixture.nativeElement.querySelector('.address-form')).toBeNull();
    expect(rows().length).toBe(1);
  });

  it('keeps the save button disabled while a required field is empty', () =>
  {
    load([]);
    click('.add-address');

    expect(saveButton().disabled).toBe(true);

    fill({ recipientName: 'Jane Doe', address: '1 Main St', city: 'Berlin', postalCode: '10115', country: 'Germany' });

    expect(saveButton().disabled).toBe(false);
  });

  it('fills the form with the address being edited and saves the change', () =>
  {
    load([home, work]);
    click('.edit-address', 1);
    expect(input('city').value).toBe('Paris');
    expect(input('region').value).toBe('IDF');
    fill({ city: 'Lyon' });

    click('.save-address');

    const req = httpTesting.expectOne(`${addressesUrl}/2`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.city).toBe('Lyon');
    req.flush({ ...work, city: 'Lyon' });
    flushList([home, { ...work, city: 'Lyon' }]);
    expect(text(rows()[1])).toContain('Lyon');
  });

  it('closes the form on cancel without saving', () =>
  {
    load([home]);
    click('.add-address');

    Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('.address-form button'))
      .find(button => button.textContent?.trim() === 'Cancel')?.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.address-form')).toBeNull();
  });

  it('makes an address the default one and reloads the list', () =>
  {
    load([home, work]);

    click('.make-default');

    const req = httpTesting.expectOne(`${addressesUrl}/2`);
    expect(req.request.method).toBe('PUT');
    const { addressId, ...expected } = work;
    expect(req.request.body).toEqual({ ...expected, isDefault: true });
    req.flush({ ...work, isDefault: true });
    flushList([{ ...work, isDefault: true }, { ...home, isDefault: false }]);
    expect(rows()[0].querySelector('.address-default')).not.toBeNull();
  });

  it('offers no "Make default" for the default address', () =>
  {
    load([home]);

    expect(fixture.nativeElement.querySelector('.make-default')).toBeNull();
  });

  it('deletes an address after confirmation and reloads the list', () =>
  {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    load([home, work]);

    click('.delete-address', 1);

    const req = httpTesting.expectOne(`${addressesUrl}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    flushList([home]);
    expect(rows().length).toBe(1);
  });

  it('does not delete when the user declines', () =>
  {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    load([home]);

    click('.delete-address');

    httpTesting.expectNone(`${addressesUrl}/1`);
  });

  it('on 409 shows the reason from the API and reloads the list', () =>
  {
    load([home, work]);

    click('.make-default');

    httpTesting.expectOne(`${addressesUrl}/2`).flush(
      { status: 409, title: 'The default address was changed by another request' },
      { status: 409, statusText: 'Conflict' }
    );
    flushList([home, work]);
    expect(text(fixture.nativeElement.querySelector('.alert-danger'))).toContain('changed by another request');
  });

  it('keeps the form open when saving failed', () =>
  {
    load([]);
    click('.add-address');
    fill({ recipientName: 'Jane Doe', address: '1 Main St', city: 'Berlin', postalCode: '10115', country: 'Germany' });
    click('.save-address');

    httpTesting.expectOne(addressesUrl).flush('Server error', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.address-form')).not.toBeNull();
    expect(text(fixture.nativeElement.querySelector('.alert-danger'))).toContain('Could not save the address');
  });

  it('offers no new address at the limit', () =>
  {
    load(Array.from({ length: maxAddresses }, (_, i) => ({ ...work, addressId: i + 1 })));

    expect(fixture.nativeElement.querySelector('.add-address')).toBeNull();
  });

  function load(addresses: IAddress[]): void
  {
    fixture.detectChanges(); // runs ngOnInit
    flushList(addresses);
  }

  function flushList(addresses: IAddress[]): void
  {
    httpTesting.expectOne(req => req.method === 'GET' && req.url === addressesUrl).flush(addresses);
    fixture.detectChanges();
  }

  function fill(values: Record<string, string>): void
  {
    for (const [name, value] of Object.entries(values))
    {
      input(name).value = value;
      input(name).dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
  }

  function click(selector: string, index = 0): void
  {
    fixture.nativeElement.querySelectorAll(selector)[index].click();
    fixture.detectChanges();
  }

  function input(name: string): HTMLInputElement
  {
    return fixture.nativeElement.querySelector(`#${name}`);
  }

  function saveButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.save-address');
  }

  function rows(): HTMLElement[]
  {
    return Array.from(fixture.nativeElement.querySelectorAll('.address-row'));
  }
});

// Text of an element with whitespace collapsed, so template line breaks do not matter.
function text(element: HTMLElement): string
{
  return (element.textContent ?? '').replace(/\s+/g, ' ');
}
