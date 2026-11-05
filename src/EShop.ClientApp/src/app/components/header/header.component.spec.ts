import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { HeaderComponent } from './header.component';
import { AuthService } from '../../services/auth.service';

class FakeAuthService
{
  readonly isAuthenticated = signal(false);
  readonly userName = signal<string | undefined>(undefined);
  readonly isAdmin = signal(false);
  readonly signIn = vi.fn();
  readonly signUp = vi.fn();
  readonly signOut = vi.fn();
}

describe('HeaderComponent', () =>
{
  let fixture: ComponentFixture<HeaderComponent>;
  let auth: FakeAuthService;

  beforeEach(async () =>
  {
    auth = new FakeAuthService();
    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderComponent);
  });

  it('offers sign-in to an anonymous user', () =>
  {
    fixture.detectChanges();

    button('Sign in').click();

    expect(button('Sign out', false)).toBeUndefined();
    expect(auth.signIn).toHaveBeenCalledOnce();
  });

  it('offers sign-up to an anonymous user', () =>
  {
    fixture.detectChanges();

    button('Sign up').click();

    expect(auth.signUp).toHaveBeenCalledOnce();
  });

  it('links to the orders next to the cart, also for an anonymous user', () =>
  {
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a[aria-label="My orders"]') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/account/orders');
  });

  it('links a signed-in user to the account pages and hides sign-up', () =>
  {
    auth.isAuthenticated.set(true);
    fixture.detectChanges();

    const links = Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll('a.dropdown-item[href^="/account"]'));

    expect(links.map(link => link.getAttribute('href'))).toEqual(['/account', '/account/orders']);
    expect(button('Sign up', false)).toBeUndefined();
  });

  it('shows the user name and signs the user out', () =>
  {
    auth.isAuthenticated.set(true);
    auth.userName.set('customer');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('customer');
    expect(button('Sign in', false)).toBeUndefined();

    button('Sign out').click();

    expect(auth.signOut).toHaveBeenCalledOnce();
  });

  function button(label: string, required = true): HTMLButtonElement
  {
    const found = Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .find(b => b.textContent?.trim() === label);
    if (required && !found)
    {
      throw new Error(`No button "${label}"`);
    }
    return found!;
  }
});
