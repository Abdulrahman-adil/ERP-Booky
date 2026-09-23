import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { BalanceSheetReport, FinancialReportFormat, IncomeStatementReport, TrialBalanceReport } from './financial-reports.models';

@Injectable({ providedIn: 'root' })
export class FinancialReportsService {
  constructor(private readonly api: ApiService) {}

  trialBalance(filters: FinancialReportFilters): Observable<TrialBalanceReport> { return this.api.get<TrialBalanceReport>(`financial-reports/trial-balance${this.query(filters)}`); }
  incomeStatement(filters: FinancialReportFilters): Observable<IncomeStatementReport> { return this.api.get<IncomeStatementReport>(`financial-reports/income-statement${this.query(filters)}`); }
  balanceSheet(filters: FinancialReportFilters): Observable<BalanceSheetReport> { return this.api.get<BalanceSheetReport>(`financial-reports/balance-sheet${this.query(filters)}`); }
  export(report: 'trial-balance' | 'income-statement' | 'balance-sheet', filters: FinancialReportFilters, format: FinancialReportFormat): Observable<Blob> {
    return this.api.getBlob(`financial-reports/${report}/export${this.query({ ...filters, format })}`);
  }

  private query(filters: FinancialReportFilters & { format?: FinancialReportFormat }): string {
    const query = Object.entries(filters).filter(([, value]) => value !== '' && value !== undefined && value !== false).map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`).join('&');
    return query ? `?${query}` : '';
  }
}

export interface FinancialReportFilters { fromDate: string; toDate: string; includeZeroBalances: boolean; }
