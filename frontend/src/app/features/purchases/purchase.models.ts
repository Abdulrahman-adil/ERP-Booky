import { PageResult } from '../master-data/master-data.models';

export type PurchaseBillStatus = 'Draft' | 'Posted' | 'Cancelled';
export type PurchasePaymentStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid';
export type PaymentStatus = 'Draft' | 'Posted' | 'Cancelled';

export interface PurchaseBillLine {
  id: string;
  lineNumber: number;
  productId: string;
  productSku: string;
  productName: string;
  description: string;
  quantity: number;
  unitOfMeasureId: string;
  unitOfMeasureName: string;
  unitCost: number;
  discountPercentage: number;
  discountAmount: number;
  netAmount: number;
  memo?: string;
}

export interface PurchaseBill {
  id: string;
  purchaseBillNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  warehouseId?: string;
  warehouseCode?: string;
  warehouseName?: string;
  invoiceDate: string;
  dueDate?: string;
  currencyCode: string;
  status: PurchaseBillStatus;
  paymentStatus?: PurchasePaymentStatus;
  subtotal: number;
  discountTotal: number;
  total: number;
  outstandingAmount: number;
  supplierReference?: string;
  notes?: string;
  createdByName?: string;
  createdAt?: string;
  postedByName?: string;
  postedAt?: string;
  lines: PurchaseBillLine[];
}

export interface PurchaseBillInput {
  supplierId: string;
  warehouseId?: string | null;
  invoiceDate: string;
  dueDate?: string | null;
  supplierReference?: string | null;
  notes?: string | null;
  lines: Array<{ productId: string; description?: string | null; quantity: number; unitCost: number; discountPercentage: number; memo?: string | null }>;
}

export interface SupplierPayable {
  openItemId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  purchaseBillId?: string;
  purchaseBillNumber: string;
  supplierReference?: string;
  billDate?: string;
  dueDate?: string;
  originalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  currencyCode: string;
  paymentStatus: PurchasePaymentStatus;
  isOverdue: boolean;
}

export interface SupplierPayment {
  id: string;
  paymentNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  cashBankAccountId: string;
  cashBankAccountName: string;
  cashBankAccountType: string;
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
  allocations: Array<{ id: string; openItemId: string; purchaseBillId?: string; purchaseBillNumber: string; supplierReference?: string; billDate?: string; dueDate?: string; originalAmount: number; outstandingAmount: number; allocationAmount: number; currencyCode: string }>;
}

export interface SupplierPaymentInput {
  supplierId: string;
  cashBankAccountId: string;
  paymentDate: string;
  amount: number;
  externalReference?: string | null;
  notes?: string | null;
  allocations: Array<{ openItemId: string; amount: number }>;
}

export interface PurchaseBillPaymentSummary {
  purchaseBillId: string;
  openItemId?: string;
  originalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  currencyCode: string;
  paymentStatus: PurchasePaymentStatus;
  appliedPayments: SupplierPayment[];
}

export interface PurchaseDashboard {
  postedPurchasesTotal: number;
  outstandingPayableTotal: number;
  currencyCode?: string;
  recentPostedBills: PurchaseBill[];
  recentSupplierPayments: SupplierPayment[];
  monthlyTotals: DashboardPeriodTotal[];
}

export interface DashboardPeriodTotal {
  periodStart: string;
  amount: number;
}

export interface SupplierPurchaseSummary {
  supplierId: string;
  totalPostedPurchases: number;
  outstandingPayable: number;
  currencyCode?: string;
  recentBills: PurchaseBill[];
  recentPayments: SupplierPayment[];
}

export type PurchaseBillPage = PageResult<PurchaseBill>;
export type SupplierPaymentPage = PageResult<SupplierPayment>;
export type SupplierPayablePage = PageResult<SupplierPayable>;
