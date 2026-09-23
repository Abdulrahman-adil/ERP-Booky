import { AccountRole, AccountType } from '../accounting/accounting.models';

export interface TrialBalanceRow {
  accountId: string;
  code: string;
  name: string;
  accountType: AccountType;
  accountRole: AccountRole;
  hierarchyLevel: number;
  isPosting: boolean;
  isContraAccount: boolean;
  openingDebit: number;
  openingCredit: number;
  periodDebit: number;
  periodCredit: number;
  closingDebit: number;
  closingCredit: number;
}

export interface TrialBalanceReport {
  currencyCode: string;
  fromDate: string | null;
  toDate: string;
  rows: TrialBalanceRow[];
  openingDebitTotal: number;
  openingCreditTotal: number;
  periodDebitTotal: number;
  periodCreditTotal: number;
  closingDebitTotal: number;
  closingCreditTotal: number;
  difference: number;
}

export interface StatementAccountRow {
  accountId: string;
  code: string;
  name: string;
  accountType: AccountType;
  accountRole: AccountRole;
  hierarchyLevel: number;
  isPosting: boolean;
  isContraAccount: boolean;
  isDerived: boolean;
  amount: number;
}

export interface StatementSection { key: string; name: string; total: number; rows: StatementAccountRow[]; }

export interface IncomeStatementReport {
  currencyCode: string;
  fromDate: string;
  toDate: string;
  revenue: StatementSection;
  costOfSales: StatementSection;
  grossProfit: number;
  operatingExpenses: StatementSection;
  operatingProfit: number;
  otherIncome: StatementSection;
  otherExpenses: StatementSection;
  netProfit: number;
}

export interface BalanceSheetReport {
  currencyCode: string;
  asOfDate: string;
  assets: StatementSection;
  liabilities: StatementSection;
  equity: StatementSection;
  currentYearEarnings: number;
  currentYearEarningsIsDerived: boolean;
  totalAssets: number;
  totalLiabilitiesAndEquity: number;
  difference: number;
}

export type FinancialReportFormat = 'Xlsx' | 'Pdf';
