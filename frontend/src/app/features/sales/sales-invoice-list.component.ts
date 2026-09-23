import { formatAmount, utcDateInput } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { Warehouse } from '../inventory/inventory.models';
import { InventoryService } from '../inventory/inventory.service';
import { Partner, Product } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { SalesInvoice, SalesInvoiceInput, SalesInvoiceLine, SalesInvoiceStatus } from './sales.models';
import { SalesInvoiceQuery, SalesService } from './sales.service';

@Component({
  selector: 'app-sales-invoice-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './sales-invoice-list.component.html',
  styleUrl: './sales-invoice-list.component.scss'
})
export class SalesInvoiceListComponent implements OnInit {
  private readonly formBuilder = new FormBuilder().nonNullable;

  readonly form = this.formBuilder.group({
    customerId: ['', Validators.required],
    warehouseId: [''],
    invoiceDate: [this.today(), Validators.required],
    dueDate: [''],
    reference: ['', Validators.maxLength(100)],
    notes: ['', Validators.maxLength(1000)],
    lines: this.formBuilder.array([this.createLine()])
  });

  invoices: SalesInvoice[] = [];
  customers: Partner[] = [];
  products: Product[] = [];
  warehouses: Warehouse[] = [];
  search = '';
  customerFilter = '';
  statusFilter: SalesInvoiceStatus | '' = '';
  fromDate = '';
  toDate = '';
  sortBy: SalesInvoiceQuery['sortBy'] = 'invoiceDate';
  sortDescending = true;
  pageNumber = 1;
  pageSize = 20;
  totalCount = 0;
  isLoading = true;
  isReferencesLoading = true;
  isSaving = false;
  isFormOpen = false;
  errorMessage = '';
  formErrorMessage = '';
  successMessage = '';
  editingInvoice?: SalesInvoice;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly sales: SalesService,
    private readonly masterData: MasterDataService,
    private readonly inventory: InventoryService,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadReferences();
    this.route.queryParamMap.subscribe((parameters) => {
      const editId = parameters.get('edit');
      if (editId && this.canManage) this.loadForEdit(editId);
    });
  }

  get lines(): FormArray {
    return this.form.controls.lines;
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('sales.manage') === true;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get draftTotalPreview(): number {
    return this.lines.controls.reduce((total, control) => {
      const line = control.getRawValue() as { quantity: number; unitPrice: number; discountPercentage: number };
      const subtotal = Number(line.quantity || 0) * Number(line.unitPrice || 0);
      return total + subtotal * (1 - Number(line.discountPercentage || 0) / 100);
    }, 0);
  }

  applyFilters(): void {
    this.pageNumber = 1;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.customerFilter = '';
    this.statusFilter = '';
    this.fromDate = '';
    this.toDate = '';
    this.sortBy = 'invoiceDate';
    this.sortDescending = true;
    this.applyFilters();
  }

  changePage(nextPage: number): void {
    if (nextPage < 1 || nextPage > this.pageCount || nextPage === this.pageNumber) return;
    this.pageNumber = nextPage;
    this.load();
  }

  openCreate(): void {
    if (!this.canManage) return;
    this.editingInvoice = undefined;
    this.resetForm();
    this.isFormOpen = true;
    this.formErrorMessage = '';
    void this.router.navigate([], { relativeTo: this.route, queryParams: { edit: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  closeForm(): void {
    this.isFormOpen = false;
    this.editingInvoice = undefined;
    this.formErrorMessage = '';
    void this.router.navigate([], { relativeTo: this.route, queryParams: { edit: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  addLine(): void {
    this.lines.push(this.createLine());
  }

  removeLine(index: number): void {
    if (this.lines.length > 1) this.lines.removeAt(index);
  }

  productForLine(index: number): Product | undefined {
    const productId = this.lines.at(index).get('productId')?.value as string;
    return this.products.find((product) => product.id === productId);
  }

  onProductSelected(index: number): void {
    const product = this.productForLine(index);
    if (product) this.lines.at(index).patchValue({ description: product.name });
  }

  saveDraft(): void {
    if (!this.canManage || this.form.invalid || this.isSaving) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.formErrorMessage = '';
    const body = this.toRequest();
    const request = this.editingInvoice
      ? this.sales.updateDraft(this.editingInvoice.id, body)
      : this.sales.createDraft(body);

    request.subscribe({
      next: (invoice) => {
        this.isSaving = false;
        this.successMessage = this.editingInvoice ? 'Draft invoice updated.' : 'Draft invoice created.';
        void this.router.navigate(['/sales', invoice.id]);
      },
      error: (error: { error?: { title?: string }; status?: number }) => {
        this.isSaving = false;
        this.formErrorMessage = error.error?.title ?? (error.status === 409 ? 'This invoice is no longer available for editing.' : 'We could not save this draft.');
      }
    });
  }

  trackById(_: number, invoice: SalesInvoice): string {
    return invoice.id;
  }

  formatAmount(amount: number, currency: string): string {
    return `${currency} ${this.formatNumber(amount)}`;
  }

  formatNumber(amount: number): string {
    return formatAmount(amount);
  }

  statusClass(status: SalesInvoiceStatus): string {
    return status.toLowerCase();
  }

  private load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.sales.listInvoices({
      search: this.search,
      customerId: this.customerFilter,
      status: this.statusFilter,
      fromDate: this.fromDate,
      toDate: this.toDate,
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      sortBy: this.sortBy,
      sortDescending: this.sortDescending
    }).subscribe({
      next: (result) => {
        this.invoices = result.items;
        this.totalCount = result.totalCount;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'We could not load sales invoices.';
        this.isLoading = false;
      }
    });
  }

  private loadReferences(): void {
    this.masterData.list<Partner>('customers', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({
      next: (result) => this.customers = result.items,
      error: () => this.formErrorMessage = 'Customers could not be loaded for invoice entry.'
    });
    this.masterData.list<Product>('products', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({
      next: (result) => this.products = result.items,
      error: () => this.formErrorMessage = 'Products could not be loaded for invoice entry.'
    });
    this.inventory.listWarehouses({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({
      next: (result) => {
        this.warehouses = result.items;
        this.isReferencesLoading = false;
      },
      error: () => {
        this.isReferencesLoading = false;
        this.formErrorMessage = 'Warehouses could not be loaded for invoice entry.';
      }
    });
  }

  private loadForEdit(id: string): void {
    this.sales.getInvoice(id).subscribe({
      next: (invoice) => {
        if (invoice.status !== 'Draft') {
          this.formErrorMessage = 'Only draft invoices can be edited.';
          return;
        }
        this.editingInvoice = invoice;
        this.form.patchValue({
          customerId: invoice.customerId,
          warehouseId: invoice.warehouseId ?? '',
          invoiceDate: invoice.invoiceDate,
          dueDate: invoice.dueDate ?? '',
          reference: invoice.reference ?? '',
          notes: invoice.notes ?? ''
        });
        this.lines.clear();
        invoice.lines.forEach((line) => this.lines.push(this.createLine(line)));
        this.isFormOpen = true;
      },
      error: () => this.formErrorMessage = 'We could not load this draft invoice for editing.'
    });
  }

  private resetForm(): void {
    this.form.reset({ customerId: '', warehouseId: '', invoiceDate: this.today(), dueDate: '', reference: '', notes: '' });
    this.lines.clear();
    this.lines.push(this.createLine());
  }

  private createLine(line?: SalesInvoiceLine) {
    return this.formBuilder.group({
      productId: [line?.productId ?? '', Validators.required],
      description: [line?.description ?? '', Validators.maxLength(500)],
      quantity: [line?.quantity ?? 1, [Validators.required, Validators.min(0.000001)]],
      unitPrice: [line?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      discountPercentage: [line?.discountPercentage ?? 0, [Validators.required, Validators.min(0), Validators.max(100)]]
    });
  }

  private toRequest(): SalesInvoiceInput {
    const value = this.form.getRawValue();
    return {
      customerId: value.customerId,
      warehouseId: value.warehouseId || null,
      invoiceDate: value.invoiceDate,
      dueDate: value.dueDate || null,
      reference: value.reference || null,
      notes: value.notes || null,
      lines: value.lines.map((line) => ({
        productId: line.productId,
        description: line.description || null,
        quantity: Number(line.quantity),
        unitPrice: Number(line.unitPrice),
        discountPercentage: Number(line.discountPercentage)
      }))
    };
  }

  private today(): string {
    return utcDateInput();
  }
}
