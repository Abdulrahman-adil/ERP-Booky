import { utcDateInput } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { Warehouse } from '../inventory/inventory.models';
import { InventoryService } from '../inventory/inventory.service';
import { Product } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { BillOfMaterials, BillOfMaterialsInput, ProductionOrder, ProductionOrderInput } from './manufacturing.models';
import { ManufacturingService } from './manufacturing.service';

type ManufacturingMode = 'boms' | 'orders';
interface ComponentDraft { componentProductId: string; quantity: number; }

@Component({
  selector: 'app-manufacturing-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './manufacturing-list.component.html',
  styleUrl: './manufacturing-list.component.scss'
})
export class ManufacturingListComponent implements OnInit {
  readonly mode = this.route.snapshot.data['mode'] as ManufacturingMode;
  bills: BillOfMaterials[] = [];
  orders: ProductionOrder[] = [];
  products: Product[] = [];
  warehouses: Warehouse[] = [];
  search = '';
  status = '';
  isLoading = true;
  isSaving = false;
  isEditorOpen = false;
  errorMessage = '';
  successMessage = '';
  editingBill?: BillOfMaterials;
  editingOrder?: ProductionOrder;
  bomDraft = this.newBomDraft();
  orderDraft = this.newOrderDraft();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly manufacturing: ManufacturingService,
    private readonly masterData: MasterDataService,
    private readonly inventory: InventoryService,
    private readonly confirmation: ConfirmDialogService,
    private readonly feedback: FeedbackService,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.loadReferences();
    this.load();
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('manufacturing.manage') === true;
  }

  get isBomMode(): boolean {
    return this.mode === 'boms';
  }

  get finishedProducts(): Product[] {
    return this.products.filter(product => product.productType === 'Stock' && product.inventoryPurpose === 'FinishedGood');
  }

  get componentProducts(): Product[] {
    return this.products.filter(product => product.productType === 'Stock' && ['RawMaterial', 'PackagingMaterial', 'Consumable', 'SemiFinishedGood'].includes(product.inventoryPurpose ?? ''));
  }

  get availableBills(): BillOfMaterials[] {
    return this.bills.filter(bill => bill.isActive && bill.finishedProductId === this.orderDraft.finishedProductId);
  }

  openCreate(): void {
    this.errorMessage = '';
    this.successMessage = '';
    this.editingBill = undefined;
    this.editingOrder = undefined;
    if (this.isBomMode) this.bomDraft = this.newBomDraft(); else this.orderDraft = this.newOrderDraft();
    this.isEditorOpen = true;
  }

  openBillEdit(bill: BillOfMaterials): void {
    if (!this.canManage) return;
    this.editingBill = bill;
    this.bomDraft = {
      finishedProductId: bill.finishedProductId,
      outputQuantity: bill.outputQuantity,
      effectiveFrom: bill.effectiveFrom ?? '',
      effectiveTo: bill.effectiveTo ?? '',
      notes: bill.notes ?? '',
      isActive: bill.isActive,
      components: bill.components.map(component => ({ componentProductId: component.componentProductId, quantity: component.quantity }))
    };
    this.isEditorOpen = true;
  }

  openOrderEdit(order: ProductionOrder): void {
    if (!this.canManage || order.status !== 'Draft') return;
    this.editingOrder = order;
    this.orderDraft = {
      finishedProductId: order.finishedProductId,
      billOfMaterialsId: order.billOfMaterialsId,
      plannedQuantity: order.plannedQuantity,
      sourceWarehouseId: order.sourceWarehouseId,
      destinationWarehouseId: order.destinationWarehouseId,
      productionDate: order.productionDate,
      notes: order.notes ?? ''
    };
    this.isEditorOpen = true;
  }

  closeEditor(): void {
    this.isEditorOpen = false;
    this.editingBill = undefined;
    this.editingOrder = undefined;
  }

  addComponent(): void {
    this.bomDraft.components.push({ componentProductId: '', quantity: 1 });
  }

  removeComponent(index: number): void {
    if (this.bomDraft.components.length > 1) this.bomDraft.components.splice(index, 1);
  }

  saveBill(): void {
    const input: BillOfMaterialsInput = {
      finishedProductId: this.bomDraft.finishedProductId,
      outputQuantity: Number(this.bomDraft.outputQuantity),
      effectiveFrom: this.bomDraft.effectiveFrom || null,
      effectiveTo: this.bomDraft.effectiveTo || null,
      notes: this.bomDraft.notes || null,
      isActive: this.bomDraft.isActive,
      components: this.bomDraft.components.map(component => ({ componentProductId: component.componentProductId, quantity: Number(component.quantity) }))
    };
    if (!this.canManage || !input.finishedProductId || input.outputQuantity <= 0 || input.components.some(component => !component.componentProductId || component.quantity <= 0)) {
      this.errorMessage = 'Choose a finished product and enter a positive quantity for every formula component.';
      return;
    }
    this.isSaving = true;
    const operation = this.editingBill ? this.manufacturing.updateBill(this.editingBill.id, input) : this.manufacturing.createBill(input);
    operation.subscribe({
      next: () => this.saveSucceeded(`Bill of materials ${this.editingBill ? 'updated' : 'created'}.`),
      error: error => this.saveFailed(error, 'The bill of materials could not be saved.')
    });
  }

  saveOrder(): void {
    const input: ProductionOrderInput = {
      finishedProductId: this.orderDraft.finishedProductId,
      billOfMaterialsId: this.orderDraft.billOfMaterialsId,
      plannedQuantity: Number(this.orderDraft.plannedQuantity),
      sourceWarehouseId: this.orderDraft.sourceWarehouseId,
      destinationWarehouseId: this.orderDraft.destinationWarehouseId,
      productionDate: this.orderDraft.productionDate,
      notes: this.orderDraft.notes || null
    };
    if (!this.canManage || !input.finishedProductId || !input.billOfMaterialsId || input.plannedQuantity <= 0 || !input.sourceWarehouseId || !input.destinationWarehouseId || input.sourceWarehouseId === input.destinationWarehouseId) {
      this.errorMessage = 'Select a formula, positive planned quantity, and different source and destination warehouses.';
      return;
    }
    this.isSaving = true;
    const operation = this.editingOrder ? this.manufacturing.updateOrder(this.editingOrder.id, input) : this.manufacturing.createOrder(input);
    operation.subscribe({
      next: order => {
        this.isSaving = false;
        this.feedback.success(`Production order ${this.editingOrder ? 'updated' : 'created'}.`);
        void this.router.navigate(['/manufacturing/production-orders', order.id]);
      },
      error: error => this.saveFailed(error, 'The production order could not be saved.')
    });
  }

  release(order: ProductionOrder): void {
    if (!this.canManage || order.status !== 'Draft') return;
    void this.confirmation.confirm({ title: `Release ${order.productionOrderNumber}?`, message: 'Releasing locks the production definition. You can still review requirements before completion.', confirmLabel: 'Release order', tone: 'primary' }).then(confirmed => {
      if (!confirmed) return;
      this.manufacturing.releaseOrder(order.id).subscribe({
        next: () => { this.feedback.success('Production order released.'); this.load(); },
        error: error => this.saveFailed(error, 'The production order could not be released.')
      });
    });
  }

  applyFilters(): void { this.load(); }
  clearFilters(): void { this.search = ''; this.status = ''; this.load(); }
  productLabel(product: Product): string { return `${product.sku} — ${product.name}`; }
  purposeLabel(value?: string): string { return value ? value.replace(/([A-Z])/g, ' $1').trim() : 'Not specified'; }
  statusLabel(status: string): string { return status.replace(/([A-Z])/g, ' $1').trim(); }

  private load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    if (this.isBomMode) {
      this.manufacturing.listBills({ search: this.search, isActive: this.status || undefined, pageNumber: 1, pageSize: 100 }).subscribe({
        next: page => { this.bills = page.items; this.isLoading = false; },
        error: () => { this.errorMessage = 'We could not load bills of materials.'; this.isLoading = false; }
      });
      return;
    }
    this.manufacturing.listOrders({ search: this.search, status: this.status || undefined, pageNumber: 1, pageSize: 100 }).subscribe({
      next: page => { this.orders = page.items; this.isLoading = false; },
      error: () => { this.errorMessage = 'We could not load production orders.'; this.isLoading = false; }
    });
    this.manufacturing.listBills({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: page => this.bills = page.items });
  }

  private loadReferences(): void {
    this.masterData.list<Product>('products', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: page => this.products = page.items });
    this.inventory.listWarehouses({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: page => this.warehouses = page.items });
  }

  private saveSucceeded(message: string): void {
    this.isSaving = false;
    this.successMessage = message;
    this.feedback.success(message);
    this.closeEditor();
    this.load();
  }

  private saveFailed(error: { error?: { title?: string } }, fallback: string): void {
    this.isSaving = false;
    this.errorMessage = error.error?.title ?? fallback;
    this.feedback.error(this.errorMessage);
  }

  private newBomDraft(): { finishedProductId: string; outputQuantity: number; effectiveFrom: string; effectiveTo: string; notes: string; isActive: boolean; components: ComponentDraft[] } {
    return { finishedProductId: '', outputQuantity: 1, effectiveFrom: '', effectiveTo: '', notes: '', isActive: true, components: [{ componentProductId: '', quantity: 1 }] };
  }

  private newOrderDraft(): { finishedProductId: string; billOfMaterialsId: string; plannedQuantity: number; sourceWarehouseId: string; destinationWarehouseId: string; productionDate: string; notes: string } {
    return { finishedProductId: '', billOfMaterialsId: '', plannedQuantity: 1, sourceWarehouseId: '', destinationWarehouseId: '', productionDate: utcDateInput(), notes: '' };
  }
}
