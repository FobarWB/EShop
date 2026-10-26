import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { HttpClient, provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { authInterceptor, provideAuth, StsConfigLoader, withAppInitializerAuthCheck } from 'angular-auth-oidc-client';
import { authConfigLoader } from './auth.config';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withXhr(), withInterceptors([authInterceptor()])),
    // Before the app starts: load /config.json and finish a sign-in that returned from Keycloak (?code=...).
    provideAuth(
      { loader: { provide: StsConfigLoader, useFactory: authConfigLoader, deps: [HttpClient] } },
      withAppInitializerAuthCheck()
    ),
  ]
};
