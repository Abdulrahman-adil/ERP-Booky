import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { ProductInventory } from '../inventory/inventory.models';
import { InventoryService } from '../inventory/inventory.service';
import { CustomerSalesSummary } from '../sales/sales.models';
import { SalesService } from '../sales/sales.service';
import { CustomerPayment } from '../payments/payment.models';
import { PaymentService } from '../payments/payment.service';
import { SupplierPurchaseSummary } from '../purchases/purchase.models';
import { PurchaseService } from '../purchases/purchase.service';
import { MasterDataKind, Partner, Product } from './master-data.models';
import { MasterDataService } from './master-data.service';

type DetailKind = Extract<MasterDataKind, 'customers' | 'suppliers' | 'products'>;
type DetailRecord = Partner | Product;

@Component({
  selector: 'app-master-data-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './master-data-detail.component.html',
  styleUrl: './master-data-detail.component.scss'
})
export class MasterDataDetailComponent implements OnInit {
  readonly kind = this.route.snapshot.data['kind'] as DetailKind;
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  readonly config = this.getConfig(this.kind);

  record?: DetailRecord;
  productInventory?: ProductInventory;
  customerSales?: CustomerSalesSummary;
  recentCustomerPayments: CustomerPayment[] = [];
  supplierPurchases?: SupplierPurchaseSummary;
  activeSection = 'overview';
  isLoading = true;
  isInventoryLoading = false;
  isSalesLoading = false;
  isPaymentsLoading = false;
  isPurchasesLoading = false;
  isUpdatingStatus = false;
  isDeleting = false;
  errorMessage = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly masterData: MasterDataService,
    private readonly inventory: InventoryService,
    private readonly sales: SalesService,
    private readonly payments: PaymentService,
    private readonly purchases: PurchaseService,
    private readonly router: Router,
    readonly auth: AuthService,
    private readonly confirmation: ConfirmDialogService,
    private readonly feedback: FeedbackService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes(this.config.managePermission) === true;
  }

  get isPartner(): boolean {
    return this.kind !== 'products';
  }

  get partner(): Partner | undefined {
    return this.isPartner ? this.record as Partner | undefined : undefined;
  }

  get product(): Product | undefined {
    return this.kind === 'products' ? this.record as Product | undefined : undefined;
  }

  get displayName(): string {
    return this.partner?.legalName ?? this.product?.name ?? '';
  }

  get generatedCode(): string {
    return this.partner?.code ?? this.product?.sku ?? '';
  }

  get isActive(): boolean {
    return this.record?.isActive ?? false;
  }

  get canDeleteSupplier(): boolean {
    return this.kind === 'suppliers' && this.canManage;
  }

  get activeSectionLabel(): string {
    return this.sections.find(section => section.id === this.activeSection)?.label ?? '';
  }

  get sections(): ReadonlyArray<{ id: string; label: string }> {
    if (this.kind === 'customers') return [{ id: 'overview', label: 'Overview' }, { id: 'transactions', label: 'Transactions' }, { id: 'invoices', label: 'Invoices' }, { id: 'payments', label: 'Payments' }, { id: 'notes', label: 'Notes' }];
    if (this.kind === 'suppliers') return [{ id: 'overview', label: 'Overview' }, { id: 'purchases', label: 'Purchases' }, { id: 'bills', label: 'Bills' }, { id: 'payments', label: 'Payments' }, { id: 'notes', label: 'Notes' }];
    return [{ id: 'overview', label: 'Overview' }, { id: 'commercial', label: 'Commercial' }, { id: 'inventory', label: 'Inventory' }, { id: 'stock-movements', label: 'Stock movements' }];
  }

  setStatus(): void {
    if (!this.record || !this.canManage || this.isUpdatingStatus) return;
    const nextStatus = !this.record.isActive;
    const action = nextStatus ? 'activate' : 'deactivate';
    void this.confirmation.confirm({ title: `${action[0].toUpperCase()}${action.slice(1)} ${this.config.singular}?`, message: nextStatus ? 'This record becomes available to new transactions again.' : 'Existing history stays intact, but this record cannot be selected for new transactions.', confirmLabel: `${action[0].toUpperCase()}${action.slice(1)} ${this.config.singular}`, tone: nextStatus ? 'primary' : 'warning' }).then(confirmed => {
      if (!confirmed || !this.record) return;
      this.isUpdatingStatus = true;
      this.masterData.setStatus(this.kind, this.record.id, nextStatus).subscribe({
        next: () => { this.record = { ...this.record!, isActive: nextStatus }; this.isUpdatingStatus = false; this.feedback.success(`${this.config.singular} ${action}d.`); },
        error: () => { this.errorMessage = `We could not update this ${this.config.singular.toLowerCase()}.`; this.isUpdatingStatus = false; this.feedback.error(this.errorMessage); }
      });
    });
  }

  deleteSupplier(): void {
    const supplier = this.partner;
    if (!supplier || !this.canDeleteSupplier || this.isDeleting) return;
    void this.confirmation.confirm({ title: `Delete ${supplier.legalName}?`, message: 'Deletion is available only when this supplier has no commercial or financial history. Otherwise, deactivate the supplier instead.', confirmLabel: 'Delete supplier', tone: 'danger' }).then(confirmed => {
      if (!confirmed) return;
      this.isDeleting = true; this.errorMessage = '';
      this.masterData.deleteSupplier(supplier.id).subscribe({ next: () => { this.feedback.success(`${supplier.legalName} deleted.`); void this.router.navigateByUrl(this.config.listRoute); }, error: (error: { error?: { title?: string } }) => { this.errorMessage = error.error?.title ?? 'We could not delete this supplier.'; this.isDeleting = false; this.feedback.error(this.errorMessage); } });
    });
  }

  futureMessage(): string {
    const messages: Record<DetailKind, Record<string, string>> = {
      customers: {
        transactions: 'Recent posted sales and the current receivable balance are shown here.',
        invoices: 'Recent sales invoices are shown here.',
        payments: 'No customer payments have been posted for this customer yet.',
        notes: 'Notes will be available in a future phase.'
      },
      suppliers: {
        purchases: 'Purchases will appear here when purchasing workflows are available.',
        bills: 'No bills yet. Purchase billing has not been implemented.',
        payments: 'No payments yet. Supplier payment workflows have not been implemented.',
        notes: 'Notes will be available in a future phase.'
      },
      products: {
        commercial: 'Sales and purchase prices are not captured by the current product master.',
        inventory: 'Inventory quantities and warehouse availability will appear here after inventory workflows are implemented.',
        'stock-movements': 'Stock movements will appear here after inventory workflows are implemented.'
      }
    };

    return messages[this.kind][this.activeSection] ?? 'This area will become available in a future phase.';
  }

  locationText(partner: Partner): string {
    return [partner.city, partner.postalCode, partner.countryCode].filter((value): value is string => Boolean(value)).join(', ');
  }

  formatAmount(amount: number, currency?: string): string {
    return currency ? `${currency} ${formatAmount(amount)}` : '—';
  }

  private load(): void {
    if (!this.id) {
      this.errorMessage = 'The requested record could not be found.';
      this.isLoading = false;
      return;
    }

    this.masterData.get<DetailRecord>(this.kind, this.id).subscribe({
      next: record => {
        this.record = record;
        this.isLoading = false;
        if (this.kind === 'products') this.loadProductInventory(record.id);
        if (this.kind === 'customers') { this.loadCustomerSales(record.id); this.loadCustomerPayments(record.id); }
        if (this.kind === 'suppliers') this.loadSupplierPurchases(record.id);
      },
      error: error => {
        this.errorMessage = error.status === 404 ? `${this.config.singular} not found.` : `We could not load this ${this.config.singular.toLowerCase()}.`;
        this.isLoading = false;
      }
    });
  }

  private loadProductInventory(productId: string): void {
    this.isInventoryLoading = true;
    this.inventory.getProductInventory(productId).subscribe({
      next: inventory => {
        this.productInventory = inventory;
        this.isInventoryLoading = false;
      },
      error: () => this.isInventoryLoading = false
    });
  }

  private loadCustomerSales(customerId: string): void {
    this.isSalesLoading = true;
    this.sales.getCustomerSummary(customerId).subscribe({
      next: (summary) => {
        this.customerSales = summary;
        this.isSalesLoading = false;
      },
      error: () => this.isSalesLoading = false
    });
  }

  private loadCustomerPayments(customerId: string): void {
    this.isPaymentsLoading = true;
    this.payments.listPayments({ customerId, pageNumber: 1, pageSize: 5 }).subscribe({
      next: result => { this.recentCustomerPayments = result.items; this.isPaymentsLoading = false; },
      error: () => this.isPaymentsLoading = false
    });
  }

  private loadSupplierPurchases(supplierId: string): void {
    this.isPurchasesLoading = true;
    this.purchases.getSupplierSummary(supplierId).subscribe({
      next: summary => { this.supplierPurchases = summary; this.isPurchasesLoading = false; },
      error: () => this.isPurchasesLoading = false
    });
  }

  private getConfig(kind: DetailKind) {
    return {
      customers: { singular: 'Customer', listRoute: '/customers', managePermission: 'customers.manage' },
      suppliers: { singular: 'Supplier', listRoute: '/suppliers', managePermission: 'purchases.manage' },
      products: { singular: 'Product', listRoute: '/products', managePermission: 'products.manage' }
    }[kind];
  }
}
