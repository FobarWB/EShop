import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OpenIdConfiguration } from 'angular-auth-oidc-client';

import { authConfigLoader } from './auth.config';

describe('authConfigLoader', () =>
{
  let httpTesting: HttpTestingController;

  beforeEach(() =>
  {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('combines /config.json with the defaults', () =>
  {
    let configs: OpenIdConfiguration[] | undefined;

    authConfigLoader(TestBed.inject(HttpClient)).loadConfigs().subscribe(c => configs = c);
    httpTesting.expectOne('/config.json').flush({ authority: 'https://sso.example/realms/eshop', clientId: 'shop' });

    expect(configs).toHaveLength(1);
    expect(configs![0]).toMatchObject({
      authority: 'https://sso.example/realms/eshop',
      clientId: 'shop',
      responseType: 'code',
      useRefreshToken: true,
      secureRoutes: ['/api/'],
    });
  });
});
