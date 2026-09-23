import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { Category, MasterDataKind, Partner, Product, Unit } from './master-data.models';
import { MasterDataService } from './master-data.service';

type RecordItem = Partner | Category | Unit | Product;

@Component({
  selector: 'app-master-data-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, RouterLinkActive],
  templateUrl: './master-data-page.component.html',
  styleUrl: './master-data-page.component.scss'
})
export class MasterDataPageComponent implements OnInit {
  private readonly formBuilder = new FormBuilder().nonNullable;

  readonly kind = this.route.snapshot.data['kind'] as MasterDataKind;
  readonly config = this.getConfig(this.kind);
  readonly form = this.formBuilder.group({
    code: ['', [Validators.required, Validators.maxLength(this.kind === 'units' ? 20 : this.kind === 'products' ? 60 : 30)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    legalName: ['', [Validators.required, Validators.maxLength(200)]],
    addressLine1: ['', [Validators.required, Validators.maxLength(200)]],
    countryCode: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(2)]],
    addressLine2: ['', Validators.maxLength(200)],
    city: ['', Validators.maxLength(100)],
    postalCode: ['', Validators.maxLength(30)],
    email: ['', [Validators.email, Validators.maxLength(254)]],
    phone: ['', Validators.maxLength(50)],
    taxIdentifier: ['', Validators.maxLength(100)],
    paymentTermsDays: [0, [Validators.required, Validators.min(0), Validators.max(3650)]],
    dimension: ['', [Validators.required, Validators.maxLength(50)]],
    conversionFactorToBase: [1, [Validators.required, Validators.min(0.000001)]],
    decimalPlaces: [0, [Validators.required, Validators.min(0), Validators.max(6)]],
    parentCategoryId: [''],
    productType: ['Stock'],
    stockUnitOfMeasureId: [''],
    productCategoryId: [''],
    inventoryPurpose: ['']
  });

