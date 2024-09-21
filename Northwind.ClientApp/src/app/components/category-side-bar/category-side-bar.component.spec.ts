import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CategorySideBarComponent } from './category-side-bar.component';

describe('CategorySideBarComponent', () => {
  let component: CategorySideBarComponent;
  let fixture: ComponentFixture<CategorySideBarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CategorySideBarComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CategorySideBarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
