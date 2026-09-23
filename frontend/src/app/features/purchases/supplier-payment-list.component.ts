import { formatAmount, utcDateInput } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { Partner } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { CashBankAccount } from '../payments/payment.models';
import { PaymentService } from '../payments/payment.service';
import { SupplierPayable, SupplierPayment, SupplierPaymentInput } from './purchase.models';
import { PurchaseService } from './purchase.service';

@Component({
  selector: 'app-supplier-payment-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  styleUrl: './supplier-payment-list.component.scss',
  template: `
    <section class="payments-page">
      <header class="page-heading">
        <div>
          <span class="eyebrow">Accounts payable</span>
          <h1>Supplier payments</h1>
          <p>Create a payment in two steps: enter the payment details, then apply it to the supplier's open bills.</p>
        </div>
        <button *ngIf="canManage" type="button" class="primary-action" (click)="openNew()">New supplier payment</button>
      </header>

      <div class="notice error" *ngIf="errorMessage">{{ errorMessage }}</div>

      <section class="filter-card" aria-label="Filter supplier payments">
        <label>
          <span>Search</span>
          <input [(ngModel)]="search" placeholder="Payment number or supplier" (keyup.enter)="load()">
        </label>
        <label>
          <span>Supplier</span>
          <select [(ngModel)]="supplierFilter">
            <option value="">All suppliers</option>
            <option *ngFor="let supplier of suppliers" [value]="supplier.id">{{ supplier.code }} — {{ supplier.legalName }}</option>
          </select>
        </label>
        <label>
          <span>Status</span>
          <select [(ngModel)]="statusFilter">
            <option value="">All statuses</option>
            <option value="Draft">Draft</option>
            <option value="Posted">Posted</option>
          </select>
        </label>
        <div class="filter-actions">
          <button type="button" (click)="load()">Apply</button>
          <button type="button" class="quiet" (click)="clear()">Clear</button>
        </div>
      </section>

      <section class="editor-card" *ngIf="editorOpen">
        <header class="editor-heading">
          <div>
            <span class="eyebrow">{{ editingId ? 'Draft payment' : 'New payment' }}</span>
            <h2>{{ editingId ? 'Edit supplier payment' : 'Record a supplier payment' }}</h2>
            <p>Nothing affects accounts payable until you post this draft.</p>
          </div>
          <button type="button" class="quiet" (click)="close()">Close</button>
        </header>

        <div class="notice error" *ngIf="editorMessage">{{ editorMessage }}</div>

        <section class="workflow-section">
          <div class="section-heading">
            <span class="step-number">1</span>
            <div>
              <h3>Payment details</h3>
              <p>Choose the supplier, the cash or bank account, and the total amount paid.</p>
            </div>
          </div>

          <div class="form-grid">
            <label>
              <span>Supplier</span>
              <select [(ngModel)]="draft.supplierId" (change)="onSupplierChanged()">
                <option value="">Select supplier</option>
                <option *ngFor="let supplier of suppliers" [value]="supplier.id">{{ supplier.code }} — {{ supplier.legalName }}</option>
              </select>
            </label>
            <label>
              <span>Cash / bank account</span>
              <select [(ngModel)]="draft.cashBankAccountId">
                <option value="">Select account</option>
                <option *ngFor="let account of accounts" [value]="account.id">{{ account.name }} · {{ account.accountType }}</option>
              </select>
            </label>
            <label>
              <span>Payment date</span>
              <input type="date" [(ngModel)]="draft.paymentDate">
            </label>
            <label>
              <span>Payment amount</span>
              <input type="number" min="0.0001" step="any" [(ngModel)]="draft.amount" (ngModelChange)="onPaymentAmountChanged()">
            </label>
          </div>

          <details class="optional-details">
            <summary>Optional reference and notes</summary>
            <div class="form-grid optional-fields">
              <label>
                <span>Reference</span>
                <input [(ngModel)]="draft.externalReference" placeholder="Cheque, transfer, or receipt number">
              </label>
              <label>
                <span>Notes</span>
                <textarea [(ngModel)]="draft.notes" placeholder="Optional internal note"></textarea>
              </label>
            </div>
          </details>
        </section>

        <section class="workflow-section allocation-section">
          <div class="section-heading">
            <span class="step-number">2</span>
            <div>
              <h3>Apply the payment</h3>
              <p *ngIf="!draft.supplierId">Select a supplier above to show its open purchase bills.</p>
              <p *ngIf="draft.supplierId">Apply the payment automatically, or adjust individual bills below.</p>
            </div>
          </div>

          <div class="focus-callout" *ngIf="focusedPayable as payable">
            <div>
              <strong>Selected purchase bill: {{ payable.purchaseBillNumber }}</strong>
              <span>{{ payable.supplierName }} · {{ formatAmount(payable.outstandingAmount, payable.currencyCode) }} outstanding</span>
            </div>
            <button type="button" class="secondary-action" (click)="applyRemainingTo(payable)">Apply remaining amount</button>
          </div>

          <ng-container *ngIf="draft.supplierId">
            <div class="loading-line" *ngIf="isLoadingPayables">Loading open purchase bills…</div>

            <ng-container *ngIf="!isLoadingPayables">
              <div class="allocation-summary">
                <article><span>Payment amount</span><strong>{{ formatAmount(paymentAmount, currencyCode) }}</strong></article>
                <article><span>Applied to bills</span><strong>{{ formatAmount(allocatedAmount, currencyCode) }}</strong></article>
                <article [class.warning]="unappliedAmount > 0"><span>Not applied</span><strong>{{ formatAmount(unappliedAmount, currencyCode) }}</strong></article>
              </div>

              <div class="allocation-actions" *ngIf="payables.length">
                <button type="button" class="secondary-action" (click)="allocateAutomatically()" [disabled]="paymentAmount <= 0">Apply automatically</button>
                <button type="button" class="quiet" (click)="clearAllocations()" [disabled]="allocatedAmount <= 0">Clear amounts</button>
                <span>Applies the payment to the earliest due bills first.</span>
              </div>

              <div class="table-wrap" *ngIf="payables.length; else noPayables">
                <table class="allocation-table">
                  <thead><tr><th>Purchase bill</th><th>Due</th><th>Outstanding</th><th>This payment</th><th></th></tr></thead>
                  <tbody>
                    <tr *ngFor="let payable of payables" [class.focused]="payable.purchaseBillId === focusedBillId">
                      <td><strong>{{ payable.purchaseBillNumber }}</strong><small>{{ payable.supplierReference || payable.supplierName }}</small></td>
                      <td>{{ payable.dueDate ? (payable.dueDate | date) : 'No due date' }}</td>
                      <td>{{ formatAmount(payable.outstandingAmount, payable.currencyCode) }}</td>
                      <td>
                        <input
                          type="number"
                          min="0"
                          [max]="payable.outstandingAmount"
                          step="any"
                          [ngModel]="allocationFor(payable)"
                          (ngModelChange)="updateAllocation(payable, $event)">
                      </td>
                      <td><button type="button" class="quiet" (click)="applyRemainingTo(payable)" [disabled]="remainingToAllocate <= 0">Use remaining</button></td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <ng-template #noPayables>
                <div class="empty-state">This supplier has no open purchase bills to pay.</div>
              </ng-template>

              <div class="notice warning" *ngIf="unappliedAmount > 0">
                {{ formatAmount(unappliedAmount, currencyCode) }} is not assigned to a purchase bill. Review the amounts before posting.
              </div>
            </ng-container>
          </ng-container>
        </section>

        <footer class="editor-footer">
          <div><strong>{{ formatAmount(allocatedAmount, currencyCode) }}</strong> applied to purchase bills</div>
          <button type="button" class="primary-action" (click)="save()" [disabled]="isSaving">
            {{ isSaving ? 'Saving…' : 'Save payment draft' }}
          </button>
        </footer>
      </section>

      <section class="table-card">
        <div class="loading" *ngIf="isLoading">Loading supplier payments…</div>
        <div class="table-wrap" *ngIf="!isLoading">
          <table>
            <thead><tr><th>Payment</th><th>Supplier</th><th>Date</th><th>Status</th><th>Amount</th><th>Applied</th></tr></thead>
            <tbody>
              <tr *ngFor="let payment of payments">
                <td><a [routerLink]="['/supplier-payments', payment.id]">{{ payment.paymentNumber }}</a><small>{{ payment.externalReference || 'No reference' }}</small></td>
                <td>{{ payment.supplierName }}<small>{{ payment.supplierCode }}</small></td>
                <td>{{ payment.paymentDate | date }}</td>
                <td><span class="status" [class.posted]="payment.status === 'Posted'">{{ payment.status }}</span></td>
                <td>{{ formatAmount(payment.amount, payment.currencyCode) }}</td>
                <td>{{ formatAmount(payment.allocatedAmount, payment.currencyCode) }}</td>
              </tr>
              <tr *ngIf="payments.length === 0"><td colspan="6" class="empty">No supplier payments match these filters.</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>
  `
})
export class SupplierPaymentListComponent implements OnInit {
  payments: SupplierPayment[] = [];
  suppliers: Partner[] = [];
  accounts: CashBankAccount[] = [];
  payables: SupplierPayable[] = [];
  allocations: Record<string, number> = {};
  search = '';
  supplierFilter = '';
  statusFilter = '';
  isLoading = true;
  isLoadingPayables = false;
  isSaving = false;
  editorOpen = false;
  editingId = '';
  focusedBillId = '';
  errorMessage = '';
  editorMessage = '';
  readonly draft = {
    supplierId: '',
    cashBankAccountId: '',
    paymentDate: this.today(),
    amount: 0,
    externalReference: '',
    notes: ''
  };

