import { queryString } from '../../core/services/query-string';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { PurchaseBill, PurchaseBillInput, PurchaseBillPage, PurchaseBillPaymentSummary, PurchaseDashboard, SupplierPayable, SupplierPayablePage, SupplierPayment, SupplierPaymentInput, SupplierPaymentPage, SupplierPurchaseSummary } from './purchase.models';

@Injectable({ providedIn: 'root' })
export class PurchaseService {
  constructor(private readonly api: ApiService) {}

  listBills(parameters: Record<string, string | number | boolean | undefined>): Observable<PurchaseBillPage> { return this.api.get<PurchaseBillPage>(`purchase-bills${this.query(parameters)}`); }
  getBill(id: string): Observable<PurchaseBill> { return this.api.get<PurchaseBill>(`purchase-bills/${id}`); }
  createBill(body: PurchaseBillInput): Observable<PurchaseBill> { return this.api.post<PurchaseBill, PurchaseBillInput>('purchase-bills', body); }
  updateBill(id: string, body: PurchaseBillInput): Observable<PurchaseBill> { return this.api.put<PurchaseBill, PurchaseBillInput>(`purchase-bills/${id}`, body); }
  deleteDraftBill(id: string): Observable<void> { return this.api.delete<void>(`purchase-bills/${id}`); }
  postBill(id: string): Observable<PurchaseBill> { return this.api.post<PurchaseBill, null>(`purchase-bills/${id}/post`, null); }
  getDashboard(): Observable<PurchaseDashboard> { return this.api.get<PurchaseDashboard>('purchase-bills/dashboard'); }
  getSupplierSummary(id: string): Observable<SupplierPurchaseSummary> { return this.api.get<SupplierPurchaseSummary>(`purchase-bills/suppliers/${id}/summary`); }
  getBillPaymentSummary(id: string): Observable<PurchaseBillPaymentSummary> { return this.api.get<PurchaseBillPaymentSummary>(`supplier-payments/bills/${id}/summary`); }
  listPayables(parameters: Record<string, string | number | boolean | undefined>): Observable<SupplierPayablePage> { return this.api.get<SupplierPayablePage>(`payables${this.query(parameters)}`); }
  getSupplierOpenPayables(id: string): Observable<SupplierPayable[]> { return this.api.get<SupplierPayable[]>(`supplier-payments/suppliers/${id}/open-payables`); }
  listSupplierPayments(parameters: Record<string, string | number | boolean | undefined>): Observable<SupplierPaymentPage> { return this.api.get<SupplierPaymentPage>(`supplier-payments${this.query(parameters)}`); }
  getSupplierPayment(id: string): Observable<SupplierPayment> { return this.api.get<SupplierPayment>(`supplier-payments/${id}`); }
  createSupplierPayment(body: SupplierPaymentInput): Observable<SupplierPayment> { return this.api.post<SupplierPayment, SupplierPaymentInput>('supplier-payments', body); }
  updateSupplierPayment(id: string, body: SupplierPaymentInput): Observable<SupplierPayment> { return this.api.put<SupplierPayment, SupplierPaymentInput>(`supplier-payments/${id}`, body); }
  postSupplierPayment(id: string): Observable<SupplierPayment> { return this.api.post<SupplierPayment, null>(`supplier-payments/${id}/post`, null); }

  private query(parameters: Record<string, string | number | boolean | undefined>): string {
    return queryString(parameters);
  }
}
