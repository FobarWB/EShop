import { computed, inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { map, of, switchMap } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AuthService
{
  private readonly oidc = inject(OidcSecurityService);

  readonly isAuthenticated = computed(() => this.oidc.authenticated().isAuthenticated);
  readonly userName = computed(() => this.oidc.userData().userData?.preferred_username as string | undefined);

  // Roles are read from the access token (realm_access.roles), as the APIs do. Hiding a button is only
  // for convenience: the APIs refuse a write without the admin role anyway.
  private readonly roles = toSignal(
    this.oidc.isAuthenticated$.pipe(
      switchMap(result => result.isAuthenticated ? this.oidc.getPayloadFromAccessToken() : of(null)),
      map(payload => (payload?.realm_access?.roles ?? []) as string[])
    ),
    { initialValue: [] }
  );
  readonly isAdmin = computed(() => this.roles().includes('admin'));

  signIn(): void
  {
    this.oidc.authorize();
  }

  signOut(): void
  {
    // Ends the Keycloak session too, then Keycloak sends the browser back to postLogoutRedirectUri.
    this.oidc.logoff().subscribe();
  }
}
