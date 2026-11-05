import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ICustomerProfile } from '../../models/customer-profile.model';
import { AuthService } from '../../services/auth.service';
import { CustomersService } from '../../services/customers.service';
import { AddressesComponent } from './addresses/addresses.component';

// The rule of UpdateProfileRequest in CustomerApi; the server decides, this only gives an early hint.
const phonePattern = /^\+?[0-9(][0-9 ()\-]{3,21}[0-9]$/;

@Component({
  selector: 'app-account',
  imports: [ReactiveFormsModule, RouterLink, AddressesComponent],
  templateUrl: './account.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountComponent implements OnInit
{
  private readonly customersService = inject(CustomersService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly auth = inject(AuthService);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    phone: ['', [Validators.maxLength(24), Validators.pattern(phonePattern)]],
  });
  protected readonly profile = signal<ICustomerProfile | null>(null);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void
  {
    this.loadProfile();
  }

  protected savePhone(): void
  {
    if (this.form.invalid || this.saving())
    {
      return;
    }
    this.saving.set(true);
    this.saved.set(false);
    this.errorMessage.set(null);
    this.customersService.updatePhone(this.form.getRawValue().phone.trim() || null).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: profile =>
      {
        this.profile.set(profile);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: () =>
      {
        this.saving.set(false);
        this.errorMessage.set('Could not save the phone.');
      }
    });
  }

  private loadProfile(): void
  {
    this.customersService.getProfile().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: profile =>
      {
        this.profile.set(profile);
        this.form.patchValue({ phone: profile.phone ?? '' });
      },
      error: () => this.errorMessage.set('Could not load your account.')
    });
  }
}
