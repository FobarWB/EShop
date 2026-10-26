import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthenticatedResult, OidcSecurityService, UserDataResult } from 'angular-auth-oidc-client';
import { BehaviorSubject, of } from 'rxjs';

import { AuthService } from './auth.service';

// Replaces the library: the tests drive the sign-in state and the access token payload directly.
class FakeOidcSecurityService
{
  readonly authenticated = signal<AuthenticatedResult>({ isAuthenticated: false, allConfigsAuthenticated: [] });
  readonly userData = signal<UserDataResult>({ userData: null, allUserData: [] });
  readonly isAuthenticated$ = new BehaviorSubject<AuthenticatedResult>(this.authenticated());
  accessTokenPayload: unknown = null;

  readonly authorize = vi.fn();
  readonly logoff = vi.fn(() => of(null));

  getPayloadFromAccessToken()
  {
    return of(this.accessTokenPayload);
  }

  signIn(userName: string, roles: string[])
  {
    const result = { isAuthenticated: true, allConfigsAuthenticated: [] };
    this.accessTokenPayload = { realm_access: { roles } };
    this.userData.set({ userData: { preferred_username: userName }, allUserData: [] });
    this.authenticated.set(result);
    this.isAuthenticated$.next(result);
  }
}

describe('AuthService', () =>
{
  let service: AuthService;
  let oidc: FakeOidcSecurityService;

  beforeEach(() =>
  {
    oidc = new FakeOidcSecurityService();
    TestBed.configureTestingModule({
      providers: [{ provide: OidcSecurityService, useValue: oidc }]
    });
    service = TestBed.inject(AuthService);
  });

  it('is not authenticated and not an admin before sign-in', () =>
  {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.userName()).toBeUndefined();
    expect(service.isAdmin()).toBe(false);
  });

  it('takes the user name from preferred_username', () =>
  {
    oidc.signIn('customer', ['customer']);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.userName()).toBe('customer');
  });

  it('is an admin when the access token has the admin role', () =>
  {
    oidc.signIn('admin', ['admin', 'customer']);

    expect(service.isAdmin()).toBe(true);
  });

  it('is not an admin without the admin role', () =>
  {
    oidc.signIn('customer', ['customer']);

    expect(service.isAdmin()).toBe(false);
  });

  it('starts the sign-in at the identity provider', () =>
  {
    service.signIn();

    expect(oidc.authorize).toHaveBeenCalledOnce();
  });

  it('signs out at the identity provider', () =>
  {
    service.signOut();

    expect(oidc.logoff).toHaveBeenCalledOnce();
  });
});
