import { formatAmount, utcDateInput } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { CustomerPayment } from '../payments/payment.models';
import { PaymentService } from '../payments/payment.service';
import { InventoryOverview } from '../inventory/inventory.models';
import { InventoryService } from '../inventory/inventory.service';
import { PurchaseDashboard } from '../purchases/purchase.models';
import { PurchaseService } from '../purchases/purchase.service';
import { BalanceSheetReport, IncomeStatementReport } from '../reports/financial-reports.models';
import { FinancialReportsService } from '../reports/financial-reports.service';
import { SalesDashboard } from '../sales/sales.models';
import { SalesService } from '../sales/sales.service';

interface ActivityMonth {
  periodStart: string;
  sales: number;
  purchases: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly quickActions = [
    { label: 'Create customer', description: 'Start a sales relationship', route: '/customers', permission: 'customers.manage' },
    { label: 'Create product', description: 'Set up inventory or services', route: '/products', permission: 'products.manage' },
    { label: 'Create warehouse', description: 'Prepare stock locations', route: '/inventory/warehouses', permission: 'inventory.manage' },
    { label: 'Open accounting', description: 'Review the financial workspace', route: '/accounting', permission: 'accounting.view' }
  ];

  salesDashboard?: SalesDashboard;
  purchaseDashboard?: PurchaseDashboard;
  inventoryOverview?: InventoryOverview;
  incomeStatement?: IncomeStatementReport;
  balanceSheet?: BalanceSheetReport;
  recentPayments: CustomerPayment[] = [];
  isSalesLoading = false;
  isPurchasesLoading = false;
  isPaymentsLoading = false;
  isFinancialLoading = false;
  isInventoryLoading = false;
  salesError = '';
  purchasesError = '';
  financialError = '';
  inventoryError = '';

  constructor(
    private readonly sales: SalesService,
    private readonly payments: PaymentService,
    private readonly purchases: PurchaseService,
    private readonly inventory: InventoryService,
    private readonly reports: FinancialReportsService,
    private readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    if (this.canShow('sales.view')) this.loadSalesDashboard();
    if (this.canShow('payments.view')) this.loadRecentPayments();
    if (this.canShow('purchases.view')) this.loadPurchaseDashboard();
    if (this.canShow('inventory.view')) this.loadInventoryOverview();
    if (this.canShow('financialreports.view')) this.loadFinancialPosition();
  }

  formatAmount(amount: number | undefined, currency = this.currencyCode): string {
    return amount === undefined || !currency ? '—' : `${currency} ${formatAmount(amount)}`;
  }

  formatQuantity(amount: number): string {
    return new Intl.NumberFormat('en-AE', { maximumFractionDigits: 3 }).format(amount);
  }

  canShow(permission: string): boolean {
    return this.auth.currentUser?.permissions.includes(permission) === true;
  }

  get currencyCode(): string | undefined {
    return this.salesDashboard?.currencyCode ?? this.purchaseDashboard?.currencyCode ?? this.incomeStatement?.currencyCode ?? this.balanceSheet?.currencyCode;
  }

  get isFreshWorkspace(): boolean {
    return this.salesDashboard?.postedSalesTotal === 0
      && this.salesDashboard?.outstandingReceivableTotal === 0
      && this.purchaseDashboard?.outstandingPayableTotal === 0
      && (this.salesDashboard?.recentPostedInvoices.length ?? 0) === 0
      && (this.purchaseDashboard?.recentPostedBills.length ?? 0) === 0;
  }

  get activityMonths(): ActivityMonth[] {
    const sales = new Map((this.salesDashboard?.monthlyTotals ?? []).map(item => [item.periodStart, item.amount]));
    const purchases = new Map((this.purchaseDashboard?.monthlyTotals ?? []).map(item => [item.periodStart, item.amount]));
    return [...new Set([...sales.keys(), ...purchases.keys()])]
      .sort()
      .map(periodStart => ({ periodStart, sales: sales.get(periodStart) ?? 0, purchases: purchases.get(periodStart) ?? 0 }));
  }

  get cashBankBalance(): number | undefined {
    const rows = this.balanceSheet?.assets.rows.filter(row => row.isPosting && (row.accountRole === 'Cash' || row.accountRole === 'Bank'));
    return rows ? rows.reduce((total, row) => total + row.amount, 0) : undefined;
  }

  activityPercent(amount: number): number {
    const highest = Math.max(0, ...this.activityMonths.flatMap(month => [month.sales, month.purchases]));
    return highest === 0 ? 0 : Math.max(4, Math.round((amount / highest) * 100));
  }

  balancePercent(amount: number): number {
    const highest = Math.max(this.salesDashboard?.outstandingReceivableTotal ?? 0, this.purchaseDashboard?.outstandingPayableTotal ?? 0);
    return highest === 0 ? 0 : Math.max(4, Math.round((amount / highest) * 100));
  }

  private loadSalesDashboard(): void {
    this.isSalesLoading = true;
    this.sales.getDashboard().subscribe({
      next: dashboard => { this.salesDashboard = dashboard; this.isSalesLoading = false; },
      error: () => { this.salesError = 'Sales metrics could not be loaded.'; this.isSalesLoading = false; }
    });
  }

  private loadRecentPayments(): void {
    this.isPaymentsLoading = true;
    this.payments.listPayments({ status: 'Posted', pageNumber: 1, pageSize: 5 }).subscribe({
      next: result => { this.recentPayments = result.items; this.isPaymentsLoading = false; },
      error: () => this.isPaymentsLoading = false
    });
  }

  private loadPurchaseDashboard(): void {
    this.isPurchasesLoading = true;
    this.purchases.getDashboard().subscribe({
      next: dashboard => { this.purchaseDashboard = dashboard; this.isPurchasesLoading = false; },
      error: () => { this.purchasesError = 'Purchase metrics could not be loaded.'; this.isPurchasesLoading = false; }
    });
  }

  private loadInventoryOverview(): void {
    this.isInventoryLoading = true;
    this.inventory.getOverview().subscribe({
      next: overview => { this.inventoryOverview = overview; this.isInventoryLoading = false; },
      error: () => { this.inventoryError = 'Inventory quantities could not be loaded.'; this.isInventoryLoading = false; }
    });
  }

  private loadFinancialPosition(): void {
    const today = utcDateInput();
    this.isFinancialLoading = true;
    forkJoin({
      income: this.reports.incomeStatement({ fromDate: `${today.slice(0, 4)}-01-01`, toDate: today, includeZeroBalances: false }),
      balance: this.reports.balanceSheet({ fromDate: '', toDate: today, includeZeroBalances: false })
    }).subscribe({
      next: result => { this.incomeStatement = result.income; this.balanceSheet = result.balance; this.isFinancialLoading = false; },
      error: () => { this.financialError = 'Financial metrics could not be loaded.'; this.isFinancialLoading = false; }
    });
  }
}
