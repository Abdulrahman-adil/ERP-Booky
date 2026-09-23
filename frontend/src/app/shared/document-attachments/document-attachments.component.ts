import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { ApiService } from '../../core/services/api.service';
import { AccountingService } from '../../features/accounting/accounting.service';
import { DocumentAttachment } from '../../features/accounting/accounting.models';

@Component({
  selector: 'app-document-attachments',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './document-attachments.component.html',
  styleUrl: './document-attachments.component.scss'
})
export class DocumentAttachmentsComponent implements OnChanges {
  @Input({ required: true }) documentType = '';
  @Input({ required: true }) documentId = '';
  @Input() canDelete = false;

  attachments: DocumentAttachment[] = [];
  isLoading = false;
  isUploading = false;
  message = '';

  constructor(private readonly accounting: AccountingService, private readonly api: ApiService, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}

  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('documents.manage') === true; }

  ngOnChanges(): void {
    if (this.documentType && this.documentId) this.load();
  }

  upload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.item(0);
    if (!file || !this.canManage || this.isUploading) return;
    this.message = '';
    if (file.size > 10 * 1024 * 1024) { this.message = 'Attachments must be 10 MB or smaller.'; return; }
    this.isUploading = true;
    this.accounting.uploadAttachment(this.documentType, this.documentId, file).subscribe({
      next: attachment => { this.attachments = [attachment, ...this.attachments]; this.isUploading = false; (event.target as HTMLInputElement).value = ''; },
      error: error => { this.message = error.error?.title || 'The attachment could not be uploaded.'; this.isUploading = false; }
    });
  }

  download(attachment: DocumentAttachment): void {
    this.api.getBlob(this.accounting.attachmentDownloadUrl(this.documentType, this.documentId, attachment.id)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.originalFileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.message = 'The attachment could not be downloaded.'
    });
  }

  delete(attachment: DocumentAttachment): void {
    if (!this.canDelete || !this.canManage) return;
    void this.confirmation.confirm({ title: `Delete ${attachment.originalFileName}?`, message: 'This attachment will be permanently removed from the document.', confirmLabel: 'Delete attachment', tone: 'danger' }).then(confirmed => {
      if (!confirmed) return;
      this.accounting.deleteAttachment(this.documentType, this.documentId, attachment.id).subscribe({ next: () => { this.attachments = this.attachments.filter(item => item.id !== attachment.id); this.feedback.success('Attachment deleted.'); }, error: error => { this.message = error.error?.title || 'The attachment could not be deleted.'; this.feedback.error(this.message); } });
    });
  }

  formatSize(size: number): string { return size >= 1024 * 1024 ? `${(size / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(size / 1024))} KB`; }

  private load(): void {
    this.isLoading = true;
    this.message = '';
    this.accounting.listAttachments(this.documentType, this.documentId).subscribe({
      next: attachments => { this.attachments = attachments; this.isLoading = false; },
      error: error => { this.message = error.error?.title || 'Attachments could not be loaded.'; this.isLoading = false; }
    });
  }
}
