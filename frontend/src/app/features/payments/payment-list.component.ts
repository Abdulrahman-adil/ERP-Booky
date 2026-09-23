import { formatAmount, utcDateInput } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { Partner } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { CashBankAccount, CustomerPayment, CustomerPaymentInput, OpenReceivable } from './payment.models';
import { PaymentService } from './payment.service';

@Component({
  selector: 'app-payment-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './payment-list.component.html',
  styleUrl: './payments.component.scss'
})
export class PaymentListComponent implements OnInit {
  payments: CustomerPayment[] = [];
  customers: Partner[] = [];
  cashBankAccounts: CashBankAccount[] = [];
  receivables: OpenReceivable[] = [];
  search = '';
  customerFilter = '';
  statusFilter = '';
  cashBankFilter = '';
  fromDate = '';
  toDate = '';
  sortBy = 'date';
  sortDescending = true;
  pageNumber = 1;
  totalCount = 0;
  isLoading = true;
  isSaving = false;
  showEditor = false;
  showCashBankForm = false;
  errorMessage = '';
  editorMessage = '';
  editingId = '';
  allocationAmounts: Record<string, number> = {};
  readonly draft = { customerId: '', cashBankAccountId: '', paymentDate: this.today(), amount: 0, externalReference: '', notes: '' };
  readonly cashBankDraft = { name: '', accountType: 'Bank' };

  constructor(
    private readonly paymentsService: PaymentService,
    private readonly masterData: MasterDataService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.loadSupportingData();
    this.loadPayments();
    const editId = this.route.snapshot.queryParamMap.get('edit');
    const customerId = this.route.snapshot.queryParamMap.get('customerId');
    if (editId) this.openExistingDraft(editId);
    else if (customerId) {
      this.showEditor = true;
      this.draft.customerId = customerId;
      this.loadReceivables();
    }
  }

  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('payments.manage') === true; }
  get canManageCashBank(): boolean { return this.auth.currentUser?.permissions.includes('cash-bank.manage') === true; }
  get allocatedAmount(): number { return Object.values(this.allocationAmounts).reduce((total, value) => total + (Number(value) || 0), 0); }
  get unappliedAmount(): number { return Math.max(0, (Number(this.draft.amount) || 0) - this.allocatedAmount); }
  get currencyCode(): string | undefined { return this.receivables[0]?.currencyCode; }

  loadPayments(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.paymentsService.listPayments({ search: this.search, customerId: this.customerFilter, status: this.statusFilter, cashBankAccountId: this.cashBankFilter, fromDate: this.fromDate, toDate: this.toDate, sortBy: this.sortBy, sortDescending: this.sortDescending, pageNumber: this.pageNumber, pageSize: 20 }).subscribe({
      next: result => { this.payments = result.items; this.totalCount = result.totalCount; this.isLoading = false; },
      error: () => { this.errorMessage = 'We could not load customer payments.'; this.isLoading = false; }
    });
  }

  openNewDraft(): void {
    this.resetEditor();
    this.showEditor = true;
  }

  onCustomerChange(): void {
    this.allocationAmounts = {};
    this.loadReceivables();
  }

  saveDraft(): void {
    if (!this.canManage || this.isSaving) return;
    this.editorMessage = '';
    if (!this.draft.customerId || !this.draft.cashBankAccountId || !this.draft.paymentDate || Number(this.draft.amount) <= 0) {
      this.editorMessage = 'Customer, cash or bank destination, date, and a positive amount are required.';
      return;
    }
    const input: CustomerPaymentInput = {
      customerId: this.draft.customerId,
      cashBankAccountId: this.draft.cashBankAccountId,
      paymentDate: this.draft.paymentDate,
      amount: Number(this.draft.amount),
      externalReference: this.draft.externalReference || null,
      notes: this.draft.notes || null,
      allocations: this.receivables.filter(item => Number(this.allocationAmounts[item.openItemId]) > 0).map(item => ({ openItemId: item.openItemId, amount: Number(this.allocationAmounts[item.openItemId]) }))
    };
    if (input.allocations.reduce((total, item) => total + item.amount, 0) > input.amount) {
      this.editorMessage = 'Allocated amounts cannot exceed the received payment amount.';
      return;
    }
    this.isSaving = true;
    const request = this.editingId ? this.paymentsService.updateDraft(this.editingId, input) : this.paymentsService.createDraft(input);
    request.subscribe({
      next: payment => this.router.navigate(['/payments', payment.id]),
      error: error => { this.editorMessage = error.error?.title || 'We could not save this payment draft.'; this.isSaving = false; }
    });
  }

  createCashBankAccount(): void {
    if (!this.canManageCashBank || !this.cashBankDraft.name.trim()) return;
    this.paymentsService.createCashBankAccount(this.cashBankDraft).subscribe({
      next: account => { this.cashBankAccounts = [...this.cashBankAccounts, account].sort((left, right) => left.name.localeCompare(right.name)); this.draft.cashBankAccountId = account.id; this.cashBankDraft.name = ''; this.showCashBankForm = false; },
      error: error => this.editorMessage = error.error?.title || 'We could not create the cash or bank account.'
    });
  }

  clearFilters(): void {
    this.search = ''; this.customerFilter = ''; this.statusFilter = ''; this.cashBankFilter = ''; this.fromDate = ''; this.toDate = ''; this.sortBy = 'date'; this.sortDescending = true; this.pageNumber = 1; this.loadPayments();
  }

  formatAmount(amount: number, currency?: string): string { return currency ? `${currency} ${formatAmount(amount)}` : '—'; }

  private openExistingDraft(id: string): void {
    this.paymentsService.getPayment(id).subscribe({
      next: payment => {
        if (payment.status !== 'Draft') return;
        this.editingId = payment.id;
        Object.assign(this.draft, { customerId: payment.customerId, cashBankAccountId: payment.cashBankAccountId, paymentDate: payment.paymentDate, amount: payment.amount, externalReference: payment.externalReference || '', notes: payment.notes || '' });
        this.showEditor = true;
        this.loadReceivables(payment.allocations);
      },
      error: () => this.errorMessage = 'We could not open the requested payment draft.'
    });
  }

  private loadSupportingData(): void {
    this.masterData.list<Partner>('customers', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.customers = result.items });
    this.paymentsService.listCashBankAccounts().subscribe({ next: accounts => this.cashBankAccounts = accounts.filter(account => account.isActive) });
  }

  private loadReceivables(existingAllocations: Array<{ openItemId: string; allocationAmount: number }> = []): void {
    if (!this.draft.customerId) { this.receivables = []; return; }
    this.paymentsService.getCustomerOpenReceivables(this.draft.customerId).subscribe({
      next: items => {
        this.receivables = items;
        this.allocationAmounts = Object.fromEntries(existingAllocations.map(item => [item.openItemId, item.allocationAmount]));
      },
      error: () => { this.receivables = []; this.editorMessage = 'Open receivables could not be loaded for this customer.'; }
    });
  }

  private resetEditor(): void {
    this.editingId = ''; this.allocationAmounts = {}; this.receivables = []; this.editorMessage = '';
    Object.assign(this.draft, { customerId: '', cashBankAccountId: '', paymentDate: this.today(), amount: 0, externalReference: '', notes: '' });
  }

  private today(): string { return utcDateInput(); }
}
