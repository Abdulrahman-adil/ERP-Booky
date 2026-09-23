import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { InventoryOverview, StockMovement } from './inventory.models';
import { InventoryService } from './inventory.service';

@Component({
  selector: 'app-inventory-overview',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './inventory-overview.component.html',
  styleUrl: './inventory-overview.component.scss'
})
export class InventoryOverviewComponent implements OnInit {
  overview?: InventoryOverview;
  isLoading = true;
  errorMessage = '';

  constructor(private readonly inventory: InventoryService, readonly auth: AuthService) {}

  ngOnInit(): void {
    this.inventory.getOverview().subscribe({
      next: overview => {
        this.overview = overview;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'We could not load the inventory overview.';
        this.isLoading = false;
      }
    });
  }

  get canManage(): boolean {
    return this.auth.currentUser?.permissions.includes('inventory.manage') === true;
  }

  movementLabel(movement: StockMovement): string {
    return movement.movementType.replace(/([A-Z])/g, ' $1').trim();
  }
}

