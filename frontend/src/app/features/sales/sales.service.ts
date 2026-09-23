import { queryString } from '../../core/services/query-string';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { CustomerSalesSummary, SalesDashboard, SalesInvoice, SalesInvoiceInput, SalesInvoicePage, SalesInvoiceStatus } from './sales.models';

@Injectable({ providedIn: 'root' })
export class SalesService {
  constructor(private readonly api: ApiService) {}

  listInvoices(parameters: SalesInvoiceQuery): Observable<SalesInvoicePage> {
    return this.api.get<SalesInvoicePage>(`sales-invoices${this.toQuery(parameters)}`);
  }

  getInvoice(id: string): Observable<SalesInvoice> {
    return this.api.get<SalesInvoice>(`sales-invoices/${id}`);
  }

  createDraft(body: SalesInvoiceInput): Observable<SalesInvoice> {
    return this.api.post<SalesInvoice, SalesInvoiceInput>('sales-invoices', body);
  }

  updateDraft(id: string, body: SalesInvoiceInput): Observable<SalesInvoice> {
    return this.api.put<SalesInvoice, SalesInvoiceInput>(`sales-invoices/${id}`, body);
  }

  postInvoice(id: string): Observable<SalesInvoice> {
    return this.api.post<SalesInvoice, null>(`sales-invoices/${id}/post`, null);
  }

  getCustomerSummary(customerId: string): Observable<CustomerSalesSummary> {
    return this.api.get<CustomerSalesSummary>(`sales-invoices/customers/${customerId}/summary`);
  }

  getDashboard(): Observable<SalesDashboard> {
    return this.api.get<SalesDashboard>('sales-invoices/dashboard');
  }

  private toQuery(parameters: SalesInvoiceQuery): string {
    return queryString(parameters);
  }
}

export interface SalesInvoiceQuery {
  search?: string;
  customerId?: string;
  status?: SalesInvoiceStatus | '';
  fromDate?: string;
  toDate?: string;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: 'number' | 'dueDate' | 'invoiceDate' | '';
  sortDescending?: boolean;
}
