import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { DocumentAttachmentsComponent } from '../../shared/document-attachments/document-attachments.component';
import { SupplierPayment } from './purchase.models';
import { PurchaseService } from './purchase.service';

@Component({ selector: 'app-supplier-payment-detail', standalone: true, imports: [CommonModule, RouterLink, DocumentAttachmentsComponent], styleUrl: '../sales/sales-invoice-detail.component.scss', template: `
<section class="detail-page"><a routerLink="/supplier-payments" class="back-link">← Supplier payments</a><div class="loading" *ngIf="isLoading">Loading supplier payment…</div><div class="notice error" *ngIf="errorMessage">{{ errorMessage }}</div><ng-container *ngIf="payment as current"><header class="detail-heading"><div><span class="eyebrow">Supplier payment</span><h1>{{ current.paymentNumber }}</h1><p>{{ current.supplierName }} · {{ current.cashBankAccountName }}</p></div><div class="actions"><button *ngIf="current.status === 'Draft' && canManage" (click)="edit()" type="button">Edit draft</button><button *ngIf="current.status === 'Draft' && canManage" class="primary-action" (click)="post()" [disabled]="isPosting">{{ isPosting ? 'Posting…' : 'Post payment' }}</button></div></header><div class="notice success" *ngIf="successMessage">{{ successMessage }}</div><section class="summary-grid"><article><span>Status</span><strong>{{ current.status }}</strong></article><article><span>Payment amount</span><strong>{{ formatAmount(current.amount, current.currencyCode) }}</strong></article><article><span>Allocated</span><strong>{{ formatAmount(current.allocatedAmount, current.currencyCode) }}</strong></article><article><span>Unapplied</span><strong>{{ formatAmount(current.unappliedAmount, current.currencyCode) }}</strong></article></section><section class="details-grid"><article><h2>Payment details</h2><dl><dt>Date</dt><dd>{{ current.paymentDate | date }}</dd><dt>Cash / bank</dt><dd>{{ current.cashBankAccountName }} · {{ current.cashBankAccountType }}</dd><dt>Reference</dt><dd>{{ current.externalReference || '—' }}</dd><dt>Created</dt><dd>{{ current.createdByName || '—' }} {{ current.createdAt ? ('· ' + (current.createdAt | date:'medium')) : '' }}</dd><dt>Posted</dt><dd>{{ current.postedByName || '—' }} {{ current.postedAt ? ('· ' + (current.postedAt | date:'medium')) : '' }}</dd></dl></article></section><section class="table-card"><h2>Allocations</h2><table><thead><tr><th>Purchase bill</th><th>Due date</th><th>Original</th><th>Allocated</th></tr></thead><tbody><tr *ngFor="let allocation of current.allocations"><td><a *ngIf="allocation.purchaseBillId" [routerLink]="['/purchases', allocation.purchaseBillId]">{{ allocation.purchaseBillNumber }}</a><span *ngIf="!allocation.purchaseBillId">{{ allocation.purchaseBillNumber }}</span><small>{{ allocation.supplierReference || '—' }}</small></td><td>{{ allocation.dueDate ? (allocation.dueDate | date) : '—' }}</td><td>{{ formatAmount(allocation.originalAmount, allocation.currencyCode) }}</td><td>{{ formatAmount(allocation.allocationAmount, allocation.currencyCode) }}</td></tr><tr *ngIf="current.allocations.length === 0"><td colspan="4" class="empty">This payment has no allocations.</td></tr></tbody></table></section><app-document-attachments documentType="SupplierPayment" [documentId]="current.id" [canDelete]="current.status === 'Draft'"></app-document-attachments></ng-container></section>` })
export class SupplierPaymentDetailComponent implements OnInit {
  readonly id = this.route.snapshot.paramMap.get('id') || ''; payment?: SupplierPayment; isLoading = true; isPosting = false; errorMessage = ''; successMessage = '';
  constructor(private readonly route: ActivatedRoute, private readonly router: Router, private readonly purchases: PurchaseService, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}
  ngOnInit(): void { if (!this.id) { this.errorMessage = 'Supplier payment not found.'; this.isLoading = false; return; } this.purchases.getSupplierPayment(this.id).subscribe({ next: payment => { this.payment = payment; this.isLoading = false; }, error: () => { this.errorMessage = 'We could not load this supplier payment.'; this.isLoading = false; } }); }
  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('supplierpayments.manage') === true; }
  edit(): void { if (this.payment?.status === 'Draft') void this.router.navigate(['/supplier-payments'], { queryParams: { edit: this.payment.id } }); }
  post(): void {
    if (!this.payment || this.payment.status !== 'Draft' || !this.canManage || this.isPosting) return;
    const payment = this.payment;
    void this.confirmation.confirm({ title: `Post ${payment.paymentNumber}?`, message: 'This applies approved allocations against supplier payables and creates the accounting impact. It cannot be edited directly after posting.', confirmLabel: 'Post supplier payment' }).then(confirmed => {
      if (!confirmed) return;
      this.isPosting = true;
      this.purchases.postSupplierPayment(payment.id).subscribe({ next: postedPayment => { this.payment = postedPayment; this.isPosting = false; this.successMessage = `${postedPayment.paymentNumber} was posted successfully.`; this.feedback.financialSuccess(`${postedPayment.paymentNumber} posted successfully.`); }, error: error => { this.isPosting = false; this.errorMessage = error.error?.title || 'We could not post this supplier payment.'; this.feedback.error(this.errorMessage); } });
    });
  }
  formatAmount(amount: number, currency: string): string { return `${currency} ${formatAmount(amount)}`; }
}
