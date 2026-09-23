import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Partner } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { OpenReceivable } from './payment.models';
import { PaymentService } from './payment.service';

@Component({ selector: 'app-receivables', standalone: true, imports: [CommonModule, FormsModule, RouterLink], templateUrl: './receivables.component.html', styleUrl: './payments.component.scss' })
export class ReceivablesComponent implements OnInit {
  items: OpenReceivable[] = [];
  customers: Partner[] = [];
  customerId = '';
  openState = 'true';
  overdue = '';
  fromDate = '';
  toDate = '';
  isLoading = true;
  errorMessage = '';
  totalCount = 0;
  constructor(private readonly payments: PaymentService, private readonly masterData: MasterDataService) {}
  ngOnInit(): void { this.masterData.list<Partner>('customers', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.customers = result.items }); this.load(); }
  load(): void { this.isLoading = true; this.payments.listReceivables({ customerId: this.customerId, isOpen: this.openState || undefined, isOverdue: this.overdue || undefined, fromDate: this.fromDate, toDate: this.toDate, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => { this.items = result.items; this.totalCount = result.totalCount; this.isLoading = false; }, error: () => { this.errorMessage = 'We could not load accounts receivable.'; this.isLoading = false; } }); }
  clear(): void { this.customerId = ''; this.openState = 'true'; this.overdue = ''; this.fromDate = ''; this.toDate = ''; this.load(); }
  formatAmount(amount: number, currency?: string): string { return currency ? `${currency} ${formatAmount(amount)}` : '—'; }
}
