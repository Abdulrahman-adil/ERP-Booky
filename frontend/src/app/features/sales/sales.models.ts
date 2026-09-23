import { PageResult } from '../master-data/master-data.models';

export type SalesInvoiceStatus = 'Draft' | 'Posted' | 'Cancelled';
export type SalesInvoicePaymentStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid';

export interface SalesInvoiceLine {
  id: string;
  lineNumber: number;
  productId: string;
  productSku: string;
  productName: string;
  description: string;
  quantity: number;
  unitOfMeasureId: string;
  unitOfMeasureName: string;
  unitPrice: number;
  discountPercentage: number;
  discountAmount: number;
  netAmount: number;
}

export interface SalesInvoice {
  id: string;
  invoiceNumber: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  warehouseId?: string;
  warehouseCode?: string;
  warehouseName?: string;
  invoiceDate: string;
  dueDate?: string;
  currencyCode: string;
  status: SalesInvoiceStatus;
  paymentStatus?: SalesInvoicePaymentStatus;
  subtotal: number;
  discountTotal: number;
  total: number;
  outstandingAmount: number;
  reference?: string;
  notes?: string;
  createdByName?: string;
  createdAt?: string;
  postedByName?: string;
  postedAt?: string;
  lines: SalesInvoiceLine[];
}

export interface SalesInvoiceLineInput {
  productId: string;
  description?: string | null;
  quantity: number;
  unitPrice: number;
  discountPercentage: number;
}

export interface SalesInvoiceInput {
  customerId: string;
  warehouseId?: string | null;
  invoiceDate: string;
  dueDate?: string | null;
  reference?: string | null;
  notes?: string | null;
  lines: SalesInvoiceLineInput[];
}

export interface CustomerSalesSummary {
  customerId: string;
  totalPostedSales: number;
  outstandingReceivable: number;
  currencyCode?: string;
  recentInvoices: SalesInvoice[];
}

export interface SalesDashboard {
  postedSalesTotal: number;
  outstandingReceivableTotal: number;
  currencyCode?: string;
  recentPostedInvoices: SalesInvoice[];
  monthlyTotals: DashboardPeriodTotal[];
}

export interface DashboardPeriodTotal {
  periodStart: string;
  amount: number;
}

export type SalesInvoicePage = PageResult<SalesInvoice>;
