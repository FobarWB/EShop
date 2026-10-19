import { NgClass } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ModalComponent } from '../../modal/modal.component';
import { ICategory } from '../../../models/category.model';
import { CategoryService } from '../../../services/categories.service';

@Component({
  selector: 'app-edit',
  imports: [ReactiveFormsModule, NgClass],
  templateUrl: './edit.component.html',
  styleUrl: './edit.component.css'
})
export class EditComponent
{
  protected category!: ICategory;
  protected categoryForm!: FormGroup;

  set data(newCategory: ICategory)
  {
    this.category = newCategory;
    this.categoryForm.patchValue(this.category);
  }

  constructor(
    private modalComponent: ModalComponent,
    private formBuilder: FormBuilder,
    private categoryService: CategoryService) { }

  ngOnInit(): void
  {
    this.categoryForm = this.formBuilder.group({
      categoryName: ['', [Validators.required, Validators.maxLength(15)]],
      description: [''],
      picture: [null]
    });
    this.categoryForm.statusChanges.subscribe(_ => this.modalComponent.disabled = this.categoryForm.invalid);
    this.modalComponent.disabled = this.categoryForm.invalid;
  }

  update()
  {
    const category = {
      categoryId: this.category.categoryId,
      categoryName: this.categoryForm.get('categoryName')?.value,
      description: this.categoryForm.get('description')?.value,
      picture: this.categoryForm.get('picture')?.value,
    }
    this.categoryService.updateCategory(this.category?.categoryId, category).subscribe();
  }

  create()
  {
    const category = {
      categoryName: this.categoryForm.get('categoryName')?.value,
      description: this.categoryForm.get('description')?.value,
      picture: this.categoryForm.get('picture')?.value,
    }
    this.categoryService.createCategory(category).subscribe();
  }

  delete()
  {
    if (this.category)
    {
      this.categoryService.deleteCategory(this.category.categoryId).subscribe();
      this.modalComponent.close();
    }
  }
}
