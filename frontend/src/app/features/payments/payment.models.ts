import { PageResult } from '../master-data/master-data.models';

export type PaymentStatus = 'Draft' | 'Posted' | 'Cancelled';
export type CashBankAccountType = 'Cash' | 'Bank';

export interface PaymentAllocation {
  id: string;
  openItemId: string;
  invoiceId?: string;
  documentNumber: string;
  invoiceDate?: string;
  dueDate?: string;
  originalAmount: number;
  outstandingAmount: number;
  allocationAmount: number;
  currencyCode: string;
}

export interface CustomerPayment {
  id: string;
  paymentNumber: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  cashBankAccountId: string;
  cashBankAccountName: string;
  cashBankAccountType: CashBankAccountType;
  direction: 'Incoming' | 'Outgoing';
  paymentDate: string;
  currencyCode: string;
  amount: number;
  allocatedAmount: number;
  unappliedAmount: number;
  status: PaymentStatus;
  externalReference?: string;
  notes?: string;
  createdByName?: string;
  createdAt?: string;
  postedByName?: string;
  postedAt?: string;
  allocations: PaymentAllocation[];
}

export interface CustomerPaymentInput {
  customerId: string;
  cashBankAccountId: string;
  paymentDate: string;
  amount: number;
  externalReference?: string | null;
  notes?: string | null;
  allocations: Array<{ openItemId: string; amount: number }>;
}

export interface CashBankAccount {
  id: string;
  name: string;
  accountType: CashBankAccountType;
  postingAccountId?: string;
  isActive: boolean;
}

export interface OpenReceivable {
  openItemId: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  invoiceId?: string;
  invoiceNumber: string;
  invoiceDate?: string;
  dueDate?: string;
  originalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  currencyCode: string;
  paymentStatus: string;
  isOverdue: boolean;
}

export interface InvoicePaymentSummary {
  invoiceId: string;
  openItemId?: string;
  originalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  currencyCode: string;
  paymentStatus: string;
  appliedPayments: CustomerPayment[];
}

export type CustomerPaymentPage = PageResult<CustomerPayment>;
export type ReceivablePage = PageResult<OpenReceivable>;
