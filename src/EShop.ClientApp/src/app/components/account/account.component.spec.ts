import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AccountComponent } from './account.component';
import { ICustomerProfile } from '../../models/customer-profile.model';
import { AuthService } from '../../services/auth.service';

const profileUrl = '/api/customers/me';
const addressesUrl = '/api/customers/me/addresses';
const accountUrl = 'http://keycloak.test/realms/eshop/account';

const profile: ICustomerProfile = {
  email: 'jane@example.com',
  firstName: 'Jane',
  lastName: 'Doe',
  phone: '+49 30 1234567'
};

describe('AccountComponent', () =>
{
  let fixture: ComponentFixture<AccountComponent>;
  let httpTesting: HttpTestingController;

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [AccountComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { accountUrl: signal(accountUrl) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('shows the name, the email and the phone', () =>
  {
    load();

    expect(text('.account-name')).toBe('Jane Doe');
    expect(text('.account-email')).toBe('jane@example.com');
    expect(phoneInput().value).toBe('+49 30 1234567');
  });

  it('links to the account console for what Keycloak owns', () =>
  {
    load();

    const link = fixture.nativeElement.querySelector('.account-console') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe(accountUrl);
  });

  it('saves the phone and says so', () =>
  {
    load();
    typePhone('+380 (44) 123-45-67');

    saveButton().click();

    const req = httpTesting.expectOne(profileUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ phone: '+380 (44) 123-45-67' });
    req.flush({ ...profile, phone: '+380 (44) 123-45-67' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.phone-saved')).not.toBeNull();
  });

  it('sends null when the phone is cleared', () =>
  {
    load();
    typePhone('');

    saveButton().click();

    const req = httpTesting.expectOne(profileUrl);
    expect(req.request.body).toEqual({ phone: null });
    req.flush({ ...profile, phone: null });
  });

  it('does not allow saving an invalid phone', () =>
  {
    load();

    typePhone('call me');

    expect(saveButton().disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('.phone-error')).not.toBeNull();
  });

  it('says so when the phone could not be saved', () =>
  {
    load();
    typePhone('+49 30 7654321');
    saveButton().click();

    httpTesting.expectOne(profileUrl).flush('Server error', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text('.alert-danger')).toContain('Could not save the phone');
    expect(fixture.nativeElement.querySelector('.phone-saved')).toBeNull();
  });

  it('says so when the account could not be loaded', () =>
  {
    fixture.detectChanges();

    httpTesting.expectOne(profileUrl).flush('Server error', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text('.alert-danger')).toContain('Could not load your account');
  });

  function load(): void
  {
    fixture.detectChanges(); // runs ngOnInit
    httpTesting.expectOne(profileUrl).flush(profile);
    fixture.detectChanges(); // renders the addresses, which load their list
    httpTesting.expectOne(addressesUrl).flush([]);
    fixture.detectChanges();
  }

  function typePhone(value: string): void
  {
    phoneInput().value = value;
    phoneInput().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function phoneInput(): HTMLInputElement
  {
    return fixture.nativeElement.querySelector('#phone');
  }

  function saveButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('.save-phone');
  }

  function text(selector: string): string
  {
    return (fixture.nativeElement.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }
});
