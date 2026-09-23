export interface PagedResult<T> { items: T[]; totalCount: number; }

export interface Account {
  id: string;
  code: string;
  name: string;
  accountType: AccountType;
  accountRole: AccountRole;
  defaultNormalBalance: 'Debit' | 'Credit';
  normalBalanceOverride: 'Debit' | 'Credit' | null;
  normalBalance: 'Debit' | 'Credit';
  isContraAccount: boolean;
  parentAccountId: string | null;
  hierarchyLevel: number;
  isPosting: boolean;
  isActive: boolean;
  balance: number;
}

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense';
export type AccountRole = 'None' | 'Cash' | 'Bank' | 'AccountsReceivable' | 'AccountsPayable' | 'Inventory' | 'SalesRevenue' | 'OtherIncome' | 'CostOfGoodsSold' | 'Expense' | 'OtherExpense' | 'Equity' | 'RetainedEarnings' | 'CurrentYearEarnings';

export interface FinancialClass { id: string; code: string; name: string; isActive: boolean; }
export interface AccountingPeriod { id: string; name: string; startDate: string; endDate: string; status: 'Open' | 'Closed'; }
export interface PostingProfileMapping { postingKey: string; accountId: string; accountCode: string; accountName: string; }
export interface PostingProfile { id: string; name: string; isActive: boolean; mappings: PostingProfileMapping[]; }
export interface CashBankGlMapping { cashBankAccountId: string; cashBankAccountName: string; accountType: string; postingAccountId: string | null; postingAccountCode: string | null; postingAccountName: string | null; }

export interface JournalLine { id?: string; lineNumber?: number; accountId: string; accountCode?: string; accountName?: string; businessPartnerId?: string | null; businessPartnerName?: string | null; financialClassId?: string | null; financialClassName?: string | null; debit: number; credit: number; memo?: string | null; }
export interface Journal { id: string; journalNumber: string; entryDate: string; description: string; reference: string | null; sourceModule: string; sourceDocumentType: string; sourceDocumentId: string; status: 'Draft' | 'Posted' | 'Reversed'; totalDebit: number; totalCredit: number; createdAt: string | null; postedAt: string | null; lines: JournalLine[]; }
export interface GeneralLedgerLine { id: string; date: string; journalNumber: string; accountId: string; accountCode: string; accountName: string; description: string; businessPartnerName: string | null; financialClassName: string | null; debit: number; credit: number; runningBalance: number; sourceModule: string; sourceDocumentType: string; sourceDocumentId: string; reference: string | null; }
export interface PendingTransaction { id: string; transactionDate: string; sourceModule: string; sourceDocumentType: string; sourceDocumentId: string; status: string; sourceDocumentNumber: string | null; }
export interface PendingPostingResult { postedCount: number; failures: string[]; }
export interface DocumentAttachment { id: string; documentType: string; documentId: string; originalFileName: string; contentType: string; fileSize: number; uploadedAt: string; uploadedByUserId: string; }

export const postingKeys = [
  { key: 'SALES_RECEIVABLE', label: 'Sales — Accounts receivable' },
  { key: 'SALES_REVENUE', label: 'Sales — Revenue' },
  { key: 'CUSTOMER_PAYMENT_RECEIVABLE', label: 'Customer payment — Accounts receivable' },
  { key: 'PURCHASE_INVENTORY', label: 'Purchase bill — Inventory' },
  { key: 'PURCHASE_PAYABLE', label: 'Purchase bill — Accounts payable' },
  { key: 'SUPPLIER_PAYMENT_PAYABLE', label: 'Supplier payment — Accounts payable' }
] as const;
