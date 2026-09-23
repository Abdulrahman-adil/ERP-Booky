import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { DocumentAttachmentsComponent } from '../../shared/document-attachments/document-attachments.component';
import { InvoicePaymentSummary } from '../payments/payment.models';
import { PaymentService } from '../payments/payment.service';
import { SalesInvoice } from './sales.models';
import { SalesService } from './sales.service';

@Component({
  selector: 'app-sales-invoice-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, DocumentAttachmentsComponent],
  templateUrl: './sales-invoice-detail.component.html',
  styleUrl: './sales-invoice-detail.component.scss'
})
export class SalesInvoiceDetailComponent implements OnInit {
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';

  invoice?: SalesInvoice;
  isLoading = true;
  isPosting = false;
  errorMessage = '';
  successMessage = '';
  paymentSummary?: InvoicePaymentSummary;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly sales: SalesService,
    private readonly payments: PaymentService,
    readonly auth: AuthService,
    private readonly confirmation: ConfirmDialogService,
    private readonly feedback: FeedbackService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('sales.manage') === true;
  }

  get isDraft(): boolean {
    return this.invoice?.status === 'Draft';
  }

  get canReceivePayment(): boolean {
    return this.auth.currentUser?.permissions.includes('payments.manage') === true;
  }

  receivePayment(): void {
    if (!this.invoice || this.invoice.status !== 'Posted' || !this.canReceivePayment) return;
    void this.router.navigate(['/payments'], { queryParams: { customerId: this.invoice.customerId, invoiceId: this.invoice.id } });
  }

  post(): void {
    if (!this.invoice || !this.isDraft || !this.canManage || this.isPosting) return;
    const invoice = this.invoice;
    void this.confirmation.confirm({ title: `Post ${invoice.invoiceNumber}?`, message: 'Once posted, this invoice creates the customer receivable, issues stock where required, and cannot be edited directly.', confirmLabel: 'Post invoice' }).then(confirmed => {
      if (!confirmed) return;
      this.isPosting = true;
      this.errorMessage = '';
      this.sales.postInvoice(invoice.id).subscribe({
        next: postedInvoice => {
          this.invoice = postedInvoice;
          this.isPosting = false;
          this.successMessage = `${postedInvoice.invoiceNumber} was posted successfully.`;
          this.feedback.financialSuccess(`${postedInvoice.invoiceNumber} posted successfully.`);
        },
        error: (error: { error?: { title?: string }; status?: number }) => {
          this.isPosting = false;
          this.errorMessage = error.error?.title ?? (error.status === 409 ? 'This invoice can no longer be posted.' : 'We could not post this invoice. Check stock availability and required warehouse information.');
          this.feedback.error(this.errorMessage);
        }
      });
    });
  }

  edit(): void {
    if (!this.invoice || !this.isDraft || !this.canManage) return;
    void this.router.navigate(['/sales'], { queryParams: { edit: this.invoice.id } });
  }

  formatAmount(amount: number, currency: string): string {
    return `${currency} ${formatAmount(amount)}`;
  }

  private load(): void {
    if (!this.id) {
      this.errorMessage = 'The requested sales invoice could not be found.';
      this.isLoading = false;
      return;
    }

    this.sales.getInvoice(this.id).subscribe({
      next: (invoice) => {
        this.invoice = invoice;
        if (invoice.status === 'Posted') this.loadPaymentSummary(invoice.id);
        this.isLoading = false;
      },
      error: (error: { status?: number }) => {
        this.errorMessage = error.status === 404 ? 'Sales invoice not found.' : 'We could not load this sales invoice.';
        this.isLoading = false;
      }
    });
  }

  private loadPaymentSummary(invoiceId: string): void {
    this.payments.getInvoiceSummary(invoiceId).subscribe({ next: summary => this.paymentSummary = summary });
  }
}
