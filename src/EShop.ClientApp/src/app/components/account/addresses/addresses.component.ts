import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { IAddress, maxAddresses } from '../../../models/address.model';
import { CustomersService } from '../../../services/customers.service';

// The form is closed (null), adds a new address (0) or edits the address with this id.
const newAddress = 0;

@Component({
  selector: 'app-addresses',
  imports: [ReactiveFormsModule],
  templateUrl: './addresses.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AddressesComponent implements OnInit
{
  private readonly customersService = inject(CustomersService);
  private readonly destroyRef = inject(DestroyRef);

  // The lengths of AddressRequest in CustomerApi.
  protected readonly fields = [
    { name: 'recipientName', label: 'Name', maxLength: 40 },
    { name: 'address', label: 'Address', maxLength: 60 },
    { name: 'city', label: 'City', maxLength: 15 },
    { name: 'region', label: 'Region (optional)', maxLength: 15 },
    { name: 'postalCode', label: 'Postal code', maxLength: 10 },
    { name: 'country', label: 'Country', maxLength: 15 },
  ] as const;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    recipientName: ['', [Validators.required, Validators.maxLength(40)]],
    address: ['', [Validators.required, Validators.maxLength(60)]],
    city: ['', [Validators.required, Validators.maxLength(15)]],
    region: ['', [Validators.maxLength(15)]],
    postalCode: ['', [Validators.required, Validators.maxLength(10)]],
    country: ['', [Validators.required, Validators.maxLength(15)]],
    isDefault: [false],
  });

  protected readonly addresses = signal<IAddress[]>([]);
  protected readonly editing = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly canAdd = computed(() => this.addresses().length < maxAddresses);

  ngOnInit(): void
  {
    this.loadAddresses();
  }

  protected startAdding(): void
  {
    this.form.reset();
    this.errorMessage.set(null);
    this.editing.set(newAddress);
  }

  protected startEditing(address: IAddress): void
  {
    this.form.reset({ ...address, region: address.region ?? '' });
    this.errorMessage.set(null);
    this.editing.set(address.addressId);
  }

  protected cancelEditing(): void
  {
    this.editing.set(null);
  }

  protected saveAddress(): void
  {
    const id = this.editing();
    if (id === null || this.form.invalid || this.saving())
    {
      return;
    }
    const value = this.form.getRawValue();
    const request = { ...value, region: value.region || null };
    this.saving.set(true);
    this.errorMessage.set(null);
    const save = id === newAddress
      ? this.customersService.addAddress(request)
      : this.customersService.updateAddress(id, request);
    save.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () =>
      {
        this.saving.set(false);
        this.editing.set(null);
        this.loadAddresses();
      },
      error: (error: HttpErrorResponse) =>
      {
        this.saving.set(false);
        this.showError(error, 'Could not save the address.');
      }
    });
  }

  protected makeDefault(address: IAddress): void
  {
    const { addressId, ...request } = address;
    this.errorMessage.set(null);
    this.customersService.updateAddress(addressId, { ...request, isDefault: true }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      // Reloaded: the previous default lost its flag on the server.
      next: () => this.loadAddresses(),
      error: (error: HttpErrorResponse) => this.showError(error, 'Could not change the default address.')
    });
  }

  protected deleteAddress(address: IAddress): void
  {
    if (!confirm(`Delete the address in ${address.city}?`))
    {
      return;
    }
    this.errorMessage.set(null);
    this.customersService.deleteAddress(address.addressId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.loadAddresses(),
      error: (error: HttpErrorResponse) => this.showError(error, 'Could not delete the address.')
    });
  }

  private loadAddresses(): void
  {
    this.customersService.getAddresses().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: addresses => this.addresses.set(addresses),
      error: () => this.errorMessage.set('Could not load your addresses.')
    });
  }

  // 409: the limit is reached or another tab changed the default; the API's title says which.
  private showError(error: HttpErrorResponse, fallback: string): void
  {
    this.errorMessage.set(error.status === 409 ? error.error?.title ?? fallback : fallback);
    if (error.status === 409)
    {
      this.loadAddresses();
    }
  }
}
