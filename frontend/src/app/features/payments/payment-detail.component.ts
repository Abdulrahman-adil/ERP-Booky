import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { DocumentAttachmentsComponent } from '../../shared/document-attachments/document-attachments.component';
import { CustomerPayment } from './payment.models';
import { PaymentService } from './payment.service';

@Component({
  selector: 'app-payment-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, DocumentAttachmentsComponent],
  templateUrl: './payment-detail.component.html',
  styleUrl: './payments.component.scss'
})
export class PaymentDetailComponent implements OnInit {
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  payment?: CustomerPayment;
  isLoading = true;
  isPosting = false;
  errorMessage = '';

  constructor(private readonly route: ActivatedRoute, private readonly router: Router, private readonly payments: PaymentService, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}

  ngOnInit(): void { this.load(); }
  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('payments.manage') === true; }
  get isDraft(): boolean { return this.payment?.status === 'Draft'; }

  post(): void {
    if (!this.payment || !this.isDraft || !this.canManage || this.isPosting) return;
    const payment = this.payment;
    void this.confirmation.confirm({ title: `Post ${payment.paymentNumber}?`, message: 'This finalizes the receipt and applies its receivable allocations. Posted financial documents cannot be edited directly.', confirmLabel: 'Post payment' }).then(confirmed => {
      if (!confirmed) return;
      this.isPosting = true;
      this.payments.postPayment(payment.id).subscribe({ next: postedPayment => { this.payment = postedPayment; this.isPosting = false; this.feedback.financialSuccess(`${postedPayment.paymentNumber} posted successfully.`); }, error: error => { this.errorMessage = error.error?.title || 'We could not post this payment.'; this.isPosting = false; this.feedback.error(this.errorMessage); } });
    });
  }

  edit(): void { if (this.payment) this.router.navigate(['/payments'], { queryParams: { edit: this.payment.id } }); }
  formatAmount(amount: number, currency?: string): string { return currency ? `${currency} ${formatAmount(amount)}` : '—'; }

  private load(): void {
    this.payments.getPayment(this.id).subscribe({ next: payment => { this.payment = payment; this.isLoading = false; }, error: error => { this.errorMessage = error.status === 404 ? 'Customer payment not found.' : 'We could not load this payment.'; this.isLoading = false; } });
  }
}
