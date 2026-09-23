import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';

import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';

@Component({
  selector: 'app-feedback-outlet',
  standalone: true,
  imports: [AsyncPipe, NgFor, NgIf],
  templateUrl: './feedback-outlet.component.html',
  styleUrl: './feedback-outlet.component.scss'
})
export class FeedbackOutletComponent {
  constructor(
    readonly feedback: FeedbackService,
    readonly confirmation: ConfirmDialogService
  ) {}

  confirm(): void {
    this.feedback.primeFinancialSound();
    this.confirmation.resolve(true);
  }
}
