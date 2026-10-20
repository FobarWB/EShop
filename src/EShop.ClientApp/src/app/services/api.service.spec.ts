import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ApiService } from './api.service';

describe('ApiService', () =>
{
  let service: ApiService;
  let httpTesting: HttpTestingController;

  beforeEach(() =>
  {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ApiService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('get sends GET and returns the response body', () =>
  {
    let result: unknown;

    service.get('/items/1').subscribe(body => result = body);

    const req = httpTesting.expectOne('/items/1');
    expect(req.request.method).toBe('GET');
    req.flush({ id: 1 });
    expect(result).toEqual({ id: 1 });
  });

  it('post sends POST with the body', () =>
  {
    service.post('/items', { name: 'Chai' }).subscribe();

    const req = httpTesting.expectOne('/items');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Chai' });
    req.flush(null);
  });

  it('put sends PUT with the body', () =>
  {
    service.put('/items/1', 3).subscribe();

    const req = httpTesting.expectOne('/items/1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toBe(3);
    req.flush(null);
  });

  it('delete sends DELETE', () =>
  {
    service.delete('/items/1').subscribe();

    expect(httpTesting.expectOne('/items/1').request.method).toBe('DELETE');
  });
});
