import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { HeaderComponent } from './header.component';
import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';

class FakeCartService
{
  readonly itemCount = signal(0);
  readonly refreshItemCount = vi.fn();
}

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
  let cart: FakeCartService;

  beforeEach(async () =>
  {
    auth = new FakeAuthService();
    cart = new FakeCartService();
    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: CartService, useValue: cart }
      ]
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

  it('hides the cart counter while the cart is empty', () =>
  {
    fixture.detectChanges();

    expect(cartCount()).toBeNull();
    expect(cartButton().getAttribute('aria-label')).toBe('Cart');
  });

  it('shows the number of units on the cart button and updates it', () =>
  {
    cart.itemCount.set(3);
    fixture.detectChanges();
    expect(cartCount()?.textContent?.trim()).toBe('3');

    cart.itemCount.set(4);
    fixture.detectChanges();

    expect(cartCount()?.textContent?.trim()).toBe('4');
    expect(cartButton().getAttribute('aria-label')).toBe('Cart, 4 items');
  });

  it('shortens a large number', () =>
  {
    cart.itemCount.set(100);
    fixture.detectChanges();

    expect(cartCount()?.textContent?.trim()).toBe('99+');
    expect(cartButton().getAttribute('aria-label')).toBe('Cart, 100 items');
  });

  it('loads the counter at start and again after the sign-in', () =>
  {
    fixture.detectChanges();
    expect(cart.refreshItemCount).toHaveBeenCalledOnce();

    auth.isAuthenticated.set(true);
    fixture.detectChanges();

    expect(cart.refreshItemCount).toHaveBeenCalledTimes(2);
  });

  function cartButton(): HTMLButtonElement
  {
    return fixture.nativeElement.querySelector('button.btn-cart');
  }

  function cartCount(): HTMLElement | null
  {
    return fixture.nativeElement.querySelector('.cart-count');
  }

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