  constructor(
    private readonly purchases: PurchaseService,
    private readonly paymentService: PaymentService,
    private readonly masterData: MasterDataService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.load();
    this.masterData.list<Partner>('suppliers', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.suppliers = result.items });
    this.paymentService.listCashBankAccounts().subscribe({ next: accounts => this.accounts = accounts.filter(account => account.isActive) });

    const edit = this.route.snapshot.queryParamMap.get('edit');
    const supplierId = this.route.snapshot.queryParamMap.get('supplierId');
    const billId = this.route.snapshot.queryParamMap.get('billId');

    if (edit) this.loadDraft(edit);
    else if (supplierId) this.openNew(supplierId, billId || '');
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('supplierpayments.manage') === true;
  }

  get paymentAmount(): number {
    return Math.max(0, Number(this.draft.amount) || 0);
  }

  get allocatedAmount(): number {
    return Object.values(this.allocations).reduce((sum, amount) => sum + (Number(amount) || 0), 0);
  }

  get remainingToAllocate(): number {
    return Math.max(0, this.paymentAmount - this.allocatedAmount);
  }

  get unappliedAmount(): number {
    return this.remainingToAllocate;
  }

  get currencyCode(): string {
    return this.payables[0]?.currencyCode || 'AED';
  }

  get focusedPayable(): SupplierPayable | undefined {
    return this.payables.find(payable => payable.purchaseBillId === this.focusedBillId);
  }

  load(): void {
    this.isLoading = true;
    this.purchases.listSupplierPayments({
      search: this.search,
      supplierId: this.supplierFilter,
      status: this.statusFilter,
      pageNumber: 1,
      pageSize: 100
    }).subscribe({
      next: page => {
        this.payments = page.items;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'We could not load supplier payments.';
        this.isLoading = false;
      }
    });
  }

  clear(): void {
    this.search = '';
    this.supplierFilter = '';
    this.statusFilter = '';
    this.load();
  }

  openNew(supplierId = '', billId = ''): void {
    this.editingId = '';
    this.focusedBillId = billId;
    this.payables = [];
    this.allocations = {};
    Object.assign(this.draft, {
      supplierId,
      cashBankAccountId: '',
      paymentDate: this.today(),
      amount: 0,
      externalReference: '',
      notes: ''
    });
    this.editorOpen = true;
    this.editorMessage = '';
    if (supplierId) this.loadPayables();
  }

  close(): void {
    this.editorOpen = false;
    this.editingId = '';
    this.focusedBillId = '';
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { edit: null, supplierId: null, billId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  onSupplierChanged(): void {
    this.focusedBillId = '';
    this.allocations = {};
    this.loadPayables();
  }

  onPaymentAmountChanged(): void {
    this.normaliseAllocations();
  }

  allocationFor(payable: SupplierPayable): number {
    return Number(this.allocations[payable.openItemId]) || 0;
  }

  updateAllocation(payable: SupplierPayable, value: number | string): void {
    const previousAmount = this.allocationFor(payable);
    const otherAllocations = this.allocatedAmount - previousAmount;
    const maximum = Math.min(payable.outstandingAmount, Math.max(0, this.paymentAmount - otherAllocations));
    this.allocations[payable.openItemId] = Math.min(maximum, Math.max(0, Number(value) || 0));
  }

  applyRemainingTo(payable: SupplierPayable): void {
    const previousAmount = this.allocationFor(payable);
    const otherAllocations = this.allocatedAmount - previousAmount;
    const available = Math.max(0, this.paymentAmount - otherAllocations);
    this.allocations[payable.openItemId] = Math.min(payable.outstandingAmount, available);
  }

  allocateAutomatically(): void {
    let available = this.paymentAmount;
    this.allocations = {};
    for (const payable of this.payables) {
      const allocation = Math.min(payable.outstandingAmount, available);
      if (allocation > 0) this.allocations[payable.openItemId] = allocation;
      available -= allocation;
    }
  }

  clearAllocations(): void {
    this.allocations = {};
  }

  save(): void {
    if (!this.canManage || this.isSaving) return;
    if (!this.draft.supplierId || !this.draft.cashBankAccountId || !this.draft.paymentDate || this.paymentAmount <= 0) {
      this.editorMessage = 'Supplier, cash or bank account, date, and a positive amount are required.';
      return;
    }

    const input: SupplierPaymentInput = {
      supplierId: this.draft.supplierId,
      cashBankAccountId: this.draft.cashBankAccountId,
      paymentDate: this.draft.paymentDate,
      amount: this.paymentAmount,
      externalReference: this.draft.externalReference || null,
      notes: this.draft.notes || null,
      allocations: this.payables
        .filter(payable => this.allocationFor(payable) > 0)
        .map(payable => ({ openItemId: payable.openItemId, amount: this.allocationFor(payable) }))
    };

    if (this.allocatedAmount > this.paymentAmount) {
      this.editorMessage = 'Allocated amounts cannot exceed the payment amount.';
      return;
    }

    this.isSaving = true;
    const request = this.editingId
      ? this.purchases.updateSupplierPayment(this.editingId, input)
      : this.purchases.createSupplierPayment(input);

    request.subscribe({
      next: payment => void this.router.navigate(['/supplier-payments', payment.id]),
      error: error => {
        this.editorMessage = error.error?.title || 'We could not save this supplier payment draft.';
        this.isSaving = false;
      }
    });
  }

  formatAmount(amount: number, currency: string): string {
    return `${currency} ${formatAmount(amount)}`;
  }

  private loadPayables(existing: Array<{ openItemId: string; allocationAmount: number }> = []): void {
    if (!this.draft.supplierId) {
      this.payables = [];
      this.allocations = {};
      return;
    }

    this.isLoadingPayables = true;
    this.purchases.getSupplierOpenPayables(this.draft.supplierId).subscribe({
      next: items => {
        this.payables = items;
        this.allocations = Object.fromEntries(existing.map(item => [item.openItemId, item.allocationAmount]));
        this.normaliseAllocations();
        this.isLoadingPayables = false;
      },
      error: () => {
        this.payables = [];
        this.allocations = {};
        this.editorMessage = 'Open supplier payables could not be loaded.';
        this.isLoadingPayables = false;
      }
    });
  }

  private loadDraft(id: string): void {
    this.purchases.getSupplierPayment(id).subscribe({
      next: payment => {
        if (payment.status !== 'Draft') return;
        this.editingId = payment.id;
        Object.assign(this.draft, {
          supplierId: payment.supplierId,
          cashBankAccountId: payment.cashBankAccountId,
          paymentDate: payment.paymentDate,
          amount: payment.amount,
          externalReference: payment.externalReference || '',
          notes: payment.notes || ''
        });
        this.editorOpen = true;
        this.loadPayables(payment.allocations);
      },
      error: () => this.errorMessage = 'We could not open that supplier payment draft.'
    });
  }

  private normaliseAllocations(): void {
    let available = this.paymentAmount;
    const normalized: Record<string, number> = {};
    for (const payable of this.payables) {
      const allocation = Math.min(payable.outstandingAmount, available, this.allocationFor(payable));
      if (allocation > 0) normalized[payable.openItemId] = allocation;
      available -= allocation;
    }
    this.allocations = normalized;
  }

  private today(): string {
    return utcDateInput();
  }
}
