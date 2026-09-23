import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { Account, AccountingPeriod, CashBankGlMapping, DocumentAttachment, FinancialClass, GeneralLedgerLine, Journal, PagedResult, PendingPostingResult, PendingTransaction, PostingProfile } from './accounting.models';

@Injectable({ providedIn: 'root' })
export class AccountingService {
  constructor(private readonly api: ApiService) {}

  listAccounts(filters: { search?: string; accountType?: string; isActive?: string } = {}): Observable<Account[]> { return this.api.get<Account[]>(`accounting/chart-of-accounts${this.query(filters)}`); }
  createAccount(input: object): Observable<Account> { return this.api.post<Account, object>('accounting/chart-of-accounts', input); }
  updateAccount(id: string, input: object): Observable<Account> { return this.api.put<Account, object>(`accounting/chart-of-accounts/${id}`, input); }
  deactivateAccount(id: string): Observable<void> { return this.api.delete(`accounting/chart-of-accounts/${id}`); }

  listClasses(isActive?: boolean): Observable<FinancialClass[]> { return this.api.get<FinancialClass[]>(`accounting/classes${isActive === undefined ? '' : `?isActive=${isActive}`}`); }
  createClass(input: object): Observable<FinancialClass> { return this.api.post<FinancialClass, object>('accounting/classes', input); }
  updateClass(id: string, input: object): Observable<FinancialClass> { return this.api.put<FinancialClass, object>(`accounting/classes/${id}`, input); }
  listPeriods(): Observable<AccountingPeriod[]> { return this.api.get<AccountingPeriod[]>('accounting/periods'); }
  createPeriod(input: object): Observable<AccountingPeriod> { return this.api.post<AccountingPeriod, object>('accounting/periods', input); }
  setPeriodStatus(id: string, status: string): Observable<AccountingPeriod> { return this.api.put<AccountingPeriod, object>(`accounting/periods/${id}/status`, { status }); }
  listProfiles(): Observable<PostingProfile[]> { return this.api.get<PostingProfile[]>('accounting/posting-profiles'); }
  createProfile(input: object): Observable<PostingProfile> { return this.api.post<PostingProfile, object>('accounting/posting-profiles', input); }
  updateProfile(id: string, input: object): Observable<PostingProfile> { return this.api.put<PostingProfile, object>(`accounting/posting-profiles/${id}`, input); }
  listCashBankMappings(): Observable<CashBankGlMapping[]> { return this.api.get<CashBankGlMapping[]>('accounting/cash-bank-mappings'); }
  setCashBankMapping(cashBankAccountId: string, postingAccountId: string | null): Observable<CashBankGlMapping> { return this.api.put<CashBankGlMapping, object>(`accounting/cash-bank-mappings/${cashBankAccountId}`, { postingAccountId }); }

  listJournals(filters: Record<string, string> = {}): Observable<PagedResult<Journal>> { return this.api.get<PagedResult<Journal>>(`accounting/journals${this.query(filters)}`); }
  getJournal(id: string): Observable<Journal> { return this.api.get<Journal>(`accounting/journals/${id}`); }
  createJournal(input: object): Observable<Journal> { return this.api.post<Journal, object>('accounting/journals', input); }
  updateJournal(id: string, input: object): Observable<Journal> { return this.api.put<Journal, object>(`accounting/journals/${id}`, input); }
  postJournal(id: string): Observable<Journal> { return this.api.post<Journal, object>(`accounting/journals/${id}/post`, {}); }
  listLedger(filters: Record<string, string> = {}): Observable<PagedResult<GeneralLedgerLine>> { return this.api.get<PagedResult<GeneralLedgerLine>>(`accounting/general-ledger${this.query(filters)}`); }
  listPending(): Observable<PendingTransaction[]> { return this.api.get<PendingTransaction[]>('accounting/pending-transactions'); }
  postPending(transactionIds: string[]): Observable<PendingPostingResult> { return this.api.post<PendingPostingResult, object>('accounting/pending-transactions/post', { transactionIds }); }

  listAttachments(documentType: string, documentId: string): Observable<DocumentAttachment[]> { return this.api.get<DocumentAttachment[]>(`documents/${documentType}/${documentId}/attachments`); }
  uploadAttachment(documentType: string, documentId: string, file: File): Observable<DocumentAttachment> { const form = new FormData(); form.append('file', file, file.name); return this.api.postForm<DocumentAttachment>(`documents/${documentType}/${documentId}/attachments`, form); }
  deleteAttachment(documentType: string, documentId: string, attachmentId: string): Observable<void> { return this.api.delete(`documents/${documentType}/${documentId}/attachments/${attachmentId}`); }
  attachmentDownloadUrl(documentType: string, documentId: string, attachmentId: string): string { return `documents/${documentType}/${documentId}/attachments/${attachmentId}/download`; }

  private query(filters: Record<string, string | undefined>): string {
    const search = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => { if (value) search.set(key, value); });
    const value = search.toString();
    return value ? `?${value}` : '';
  }
}
