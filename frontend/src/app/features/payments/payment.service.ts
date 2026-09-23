import { queryString } from '../../core/services/query-string';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { CashBankAccount, CustomerPayment, CustomerPaymentInput, CustomerPaymentPage, InvoicePaymentSummary, OpenReceivable, ReceivablePage } from './payment.models';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  constructor(private readonly api: ApiService) {}

  listPayments(parameters: Record<string, string | number | boolean | undefined>): Observable<CustomerPaymentPage> {
    return this.api.get<CustomerPaymentPage>(`customer-payments${this.toQuery(parameters)}`);
  }

  getPayment(id: string): Observable<CustomerPayment> { return this.api.get<CustomerPayment>(`customer-payments/${id}`); }
  createDraft(body: CustomerPaymentInput): Observable<CustomerPayment> { return this.api.post<CustomerPayment, CustomerPaymentInput>('customer-payments', body); }
  updateDraft(id: string, body: CustomerPaymentInput): Observable<CustomerPayment> { return this.api.put<CustomerPayment, CustomerPaymentInput>(`customer-payments/${id}`, body); }
  postPayment(id: string): Observable<CustomerPayment> { return this.api.post<CustomerPayment, null>(`customer-payments/${id}/post`, null); }
  getCustomerOpenReceivables(customerId: string): Observable<OpenReceivable[]> { return this.api.get<OpenReceivable[]>(`customer-payments/customers/${customerId}/open-receivables`); }
  getInvoiceSummary(invoiceId: string): Observable<InvoicePaymentSummary> { return this.api.get<InvoicePaymentSummary>(`customer-payments/invoices/${invoiceId}/summary`); }
  listReceivables(parameters: Record<string, string | number | boolean | undefined>): Observable<ReceivablePage> { return this.api.get<ReceivablePage>(`receivables${this.toQuery(parameters)}`); }
  listCashBankAccounts(): Observable<CashBankAccount[]> { return this.api.get<CashBankAccount[]>('cash-bank-accounts'); }
  createCashBankAccount(body: { name: string; accountType: string }): Observable<CashBankAccount> { return this.api.post<CashBankAccount, { name: string; accountType: string }>('cash-bank-accounts', body); }
  updateCashBankAccount(id: string, body: { name: string; isActive: boolean }): Observable<CashBankAccount> { return this.api.put<CashBankAccount, { name: string; isActive: boolean }>(`cash-bank-accounts/${id}`, body); }

  private toQuery(parameters: Record<string, string | number | boolean | undefined>): string {
    return queryString(parameters);
  }
}
