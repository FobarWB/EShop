import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { HomeComponent } from './home.component';
import { AuthService } from '../../services/auth.service';

describe('HomeComponent', () =>
{
  let fixture: ComponentFixture<HomeComponent>;
  let httpTesting: HttpTestingController;
  const auth = { isAdmin: signal(false) };

  beforeEach(async () =>
  {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('shows the edit and create buttons to an admin', () =>
  {
    auth.isAdmin.set(true);

    loadCategories();

    expect(buttonLabels()).toContain('Edit');
    expect(buttonLabels()).toContain('Add new category');
  });

  it('hides the edit and create buttons from other users', () =>
  {
    auth.isAdmin.set(false);

    loadCategories();

    expect(fixture.nativeElement.textContent).toContain('Beverages');
    expect(buttonLabels()).not.toContain('Edit');
    expect(buttonLabels()).not.toContain('Add new category');
  });

  function loadCategories(): void
  {
    fixture.detectChanges(); // runs ngOnInit
    httpTesting.expectOne('/api/categories')
      .flush([{ categoryId: 1, categoryName: 'Beverages', description: 'Soft drinks' }]);
    fixture.detectChanges();
  }

  function buttonLabels(): string[]
  {
    return Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .map(b => b.textContent?.trim() ?? '');
  }
});
