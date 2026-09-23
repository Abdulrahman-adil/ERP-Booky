import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { DocumentAttachmentsComponent } from '../../shared/document-attachments/document-attachments.component';
import { PurchaseBill, PurchaseBillPaymentSummary } from './purchase.models';
import { PurchaseService } from './purchase.service';

@Component({ selector: 'app-purchase-bill-detail', standalone: true, imports: [CommonModule, RouterLink, DocumentAttachmentsComponent], styleUrl: '../sales/sales-invoice-detail.component.scss', template: `
<section class="detail-page"><a routerLink="/purchases" class="back-link">← Purchase bills</a><div *ngIf="isLoading" class="loading">Loading purchase bill…</div><div class="notice error" *ngIf="errorMessage">{{ errorMessage }}</div><ng-container *ngIf="bill as current"><header class="detail-heading"><div><span class="eyebrow">Purchase bill</span><h1>{{ current.purchaseBillNumber }}</h1><p>{{ current.supplierName }} · {{ current.supplierReference || 'No supplier reference' }}</p></div><div class="actions"><button *ngIf="current.status === 'Draft' && canManage" (click)="edit()" type="button">Edit draft</button><button *ngIf="current.status === 'Draft' && canManage" class="danger" (click)="deleteDraft()" type="button" [disabled]="isDeleting">{{ isDeleting ? 'Deleting…' : 'Delete draft' }}</button><button *ngIf="current.status === 'Draft' && canManage" class="primary-action" (click)="post()" [disabled]="isPosting">{{ isPosting ? 'Posting…' : 'Post purchase bill' }}</button><button *ngIf="current.status === 'Posted' && canPay" class="primary-action" (click)="pay()">Pay supplier</button></div></header><div class="notice success" *ngIf="successMessage">{{ successMessage }}</div><section class="summary-grid"><article><span>Status</span><strong>{{ current.status }}</strong></article><article><span>Payment status</span><strong>{{ current.paymentStatus || 'Not posted' }}</strong></article><article><span>Total</span><strong>{{ formatAmount(current.total, current.currencyCode) }}</strong></article><article><span>Outstanding</span><strong>{{ current.status === 'Posted' ? formatAmount(current.outstandingAmount, current.currencyCode) : '—' }}</strong></article></section><section class="details-grid"><article><h2>Document details</h2><dl><dt>Bill date</dt><dd>{{ current.invoiceDate | date }}</dd><dt>Due date</dt><dd>{{ current.dueDate ? (current.dueDate | date) : '—' }}</dd><dt>Warehouse</dt><dd>{{ current.warehouseName || 'No inventory impact' }}</dd><dt>Created</dt><dd>{{ current.createdByName || '—' }} {{ current.createdAt ? ('· ' + (current.createdAt | date:'medium')) : '' }}</dd><dt>Posted</dt><dd>{{ current.postedByName || '—' }} {{ current.postedAt ? ('· ' + (current.postedAt | date:'medium')) : '' }}</dd></dl></article><article *ngIf="paymentSummary"><h2>Applied supplier payments</h2><p>{{ formatAmount(paymentSummary.paidAmount, paymentSummary.currencyCode) }} paid of {{ formatAmount(paymentSummary.originalAmount, paymentSummary.currencyCode) }}</p><a *ngFor="let payment of paymentSummary.appliedPayments" [routerLink]="['/supplier-payments', payment.id]">{{ payment.paymentNumber }} · {{ formatAmount(payment.allocatedAmount, payment.currencyCode) }}</a></article></section><section class="table-card"><table><thead><tr><th>Product</th><th>Description</th><th>Quantity</th><th>Unit cost</th><th>Discount</th><th>Total</th></tr></thead><tbody><tr *ngFor="let line of current.lines"><td>{{ line.productSku }}<small>{{ line.productName }}</small></td><td>{{ line.description }}<small *ngIf="line.memo">{{ line.memo }}</small></td><td>{{ line.quantity }} {{ line.unitOfMeasureName }}</td><td>{{ formatAmount(line.unitCost, current.currencyCode) }}</td><td>{{ line.discountPercentage }}%</td><td>{{ formatAmount(line.netAmount, current.currencyCode) }}</td></tr></tbody></table></section><app-document-attachments documentType="PurchaseBill" [documentId]="current.id" [canDelete]="current.status === 'Draft'"></app-document-attachments></ng-container></section>` })
export class PurchaseBillDetailComponent implements OnInit {
  readonly id = this.route.snapshot.paramMap.get('id') || ''; bill?: PurchaseBill; paymentSummary?: PurchaseBillPaymentSummary; isLoading = true; isPosting = false; isDeleting = false; errorMessage = ''; successMessage = '';
  constructor(private readonly route: ActivatedRoute, private readonly router: Router, private readonly purchases: PurchaseService, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}
  ngOnInit(): void { if (!this.id) { this.errorMessage = 'Purchase bill not found.'; this.isLoading = false; return; } this.purchases.getBill(this.id).subscribe({ next: bill => { this.bill = bill; this.isLoading = false; if (bill.status === 'Posted') this.purchases.getBillPaymentSummary(bill.id).subscribe({ next: summary => this.paymentSummary = summary }); }, error: () => { this.errorMessage = 'We could not load this purchase bill.'; this.isLoading = false; } }); }
  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('purchases.manage') === true; }
  get canPay(): boolean { return this.auth.currentUser?.permissions.includes('supplierpayments.manage') === true; }
  edit(): void { if (this.bill?.status === 'Draft') void this.router.navigate(['/purchases'], { queryParams: { edit: this.bill.id } }); }
  pay(): void { if (this.bill) void this.router.navigate(['/supplier-payments'], { queryParams: { supplierId: this.bill.supplierId, billId: this.bill.id } }); }
  deleteDraft(): void {
    if (!this.bill || this.bill.status !== 'Draft' || !this.canManage || this.isDeleting) return;
    const bill = this.bill;
    void this.confirmation.confirm({ title: `Delete draft ${bill.purchaseBillNumber}?`, message: 'This unposted purchase bill will be permanently removed. Posted bills must be corrected through a controlled accounting process.', confirmLabel: 'Delete draft', tone: 'danger' }).then(confirmed => {
      if (!confirmed) return;
      this.isDeleting = true; this.errorMessage = '';
      this.purchases.deleteDraftBill(bill.id).subscribe({ next: () => { this.feedback.success(`${bill.purchaseBillNumber} draft deleted.`); void this.router.navigate(['/purchases']); }, error: error => { this.isDeleting = false; this.errorMessage = error.error?.title || 'We could not delete this purchase bill.'; this.feedback.error(this.errorMessage); } });
    });
  }
  post(): void {
    if (!this.bill || this.bill.status !== 'Draft' || !this.canManage || this.isPosting) return;
    const bill = this.bill;
    void this.confirmation.confirm({ title: `Post ${bill.purchaseBillNumber}?`, message: 'This receives stock where required, creates the supplier payable, and records the accounting impact. Posted bills cannot be edited directly.', confirmLabel: 'Post purchase bill' }).then(confirmed => {
      if (!confirmed) return;
      this.isPosting = true;
      this.purchases.postBill(bill.id).subscribe({ next: postedBill => { this.bill = postedBill; this.isPosting = false; this.successMessage = `${postedBill.purchaseBillNumber} was posted successfully.`; this.feedback.financialSuccess(`${postedBill.purchaseBillNumber} posted successfully.`); this.purchases.getBillPaymentSummary(postedBill.id).subscribe({ next: summary => this.paymentSummary = summary }); }, error: error => { this.isPosting = false; this.errorMessage = error.error?.title || 'We could not post this purchase bill.'; this.feedback.error(this.errorMessage); } });
    });
  }
  formatAmount(amount: number, currency: string): string { return `${currency} ${formatAmount(amount)}`; }
}
