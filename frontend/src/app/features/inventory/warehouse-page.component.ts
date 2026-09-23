import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { Warehouse } from './inventory.models';
import { InventoryService, WarehouseInput } from './inventory.service';

@Component({
  selector: 'app-warehouse-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './warehouse-page.component.html',
  styleUrl: './warehouse-page.component.scss'
})
export class WarehousePageComponent implements OnInit {
  private readonly formBuilder = new FormBuilder().nonNullable;
  readonly form = this.formBuilder.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', Validators.maxLength(500)],
    addressLine1: ['', Validators.maxLength(200)],
    addressLine2: ['', Validators.maxLength(200)],
    city: ['', Validators.maxLength(100)],
    postalCode: ['', Validators.maxLength(30)],
    countryCode: ['', [Validators.minLength(2), Validators.maxLength(2)]]
  });

  warehouses: Warehouse[] = [];
  selectedWarehouse?: Warehouse;
  search = '';
  statusFilter = '';
  sortBy = '';
  sortDescending = false;
  pageNumber = 1;
  pageSize = 20;
  totalCount = 0;
  isLoading = true;
  isFormOpen = false;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  constructor(private readonly inventory: InventoryService, private readonly route: ActivatedRoute, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('inventory.manage') === true;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  applyFilters(): void { this.pageNumber = 1; this.load(); }

  clearFilters(): void {
    this.search = '';
    this.statusFilter = '';
    this.sortBy = '';
    this.sortDescending = false;
    this.applyFilters();
  }

  changePage(nextPage: number): void {
    if (nextPage < 1 || nextPage > this.pageCount || nextPage === this.pageNumber) return;
    this.pageNumber = nextPage;
    this.load();
  }

  openCreate(): void {
    this.selectedWarehouse = undefined;
    this.form.reset(this.emptyForm());
    this.errorMessage = '';
    this.isFormOpen = true;
  }

  openEdit(warehouse: Warehouse): void {
    this.selectedWarehouse = warehouse;
    this.form.reset({
      name: warehouse.name,
      description: warehouse.description ?? '',
      addressLine1: warehouse.addressLine1 ?? '',
      addressLine2: warehouse.addressLine2 ?? '',
      city: warehouse.city ?? '',
      postalCode: warehouse.postalCode ?? '',
      countryCode: warehouse.countryCode ?? ''
    });
    this.errorMessage = '';
    this.isFormOpen = true;
  }

  closeForm(): void { this.isFormOpen = false; this.selectedWarehouse = undefined; }

  save(): void {
    if (this.form.invalid || !this.canManage || this.hasIncompleteAddress()) {
      this.form.markAllAsTouched();
      this.errorMessage = this.hasIncompleteAddress() ? 'Provide both address line 1 and country code, or leave both blank.' : '';
      return;
    }
    this.isSaving = true;
    this.errorMessage = '';
    const input = this.toInput();
    const request = this.selectedWarehouse
      ? this.inventory.updateWarehouse(this.selectedWarehouse.id, input)
      : this.inventory.createWarehouse(input);
    request.subscribe({
      next: () => {
        this.successMessage = `Warehouse ${this.selectedWarehouse ? 'updated' : 'created'} successfully.`;
        this.isSaving = false;
        this.closeForm();
        this.load();
      },
      error: error => {
        this.errorMessage = error.error?.title ?? 'We could not save this warehouse.';
        this.isSaving = false;
      }
    });
  }

  setStatus(warehouse: Warehouse): void {
    if (!this.canManage) return;
    const isActive = !warehouse.isActive;
    const action = isActive ? 'activate' : 'deactivate';
    void this.confirmation.confirm({ title: `${action[0].toUpperCase()}${action.slice(1)} ${warehouse.name}?`, message: isActive ? 'This warehouse becomes available to new inventory activity.' : 'Existing history stays intact, but it cannot be selected for new activity.', confirmLabel: `${action[0].toUpperCase()}${action.slice(1)} warehouse`, tone: isActive ? 'primary' : 'warning' }).then(confirmed => {
      if (!confirmed) return;
      this.inventory.setWarehouseStatus(warehouse.id, isActive).subscribe({ next: () => { this.successMessage = `Warehouse ${action}d.`; this.feedback.success(this.successMessage); this.load(); }, error: error => { this.errorMessage = error.error?.title ?? 'We could not update the warehouse status.'; this.feedback.error(this.errorMessage); } });
    });
  }

  private load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.inventory.listWarehouses({
      search: this.search,
      isActive: this.statusFilter === '' ? undefined : this.statusFilter === 'active',
      sortBy: this.sortBy || undefined,
      sortDescending: this.sortDescending || undefined,
      pageNumber: this.pageNumber,
      pageSize: this.pageSize
    }).subscribe({
      next: result => {
        this.warehouses = result.items;
        this.totalCount = result.totalCount;
        this.isLoading = false;
        const editId = this.route.snapshot.queryParamMap.get('edit');
        const requestedWarehouse = editId ? result.items.find(item => item.id === editId) : undefined;
        if (requestedWarehouse && !this.isFormOpen) this.openEdit(requestedWarehouse);
      },
      error: () => { this.errorMessage = 'We could not load warehouses.'; this.isLoading = false; }
    });
  }

  private hasIncompleteAddress(): boolean {
    const value = this.form.getRawValue();
    return Boolean(value.addressLine1) !== Boolean(value.countryCode);
  }

  private toInput(): WarehouseInput {
    const value = this.form.getRawValue();
    return {
      name: value.name,
      description: value.description || null,
      addressLine1: value.addressLine1 || null,
      countryCode: value.countryCode ? value.countryCode.toUpperCase() : null,
      addressLine2: value.addressLine2 || null,
      city: value.city || null,
      postalCode: value.postalCode || null
    };
  }

  private emptyForm() {
    return { name: '', description: '', addressLine1: '', addressLine2: '', city: '', postalCode: '', countryCode: '' };
  }
}