  items: RecordItem[] = [];
  categories: Category[] = [];
  units: Unit[] = [];
  search = '';
  statusFilter = '';
  sortBy = '';
  sortDescending = false;
  productCategoryFilter = '';
  productUnitFilter = '';
  pageNumber = 1;
  pageSize = 20;
  totalCount = 0;
  isLoading = true;
  isSaving = false;
  deletingSupplierId?: string;
  errorMessage = '';
  successMessage = '';
  selectedItem?: RecordItem;
  isFormOpen = false;
  private editRequestHandled = false;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly masterData: MasterDataService,
    readonly auth: AuthService,
    private readonly confirmation: ConfirmDialogService,
    private readonly feedback: FeedbackService
  ) {}

  ngOnInit(): void {
    this.configureForm();
    this.load();
    if (this.kind === 'products' || this.kind === 'categories') {
      this.loadReferences();
    }
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes(this.config.managePermission) === true;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  applyFilters(): void {
    this.pageNumber = 1;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.statusFilter = '';
    this.sortBy = '';
    this.sortDescending = false;
    this.productCategoryFilter = '';
    this.productUnitFilter = '';
    this.applyFilters();
  }

  changePage(nextPage: number): void {
    if (nextPage < 1 || nextPage > this.pageCount || nextPage === this.pageNumber) return;
    this.pageNumber = nextPage;
    this.load();
  }

  openCreate(): void {
    this.selectedItem = undefined;
    this.form.reset(this.formDefaults());
    this.isFormOpen = true;
    this.errorMessage = '';
  }

  openEdit(item: RecordItem): void {
    this.selectedItem = item;
    this.form.reset(this.formDefaults());
    this.form.patchValue(this.toFormValue(item));
    this.isFormOpen = true;
    this.errorMessage = '';
  }

  closeForm(): void {
    this.isFormOpen = false;
    this.selectedItem = undefined;
  }

  save(): void {
    if (this.form.invalid || !this.canManage) {
      this.form.markAllAsTouched();
      return;
    }
    this.isSaving = true;
    this.errorMessage = '';
    const request = this.toRequest();
    const operation = this.selectedItem
      ? this.masterData.update<RecordItem>(this.kind, this.selectedItem.id, request)
      : this.masterData.create<RecordItem>(this.kind, request);
    operation.subscribe({
      next: () => {
        this.successMessage = `${this.config.singular} ${this.selectedItem ? 'updated' : 'created'} successfully.`;
        this.isSaving = false;
        this.closeForm();
        this.load();
        this.loadReferences();
      },
      error: (error: { error?: { title?: string }; status?: number }) => {
        this.isSaving = false;
        this.errorMessage = error.error?.title ?? (error.status === 409 ? 'This code is already in use.' : 'We could not save this record.');
      }
    });
  }

  setStatus(item: RecordItem): void {
    if (!this.canManage || !('isActive' in item)) return;
    const isActive = !item.isActive;
    const action = isActive ? 'activate' : 'deactivate';
    void this.confirmation.confirm({ title: `${action[0].toUpperCase()}${action.slice(1)} ${this.config.singular}?`, message: isActive ? 'This record becomes available for new activity.' : 'Existing history stays intact, but this record cannot be selected for new activity.', confirmLabel: `${action[0].toUpperCase()}${action.slice(1)} ${this.config.singular}`, tone: isActive ? 'primary' : 'warning' }).then(confirmed => {
      if (!confirmed) return;
      this.masterData.setStatus(this.kind as Exclude<MasterDataKind, 'units'>, item.id, isActive).subscribe({ next: () => { this.successMessage = `${this.config.singular} ${isActive ? 'activated' : 'deactivated'}.`; this.feedback.success(this.successMessage); this.load(); }, error: () => { this.errorMessage = 'We could not update this record status.'; this.feedback.error(this.errorMessage); } });
    });
  }

  deleteSupplier(item: RecordItem): void {
    if (this.kind !== 'suppliers' || !this.canManage || !('legalName' in item) || this.deletingSupplierId) return;
    void this.confirmation.confirm({ title: `Delete ${item.legalName}?`, message: 'Deletion is available only when this supplier has no commercial or financial history. Otherwise, deactivate the supplier instead.', confirmLabel: 'Delete supplier', tone: 'danger' }).then(confirmed => {
      if (!confirmed) return;
      this.errorMessage = ''; this.deletingSupplierId = item.id;
      this.masterData.deleteSupplier(item.id).subscribe({ next: () => { this.successMessage = 'Supplier deleted successfully.'; this.feedback.success(this.successMessage); this.deletingSupplierId = undefined; this.load(); }, error: (error: { error?: { title?: string } }) => { this.errorMessage = error.error?.title ?? 'We could not delete this supplier.'; this.deletingSupplierId = undefined; this.feedback.error(this.errorMessage); } });
    });
  }

  displayName(item: RecordItem): string {
    return 'legalName' in item ? item.legalName : item.name;
  }

  itemCode(item: RecordItem): string { return 'sku' in item ? item.sku : item.code; }
  itemContact(item: RecordItem): string { return 'email' in item ? item.email || item.phone || '—' : ''; }
  itemProductType(item: RecordItem): string { return 'productType' in item ? item.productType : ''; }
  itemInventoryPurpose(item: RecordItem): string { return 'inventoryPurpose' in item && item.inventoryPurpose ? item.inventoryPurpose.replace(/([A-Z])/g, ' $1').trim() : '—'; }
  itemCategory(item: RecordItem): string { return 'productCategoryName' in item ? item.productCategoryName || 'Uncategorized' : ''; }
  itemUnit(item: RecordItem): string { return 'unitOfMeasureName' in item ? item.unitOfMeasureName ?? '' : ''; }
  itemDimension(item: RecordItem): string { return 'dimension' in item ? item.dimension : ''; }
  itemParent(item: RecordItem): string { return 'parentCategoryName' in item ? item.parentCategoryName || '' : ''; }
  itemIsActive(item: RecordItem): boolean { return 'isActive' in item && item.isActive; }
  detailRoute(item: RecordItem): string { return `${this.config.navigation}/${item.id}`; }

  isPartner(): boolean { return this.kind === 'customers' || this.kind === 'suppliers'; }
  isProduct(): boolean { return this.kind === 'products'; }
  isCategory(): boolean { return this.kind === 'categories'; }
  isUnit(): boolean { return this.kind === 'units'; }

  private load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    const isActive = this.statusFilter === '' ? undefined : this.statusFilter === 'active';
    this.masterData.list<RecordItem>(this.kind, {
      search: this.search,
      isActive,
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      sortBy: this.sortBy || undefined,
      sortDescending: this.sortDescending || undefined,
      productCategoryId: this.isProduct() ? this.productCategoryFilter || undefined : undefined,
      unitOfMeasureId: this.isProduct() ? this.productUnitFilter || undefined : undefined
    }).subscribe({
      next: (result) => {
        this.items = result.items;
        this.totalCount = result.totalCount;
        this.isLoading = false;
        const editId = this.route.snapshot.queryParamMap.get('edit');
        const itemToEdit = !this.editRequestHandled && editId ? result.items.find(item => item.id === editId) : undefined;
        if (itemToEdit) {
          this.editRequestHandled = true;
          this.openEdit(itemToEdit);
        }
      },
      error: () => {
        this.errorMessage = `We could not load ${this.config.title.toLowerCase()}.`;
        this.isLoading = false;
      }
    });
  }

  private loadReferences(): void {
    if (this.kind === 'products' || this.kind === 'categories') {
      this.masterData.list<Category>('categories', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.categories = result.items });
    }
    if (this.kind === 'products') {
      this.masterData.list<Unit>('units', { pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.units = result.items });
    }
  }

  private configureForm(): void {
    if (this.isPartner()) {
      this.form.controls.name.clearValidators();
      this.form.controls.code.clearValidators();
      ['addressLine1', 'countryCode', 'email', 'phone', 'paymentTermsDays'].forEach(control => this.form.controls[control as keyof typeof this.form.controls].clearValidators());
    }
    if (!this.isPartner()) {
      ['legalName', 'addressLine1', 'countryCode', 'email', 'phone', 'paymentTermsDays'].forEach(control => this.form.controls[control as keyof typeof this.form.controls].clearValidators());
    }
    if (!this.isUnit()) {
      ['dimension', 'conversionFactorToBase', 'decimalPlaces'].forEach(control => this.form.controls[control as keyof typeof this.form.controls].clearValidators());
    }
    if (this.isProduct()) this.form.controls.code.clearValidators();
    if (!this.isProduct()) this.form.controls.stockUnitOfMeasureId.clearValidators();
    this.form.updateValueAndValidity();
  }

  private toRequest(): Record<string, unknown> {
    const value = this.form.getRawValue();
    if (this.isPartner()) return { legalName: value.legalName, addressLine1: value.addressLine1 || null, countryCode: value.countryCode || null, addressLine2: value.addressLine2 || null, city: value.city || null, postalCode: value.postalCode || null, email: value.email || null, phone: value.phone || null, taxIdentifier: value.taxIdentifier || null, paymentTermsDays: value.paymentTermsDays };
    if (this.isUnit()) return { code: value.code, name: value.name, dimension: value.dimension, conversionFactorToBase: value.conversionFactorToBase, decimalPlaces: value.decimalPlaces };
    if (this.isCategory()) return { code: value.code, name: value.name, parentCategoryId: value.parentCategoryId || null };
    return { name: value.name, productType: value.productType, stockUnitOfMeasureId: value.stockUnitOfMeasureId || null, productCategoryId: value.productCategoryId || null, inventoryPurpose: value.inventoryPurpose || null };
  }

  private toFormValue(item: RecordItem): Record<string, unknown> {
    if ('legalName' in item) return { ...item, email: item.email ?? '', phone: item.phone ?? '', addressLine2: item.addressLine2 ?? '', city: item.city ?? '', postalCode: item.postalCode ?? '', taxIdentifier: item.taxIdentifier ?? '' };
    if ('sku' in item) return { code: item.sku, name: item.name, productType: item.productType, stockUnitOfMeasureId: item.stockUnitOfMeasureId, productCategoryId: item.productCategoryId ?? '', inventoryPurpose: item.inventoryPurpose ?? '' };
    if ('dimension' in item) return { ...item } as Record<string, unknown>;
    return { ...item, parentCategoryId: item.parentCategoryId ?? '' };
  }

  private formDefaults(): Record<string, unknown> {
    return { code: '', name: '', legalName: '', addressLine1: '', countryCode: '', addressLine2: '', city: '', postalCode: '', email: '', phone: '', taxIdentifier: '', paymentTermsDays: 0, dimension: '', conversionFactorToBase: 1, decimalPlaces: 0, parentCategoryId: '', productType: 'Stock', stockUnitOfMeasureId: '', productCategoryId: '', inventoryPurpose: '' };
  }

  private getConfig(kind: MasterDataKind) {
    return {
      customers: { title: 'Customers', singular: 'Customer', description: 'Maintain customer records before sales and receivables are introduced.', managePermission: 'customers.manage', navigation: '/customers' },
      suppliers: { title: 'Suppliers', singular: 'Supplier', description: 'Maintain supplier records before purchasing and payables are introduced.', managePermission: 'purchases.manage', navigation: '/suppliers' },
      products: { title: 'Products', singular: 'Product', description: 'Maintain product master records for future sales, purchasing, and inventory.', managePermission: 'products.manage', navigation: '/products' },
      categories: { title: 'Product Categories', singular: 'Product category', description: 'Organize products with lightweight reusable classifications.', managePermission: 'products.manage', navigation: '/products/categories' },
      units: { title: 'Units of Measure', singular: 'Unit of measure', description: 'Define the quantities products can use in future transactions.', managePermission: 'products.manage', navigation: '/products/units' }
    }[kind];
  }
}
