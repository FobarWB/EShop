import { HttpClient } from '@angular/common/http';
import { LogLevel, OpenIdConfiguration, StsConfigHttpLoader } from 'angular-auth-oidc-client';
import { map } from 'rxjs';
import { IClientConfig } from './models/client-config.model';

// The same in every environment; authority and clientId come from /config.json (public/config.json in the build;
// another environment mounts its own file over it), so one build of the client runs everywhere.
const defaults: OpenIdConfiguration = {
  redirectUrl: window.location.origin,
  postLogoutRedirectUri: window.location.origin,
  scope: 'openid profile email',
  // Authorization Code flow with PKCE: the browser never sees a client secret.
  responseType: 'code',
  // A refresh token renews the short-lived (5 min) access token without the login page.
  silentRenew: true,
  useRefreshToken: true,
  renewTimeBeforeTokenExpiresInSeconds: 30,
  // Keycloak does not repeat the nonce in an ID token received with a refresh token.
  ignoreNonceAfterRefresh: true,
  // The token is added only to our APIs: never to Keycloak itself or to any other host.
  secureRoutes: ['/api/'],
  logLevel: LogLevel.Warn,
};

export const authConfigLoader = (http: HttpClient) =>
  new StsConfigHttpLoader(
    http.get<IClientConfig>('/config.json').pipe(map(config => ({ ...defaults, ...config })))
  );
