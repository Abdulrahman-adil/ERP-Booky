import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { StockMovement, WarehouseDetails } from './inventory.models';
import { InventoryService } from './inventory.service';

@Component({
  selector: 'app-warehouse-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './warehouse-detail.component.html',
  styleUrl: './warehouse-detail.component.scss'
})
export class WarehouseDetailComponent implements OnInit {
  readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  details?: WarehouseDetails;
  isLoading = true;
  errorMessage = '';
  isUpdatingStatus = false;

  constructor(private readonly route: ActivatedRoute, private readonly inventory: InventoryService, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}

  ngOnInit(): void { this.load(); }

  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('inventory.manage') === true; }

  get addressText(): string {
    const warehouse = this.details?.warehouse;
    if (!warehouse) return '';
    return [warehouse.addressLine1, warehouse.addressLine2, warehouse.city, warehouse.postalCode, warehouse.countryCode].filter(Boolean).join(', ');
  }

  movementLabel(movement: StockMovement): string { return movement.movementType.replace(/([A-Z])/g, ' $1').trim(); }
  itemPurposeLabel(value?: string): string { return value ? value.replace(/([A-Z])/g, ' $1').trim() : 'Not specified'; }

  setStatus(): void {
    const warehouse = this.details?.warehouse;
    if (!warehouse || !this.canManage || this.isUpdatingStatus) return;
    const isActive = !warehouse.isActive;
    const action = isActive ? 'activate' : 'deactivate';
    void this.confirmation.confirm({ title: `${action[0].toUpperCase()}${action.slice(1)} ${warehouse.name}?`, message: isActive ? 'This warehouse becomes available to new inventory activity.' : 'Existing stock history remains intact, but it cannot be selected for new activity.', confirmLabel: `${action[0].toUpperCase()}${action.slice(1)} warehouse`, tone: isActive ? 'primary' : 'warning' }).then(confirmed => {
      if (!confirmed) return;
      this.isUpdatingStatus = true;
      this.inventory.setWarehouseStatus(warehouse.id, isActive).subscribe({ next: () => { this.isUpdatingStatus = false; this.feedback.success(`Warehouse ${action}d.`); this.load(); }, error: error => { this.errorMessage = error.error?.title ?? 'We could not update the warehouse status.'; this.isUpdatingStatus = false; this.feedback.error(this.errorMessage); } });
    });
  }

  private load(): void {
    if (!this.id) { this.errorMessage = 'The requested warehouse could not be found.'; this.isLoading = false; return; }
    this.inventory.getWarehouseDetails(this.id).subscribe({
      next: details => { this.details = details; this.isLoading = false; },
      error: error => { this.errorMessage = error.status === 404 ? 'Warehouse not found.' : 'We could not load this warehouse.'; this.isLoading = false; }
    });
  }
}
