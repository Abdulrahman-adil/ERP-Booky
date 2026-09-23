import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { ProductionOrder, ProductionRequirement } from './manufacturing.models';
import { ManufacturingService } from './manufacturing.service';

@Component({
  selector: 'app-production-order-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './production-order-detail.component.html',
  styleUrl: './production-order-detail.component.scss'
})
export class ProductionOrderDetailComponent implements OnInit {
  order?: ProductionOrder;
  requirements: ProductionRequirement[] = [];

  get hasShortages(): boolean {
    return this.requirements.some(requirement => !requirement.isAvailable);
  }
  actualProducedQuantity = 0;
  isLoading = true;
  isPosting = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly manufacturing: ManufacturingService,
    private readonly confirmation: ConfirmDialogService,
    private readonly feedback: FeedbackService,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('manufacturing.manage') === true;
  }

  get canComplete(): boolean {
    return this.order?.status === 'Released' && this.actualProducedQuantity > 0 && this.requirements.length > 0 && this.requirements.every(requirement => requirement.isAvailable);
  }

  refreshRequirements(): void {
    if (!this.order || this.actualProducedQuantity <= 0) {
      this.requirements = [];
      return;
    }
    this.manufacturing.getRequirements(this.order.id, this.actualProducedQuantity).subscribe({
      next: requirements => this.requirements = requirements,
      error: error => this.errorMessage = error.error?.title ?? 'Material requirements could not be calculated.'
    });
  }

  release(): void {
    if (!this.order || !this.canManage || this.order.status !== 'Draft') return;
    void this.confirmation.confirm({ title: `Release ${this.order.productionOrderNumber}?`, message: 'The material snapshot is locked after release. No inventory changes happen until you complete the order.', confirmLabel: 'Release order', tone: 'primary' }).then(confirmed => {
      if (!confirmed || !this.order) return;
      this.isPosting = true;
      this.manufacturing.releaseOrder(this.order.id).subscribe({
        next: order => { this.order = order; this.isPosting = false; this.successMessage = 'Production order released. Review the calculated requirements before completing it.'; this.feedback.success(this.successMessage); this.refreshRequirements(); },
        error: error => this.fail(error, 'The production order could not be released.')
      });
    });
  }

  complete(): void {
    if (!this.order || !this.canComplete || !this.canManage) return;
    void this.confirmation.confirm({ title: `Complete ${this.order.productionOrderNumber}?`, message: 'This posts material consumption and finished-goods receipt together. It cannot be posted twice.', confirmLabel: 'Complete production', tone: 'warning' }).then(confirmed => {
      if (!confirmed || !this.order) return;
      this.isPosting = true;
      this.manufacturing.completeOrder(this.order.id, this.actualProducedQuantity).subscribe({
        next: order => { this.order = order; this.isPosting = false; this.successMessage = 'Production completed and stock movements were posted.'; this.feedback.success(this.successMessage); this.refreshRequirements(); },
        error: error => this.fail(error, 'Production could not be completed.')
      });
    });
  }

  statusLabel(status: string): string { return status.replace(/([A-Z])/g, ' $1').trim(); }

  private load(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.errorMessage = 'Production order not found.'; this.isLoading = false; return; }
    this.manufacturing.getOrder(id).subscribe({
      next: order => { this.order = order; this.actualProducedQuantity = order.actualProducedQuantity ?? order.plannedQuantity; this.isLoading = false; this.refreshRequirements(); },
      error: () => { this.errorMessage = 'Production order not found or is unavailable.'; this.isLoading = false; }
    });
  }

  private fail(error: { error?: { title?: string } }, fallback: string): void {
    this.isPosting = false;
    this.errorMessage = error.error?.title ?? fallback;
    this.feedback.error(this.errorMessage);
  }
}
