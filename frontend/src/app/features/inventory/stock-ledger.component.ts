import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { Product } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { inventoryMovementTypes, InventoryMovementType, StockMovement, Warehouse } from './inventory.models';
import { InventoryService } from './inventory.service';

@Component({
  selector: 'app-stock-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './stock-ledger.component.html',
  styleUrl: './stock-ledger.component.scss'
})
export class StockLedgerComponent implements OnInit {
  private readonly formBuilder = new FormBuilder().nonNullable;
  readonly movementTypes = inventoryMovementTypes;
  readonly form = this.formBuilder.group({
    productId: ['', Validators.required],
    warehouseId: ['', Validators.required],
    movementType: ['StockIn' as InventoryMovementType, Validators.required],
    quantity: [0, [Validators.required, Validators.min(0.000001)]],
    occurredAt: [this.localDateTime(), Validators.required],
    externalReference: ['', Validators.maxLength(100)],
    notes: ['', Validators.maxLength(1000)]
  });

  products: Product[] = [];
  warehouses: Warehouse[] = [];
  movements: StockMovement[] = [];
  search = '';
  productFilter = '';
  warehouseFilter = '';
  movementTypeFilter = '';
  fromDate = '';
  toDate = '';
  pageNumber = 1;
  pageSize = 20;
  totalCount = 0;
  isLoading = true;
  isSaving = false;
  isFormOpen = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private readonly inventory: InventoryService,
    private readonly masterData: MasterDataService,
    private readonly route: ActivatedRoute,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.productFilter = this.route.snapshot.queryParamMap.get('productId') ?? '';
    this.warehouseFilter = this.route.snapshot.queryParamMap.get('warehouseId') ?? '';
    this.movementTypeFilter = this.route.snapshot.queryParamMap.get('movementType') ?? '';
    this.loadReferences();
    this.load();
    const action = this.route.snapshot.queryParamMap.get('action') as InventoryMovementType | null;
    if (action && this.isManualType(action) && this.canManage) this.openMovement(action);
  }

  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('inventory.manage') === true; }
  get pageCount(): number { return Math.max(1, Math.ceil(this.totalCount / this.pageSize)); }
  get isAdjustment(): boolean { return this.form.controls.movementType.value === 'PositiveAdjustment' || this.form.controls.movementType.value === 'NegativeAdjustment'; }

  applyFilters(): void { this.pageNumber = 1; this.load(); }
  clearFilters(): void {
    this.search = ''; this.productFilter = ''; this.warehouseFilter = ''; this.movementTypeFilter = ''; this.fromDate = ''; this.toDate = ''; this.applyFilters();
  }
  changePage(nextPage: number): void {
    if (nextPage < 1 || nextPage > this.pageCount || nextPage === this.pageNumber) return;
    this.pageNumber = nextPage; this.load();
  }

  openMovement(type: InventoryMovementType = 'StockIn'): void {
    if (!this.canManage) return;
    this.form.reset({
      productId: this.productFilter,
      warehouseId: this.warehouseFilter,
      movementType: type,
      quantity: 0,
      occurredAt: this.localDateTime(),
      externalReference: '',
      notes: ''
    });
    this.errorMessage = '';
    this.isFormOpen = true;
  }

  closeForm(): void { this.isFormOpen = false; }

  save(): void {
    if (this.form.invalid || !this.canManage || (this.isAdjustment && !this.form.controls.notes.value.trim())) {
      this.form.markAllAsTouched();
      if (this.isAdjustment && !this.form.controls.notes.value.trim()) this.errorMessage = 'A reason is required for an inventory adjustment.';
      return;
    }
    this.isSaving = true;
    this.errorMessage = '';
    const value = this.form.getRawValue();
    this.inventory.recordMovement({
      productId: value.productId,
      warehouseId: value.warehouseId,
      movementType: value.movementType,
      quantity: value.quantity,
      occurredAt: new Date(value.occurredAt).toISOString(),
      externalReference: value.externalReference || null,
      notes: value.notes || null
    }).subscribe({
      next: movement => {
        this.successMessage = `${this.movementLabel(movement)} ${movement.movementNumber} was recorded.`;
        this.isSaving = false;
        this.closeForm();
        this.load();
      },
      error: error => { this.errorMessage = error.error?.title ?? 'We could not record this movement.'; this.isSaving = false; }
    });
  }

  movementLabel(movement: StockMovement | InventoryMovementType): string {
    const value = typeof movement === 'string' ? movement : movement.movementType;
    return value.replace(/([A-Z])/g, ' $1').trim();
  }

  trackById(_: number, item: { id: string }): string { return item.id; }

  private load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.inventory.getLedger({
      search: this.search,
      productId: this.productFilter || undefined,
      warehouseId: this.warehouseFilter || undefined,
      movementType: this.movementTypeFilter || undefined,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      pageNumber: this.pageNumber,
      pageSize: this.pageSize
    }).subscribe({
      next: result => { this.movements = result.items; this.totalCount = result.totalCount; this.isLoading = false; },
      error: () => { this.errorMessage = 'We could not load the stock ledger.'; this.isLoading = false; }
    });
  }

  private loadReferences(): void {
    this.masterData.list<Product>('products', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.products = result.items });
    this.inventory.listWarehouses({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.warehouses = result.items });
  }

  private isManualType(value: string): value is InventoryMovementType {
    return this.movementTypes.some(type => type.value === value);
  }

  private localDateTime(): string { return new Date().toISOString().slice(0, 16); }
}

