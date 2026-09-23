import { formatAmount } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../core/auth/auth.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { GeneralLedgerLine } from '../accounting/accounting.models';
import { AccountingService } from '../accounting/accounting.service';
import { BalanceSheetReport, FinancialReportFormat, IncomeStatementReport, StatementAccountRow, StatementSection, TrialBalanceReport, TrialBalanceRow } from './financial-reports.models';
import { FinancialReportFilters, FinancialReportsService } from './financial-reports.service';

type ReportView = 'overview' | 'trial-balance' | 'income-statement' | 'balance-sheet' | 'general-ledger';
type DatePreset = 'today' | 'week' | 'month' | 'last-month' | 'quarter' | 'year' | 'custom';

@Component({
  selector: 'app-financial-reports',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './financial-reports.component.html',
  styleUrl: './financial-reports.component.scss'
})
export class FinancialReportsComponent implements OnInit {
  readonly views: Array<{ id: ReportView; label: string }> = [
    { id: 'overview', label: 'Overview' }, { id: 'trial-balance', label: 'Trial balance' }, { id: 'income-statement', label: 'Profit and loss' }, { id: 'balance-sheet', label: 'Balance sheet' }, { id: 'general-ledger', label: 'General ledger' }
  ];
  activeView: ReportView = 'overview';
  filters: FinancialReportFilters = { fromDate: this.yearStart(), toDate: this.today(), includeZeroBalances: false };
  activePreset: DatePreset = 'year';
  trialBalance: TrialBalanceReport | null = null;
  incomeStatement: IncomeStatementReport | null = null;
  balanceSheet: BalanceSheetReport | null = null;
  ledger: GeneralLedgerLine[] = [];
  ledgerTotal = 0;
  ledgerAccountId = '';
  ledgerAccountLabel = '';
  isLoading = false;
  isExporting = false;
  errorMessage = '';

  constructor(private readonly reports: FinancialReportsService, private readonly accounting: AccountingService, private readonly feedback: FeedbackService, readonly auth: AuthService) {}

  get canView(): boolean { return this.auth.currentUser?.permissions.includes('financialreports.view') === true; }
  get canExport(): boolean { return this.auth.currentUser?.permissions.includes('financialreports.export') === true; }

  ngOnInit(): void { this.restoreFilters(); this.loadAll(); }

  selectView(view: ReportView): void {
    this.activeView = view;
    this.errorMessage = '';
    if (view === 'general-ledger') this.loadLedger();
  }

  applyFilters(): void { this.persistFilters(); this.loadAll(); }

  clearFilters(): void { this.filters = { fromDate: this.yearStart(), toDate: this.today(), includeZeroBalances: false }; this.activePreset = 'year'; this.persistFilters(); this.loadAll(); }

  applyDatePreset(preset: Exclude<DatePreset, 'custom'>): void {
    const today = this.localDate();
    let from = new Date(today);
    const to = new Date(today);
    if (preset === 'week') from.setDate(today.getDate() - ((today.getDay() + 6) % 7));
    if (preset === 'month') from.setDate(1);
    if (preset === 'last-month') { from.setDate(1); from.setMonth(from.getMonth() - 1); to.setDate(0); }
    if (preset === 'quarter') { from.setDate(1); from.setMonth(today.getMonth() - (today.getMonth() % 3)); }
    if (preset === 'year') from.setMonth(0, 1);
    this.filters = { ...this.filters, fromDate: this.toDateInput(from), toDate: this.toDateInput(to) };
    this.activePreset = preset;
    this.applyFilters();
  }

  useCustomDates(): void { this.activePreset = 'custom'; }

  openLedger(accountId: string, code: string, name: string): void {
    this.ledgerAccountId = accountId;
    this.ledgerAccountLabel = `${code} — ${name}`;
    this.activeView = 'general-ledger';
    this.loadLedger();
  }

  export(report: 'trial-balance' | 'income-statement' | 'balance-sheet', format: FinancialReportFormat): void {
    if (!this.canExport || this.isExporting) return;
    this.isExporting = true;
    this.reports.export(report, this.filters, format).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${report}-${this.filters.toDate}.${format.toLowerCase()}`;
        link.click();
        URL.revokeObjectURL(url);
        this.feedback.success('Report export is ready.');
        this.isExporting = false;
      },
      error: error => { this.errorMessage = error.error?.title || 'The report export could not be generated.'; this.isExporting = false; }
    });
  }

  statementRows(section: StatementSection): StatementAccountRow[] { return section.rows; }
  accountIndent(row: { hierarchyLevel: number }): string { return `${Math.max(0, row.hierarchyLevel - 1) * 1.15}rem`; }
  formatAmount(value: number): string { return `${this.currencyCode} ${formatAmount(value || 0)}`; }

  private loadAll(): void {
    if (!this.canView || !this.filters.toDate) return;
    this.isLoading = true;
    this.errorMessage = '';
    this.reports.trialBalance(this.filters).subscribe({ next: report => { this.trialBalance = report; this.finishLoad(); }, error: error => this.failLoad(error) });
    this.reports.incomeStatement(this.filters).subscribe({ next: report => { this.incomeStatement = report; this.finishLoad(); }, error: error => this.failLoad(error) });
    this.reports.balanceSheet(this.filters).subscribe({ next: report => { this.balanceSheet = report; this.finishLoad(); }, error: error => this.failLoad(error) });
    this.loadLedger();
  }

  loadLedger(): void {
    if (!this.canView || !this.filters.toDate) return;
    this.accounting.listLedger({ accountId: this.ledgerAccountId, fromDate: this.filters.fromDate, toDate: this.filters.toDate, pageSize: '200' }).subscribe({
      next: result => { this.ledger = result.items; this.ledgerTotal = result.totalCount; },
      error: error => this.failLoad(error)
    });
  }

  private finishLoad(): void {
    if (this.trialBalance && this.incomeStatement && this.balanceSheet) this.isLoading = false;
  }

  private failLoad(error: { error?: { title?: string } }): void { this.errorMessage = error.error?.title || 'Financial reports could not be loaded.'; this.isLoading = false; }
  private get currencyCode(): string { return this.trialBalance?.currencyCode || this.incomeStatement?.currencyCode || this.balanceSheet?.currencyCode || 'USD'; }
  private readonly filterStorageKey = 'erp.financial-report-filters';
  private today(): string { return this.toDateInput(this.localDate()); }
  private yearStart(): string { return `${this.today().slice(0, 4)}-01-01`; }
  private localDate(): Date { const now = new Date(); return new Date(now.getFullYear(), now.getMonth(), now.getDate()); }
  private toDateInput(date: Date): string { return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`; }
  private persistFilters(): void { localStorage.setItem(this.filterStorageKey, JSON.stringify(this.filters)); }
  private restoreFilters(): void {
    try {
      const saved = JSON.parse(localStorage.getItem(this.filterStorageKey) || 'null') as Partial<FinancialReportFilters> | null;
      if (saved?.toDate) this.filters = { fromDate: saved.fromDate || '', toDate: saved.toDate, includeZeroBalances: saved.includeZeroBalances === true };
    } catch { localStorage.removeItem(this.filterStorageKey); }
  }
}
