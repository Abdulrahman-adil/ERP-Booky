import { formatAmount, utcDateInput } from '../../shared/utils/display-format';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { DocumentAttachmentsComponent } from '../../shared/document-attachments/document-attachments.component';
import { SearchSelectComponent, SearchSelectOption } from '../../shared/search-select/search-select.component';
import { Partner } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { CashBankAccount } from '../payments/payment.models';
import { PaymentService } from '../payments/payment.service';
import { AccountingService } from './accounting.service';
import { Account, AccountRole, AccountType, AccountingPeriod, CashBankGlMapping, FinancialClass, GeneralLedgerLine, Journal, JournalLine, PendingTransaction, PostingProfile, postingKeys } from './accounting.models';

type WorkspaceView = 'accounts' | 'journals' | 'ledger' | 'setup' | 'pending';

@Component({
  selector: 'app-accounting-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, DocumentAttachmentsComponent, SearchSelectComponent],
  templateUrl: './accounting-workspace.component.html',
  styleUrl: './accounting-workspace.component.scss'
})
export class AccountingWorkspaceComponent implements OnInit {
  readonly views: Array<{ id: WorkspaceView; label: string }> = [
    { id: 'accounts', label: 'Chart of accounts' }, { id: 'journals', label: 'Journals' }, { id: 'ledger', label: 'General ledger' }, { id: 'setup', label: 'Setup' }, { id: 'pending', label: 'Pending posting' }
  ];
  readonly accountTypes: AccountType[] = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'];
  readonly accountRoles: AccountRole[] = ['None', 'Cash', 'Bank', 'AccountsReceivable', 'AccountsPayable', 'Inventory', 'SalesRevenue', 'OtherIncome', 'CostOfGoodsSold', 'Expense', 'OtherExpense', 'Equity', 'RetainedEarnings', 'CurrentYearEarnings'];
  readonly postingKeys = postingKeys;

  activeView: WorkspaceView = 'accounts';
  accounts: Account[] = [];
  classes: FinancialClass[] = [];
  periods: AccountingPeriod[] = [];
  profiles: PostingProfile[] = [];
  cashBankMappings: CashBankGlMapping[] = [];
  cashBankAccounts: CashBankAccount[] = [];
  journals: Journal[] = [];
  journalTotal = 0;
  ledger: GeneralLedgerLine[] = [];
  ledgerTotal = 0;
  pending: PendingTransaction[] = [];
  customers: Partner[] = [];
  suppliers: Partner[] = [];
  message = '';
  errorMessage = '';
  isLoading = true;
  isSaving = false;
  showAccountForm = false;
  showJournalForm = false;
  showSetupForms = false;
  showCashBankForm = false;
  editingAccountId = '';
  editingJournal: Journal | null = null;
  selectedPendingIds = new Set<string>();
  expandedAccountIds = new Set<string>();

  accountFilters = { search: '', accountType: '', isActive: 'true' };
  journalFilters = { search: '', status: '', fromDate: '', toDate: '' };
  ledgerFilters = { accountId: '', sourceModule: '', fromDate: '', toDate: '', reference: '' };
  accountDraft = this.newAccountDraft();
  classDraft = { code: '', name: '', isActive: true };
  periodDraft = { name: '', startDate: this.monthStart(), endDate: this.monthEnd(), status: 'Open' };
  profileDraft = { id: '', name: 'Default operational profile', isActive: true, mappings: {} as Record<string, string> };
  cashBankDraft = { id: '', name: '', accountType: 'Bank', isActive: true, originalIsActive: true };
  journalDraft = this.newJournalDraft();

  constructor(private readonly accounting: AccountingService, private readonly masterData: MasterDataService, private readonly payments: PaymentService, private readonly route: ActivatedRoute, readonly auth: AuthService, private readonly confirmation: ConfirmDialogService, private readonly feedback: FeedbackService) {}

  get canConfigure(): boolean { return this.auth.currentUser?.permissions.includes('accountingconfiguration.manage') === true; }
  get canManageAccounts(): boolean { return this.auth.currentUser?.permissions.includes('chartofaccounts.manage') === true; }
  get canManageJournals(): boolean { return this.auth.currentUser?.permissions.includes('journals.manage') === true; }
  get canPostJournals(): boolean { return this.auth.currentUser?.permissions.includes('journals.post') === true; }
  get canManageCashBank(): boolean { return this.auth.currentUser?.permissions.includes('cash-bank.manage') === true; }
  get postingAccounts(): Account[] { return this.accounts.filter(account => account.isActive && account.isPosting); }
  get postingAccountOptions(): SearchSelectOption[] { return this.postingAccounts.map(account => ({ id: account.id, label: `${account.code} — ${account.name}`, keywords: `${account.code} ${account.name} ${account.accountRole}` })); }
  get classOptions(): SearchSelectOption[] { return this.classes.map(financialClass => ({ id: financialClass.id, label: `${financialClass.code} — ${financialClass.name}`, keywords: `${financialClass.code} ${financialClass.name}` })); }
  get visibleAccounts(): Account[] {
    const accountsByParent = new Map<string, Account[]>();
    const accountIds = new Set(this.accounts.map(account => account.id));
    const roots: Account[] = [];

    for (const account of this.accounts) {
      if (!account.parentAccountId || !accountIds.has(account.parentAccountId)) {
        roots.push(account);
        continue;
      }

      const children = accountsByParent.get(account.parentAccountId) || [];
      children.push(account);
      accountsByParent.set(account.parentAccountId, children);
    }

    const appendVisible = (account: Account): Account[] => {
      const children = (accountsByParent.get(account.id) || []).sort((left, right) => left.code.localeCompare(right.code));
      return [account, ...(this.expandedAccountIds.has(account.id) ? children.flatMap(appendVisible) : [])];
    };

    return roots.sort((left, right) => left.code.localeCompare(right.code)).flatMap(appendVisible);
  }
  get journalDebits(): number { return this.journalDraft.lines.reduce((sum, line) => sum + (Number(line.debit) || 0), 0); }
  get journalCredits(): number { return this.journalDraft.lines.reduce((sum, line) => sum + (Number(line.credit) || 0), 0); }
  get journalDifference(): number { return Math.abs(this.journalDebits - this.journalCredits); }
  get journalBalanced(): boolean { return this.journalDebits > 0 && this.journalCredits > 0 && Math.abs(this.journalDebits - this.journalCredits) < 0.000001; }
  get journalValidationMessage(): string {
    if (!this.journalDraft.description.trim()) return 'Enter a description.';
    if (this.journalDraft.lines.length < 2) return 'Add at least two journal lines.';
    if (this.journalDraft.lines.some(line => !line.accountId)) return 'Select a posting account on every line.';
    if (this.journalDraft.lines.some(line => this.accountRequiresPartner(line) && !line.businessPartnerId)) return 'Select the required customer or supplier for each AR or AP line.';
    if (this.journalDraft.lines.some(line => Number(line.debit) > 0 && Number(line.credit) > 0)) return 'A journal line can contain either a debit or a credit, not both.';
    if (this.journalDraft.lines.some(line => Number(line.debit) <= 0 && Number(line.credit) <= 0)) return 'Enter a positive debit or credit amount on every journal line.';
    if (this.journalDebits <= 0 || this.journalCredits <= 0) return 'Enter non-zero debit and credit totals.';
    if (!this.journalBalanced) return 'Debit and credit totals must match.';
    return '';
  }
  get journalIsValid(): boolean { return this.journalValidationMessage.length === 0; }
  get journalCanSave(): boolean { return this.canManageJournals && !this.isSaving && this.journalIsValid; }
  get activeProfile(): PostingProfile | undefined { return this.profiles.find(profile => profile.isActive); }

  ngOnInit(): void {
    this.activeView = (this.route.snapshot.data['initialView'] as WorkspaceView | undefined) || 'accounts';
    this.loadSupportingData();
    this.loadAccounts();
    this.loadJournals();
    this.loadLedger();
    this.loadPending();
  }

  selectView(view: WorkspaceView): void { this.activeView = view; this.message = ''; this.errorMessage = ''; }

  loadAccounts(): void {
    this.accounting.listAccounts(this.accountFilters).subscribe({
      next: accounts => { this.setAccounts(accounts); this.isLoading = false; },
      error: () => { this.errorMessage = 'The chart of accounts could not be loaded.'; this.isLoading = false; }
    });
  }

  clearAccountFilters(): void { this.accountFilters = { search: '', accountType: '', isActive: 'true' }; this.loadAccounts(); }

  editAccount(account: Account): void {
    if (!this.canManageAccounts) return;
    this.editingAccountId = account.id;
    this.accountDraft = { code: account.code, name: account.name, accountType: account.accountType, accountRole: account.accountRole, normalBalanceOverride: account.normalBalanceOverride || '', isPosting: account.isPosting, parentAccountId: account.parentAccountId || '', isActive: account.isActive };
    this.showAccountForm = true;
  }

  newAccount(parent?: Account): void {
    this.editingAccountId = '';
    this.accountDraft = parent
      ? { ...this.newAccountDraft(), accountType: parent.accountType, parentAccountId: parent.id }
      : this.newAccountDraft();
    this.showAccountForm = true;
  }

  toggleAccount(account: Account): void {
    if (!this.hasAccountChildren(account)) return;
    this.expandedAccountIds.has(account.id) ? this.expandedAccountIds.delete(account.id) : this.expandedAccountIds.add(account.id);
  }

  hasAccountChildren(account: Account): boolean { return this.accounts.some(candidate => candidate.parentAccountId === account.id); }
  accountCanBeParent(account: Account): boolean {
    return !account.isPosting
      && account.accountType === this.accountDraft.accountType
      && account.id !== this.editingAccountId
      && !this.isAccountDescendantOfEditingAccount(account);
  }

  saveAccount(): void {
    if (!this.canManageAccounts || this.isSaving) return;
    this.errorMessage = ''; this.message = '';
    if (!this.accountDraft.code.trim() || !this.accountDraft.name.trim()) { this.errorMessage = 'Account code and name are required.'; return; }
    const input = { ...this.accountDraft, code: this.accountDraft.code.trim(), name: this.accountDraft.name.trim(), parentAccountId: this.accountDraft.parentAccountId || null, normalBalanceOverride: this.accountDraft.normalBalanceOverride || null };
    this.isSaving = true;
    const request = this.editingAccountId ? this.accounting.updateAccount(this.editingAccountId, input) : this.accounting.createAccount(input);
    request.subscribe({
      next: () => { this.message = this.editingAccountId ? 'Account updated.' : 'Account created.'; this.showAccountForm = false; this.isSaving = false; this.loadAccounts(); this.loadSupportingData(); },
      error: error => { this.errorMessage = error.error?.title || 'The account could not be saved.'; this.isSaving = false; }
    });
  }

  deactivateAccount(account: Account): void {
    if (!this.canManageAccounts) return;
    void this.confirmation.confirm({ title: `Deactivate ${account.code} — ${account.name}?`, message: 'This account remains in financial history but cannot be selected for future postings.', confirmLabel: 'Deactivate account', tone: 'warning' }).then(confirmed => {
      if (!confirmed) return;
      this.accounting.deactivateAccount(account.id).subscribe({ next: () => { this.message = 'Account deactivated.'; this.feedback.success(this.message); this.loadAccounts(); }, error: error => { this.errorMessage = error.error?.title || 'The account cannot be deactivated.'; this.feedback.error(this.errorMessage); } });
    });
  }

  loadJournals(): void { this.accounting.listJournals(this.journalFilters).subscribe({ next: result => { this.journals = result.items; this.journalTotal = result.totalCount; }, error: () => this.errorMessage = 'Journals could not be loaded.' }); }
  clearJournalFilters(): void { this.journalFilters = { search: '', status: '', fromDate: '', toDate: '' }; this.loadJournals(); }

  openJournal(journal: Journal): void {
    this.editingJournal = journal;
    this.journalDraft = { entryDate: journal.entryDate, description: journal.description, reference: journal.reference || '', lines: journal.lines.map(line => ({ accountId: line.accountId, businessPartnerId: line.businessPartnerId || '', financialClassId: line.financialClassId || '', debit: line.debit, credit: line.credit, memo: line.memo || '' })) };
    this.showJournalForm = true;
  }

  newJournal(): void { this.editingJournal = null; this.journalDraft = this.newJournalDraft(); this.errorMessage = ''; this.message = ''; this.showJournalForm = true; }
  addJournalLine(): void { this.journalDraft.lines.push({ accountId: '', businessPartnerId: '', financialClassId: '', debit: 0, credit: 0, memo: '' }); }
  removeJournalLine(index: number): void { if (this.journalDraft.lines.length > 2) this.journalDraft.lines.splice(index, 1); }
  setJournalAmount(line: JournalLine, side: 'debit' | 'credit', value: number | string): void {
    const amount = Number(value) || 0;
    line[side] = amount;
    if (amount > 0) line[side === 'debit' ? 'credit' : 'debit'] = 0;
  }
  onJournalAccountChange(line: JournalLine): void {
    if (!this.accountRequiresPartner(line)) line.businessPartnerId = '';
  }

  accountRequiresPartner(line: JournalLine): boolean {
    const role = this.accounts.find(account => account.id === line.accountId)?.accountRole;
    return role === 'AccountsReceivable' || role === 'AccountsPayable';
  }

  partnersFor(line: JournalLine): Partner[] {
    return this.accounts.find(account => account.id === line.accountId)?.accountRole === 'AccountsPayable' ? this.suppliers : this.customers;
  }

  partnerOptionsFor(line: JournalLine): SearchSelectOption[] {
    return this.partnersFor(line).map(partner => ({ id: partner.id, label: `${partner.code} — ${partner.legalName}`, keywords: `${partner.code} ${partner.legalName}` }));
  }

  partnerLabelFor(line: JournalLine): string {
    return this.accounts.find(account => account.id === line.accountId)?.accountRole === 'AccountsPayable' ? 'Supplier' : 'Customer';
  }

  saveJournal(postAfterSave = false): void {
    if (!this.canManageJournals || this.isSaving) return;
    this.errorMessage = ''; this.message = '';
    if (!this.journalIsValid) { this.errorMessage = this.journalValidationMessage; return; }
    const input = {
      entryDate: this.journalDraft.entryDate,
      description: this.journalDraft.description.trim(),
      reference: this.journalDraft.reference.trim() || null,
      lines: this.journalDraft.lines.map(line => ({ accountId: line.accountId, debit: Number(line.debit) || 0, credit: Number(line.credit) || 0, businessPartnerId: line.businessPartnerId || null, financialClassId: line.financialClassId || null, memo: line.memo?.trim() || null }))
    };
    this.isSaving = true;
    const request = this.editingJournal ? this.accounting.updateJournal(this.editingJournal.id, input) : this.accounting.createJournal(input);
    request.subscribe({
      next: journal => {
        this.editingJournal = journal;
        if (postAfterSave) { this.postJournal(journal); return; }
        this.message = 'Draft journal saved.'; this.isSaving = false; this.loadJournals();
      },
      error: error => { this.errorMessage = error.error?.title || 'The journal could not be saved.'; this.isSaving = false; }
    });
  }

  postJournal(journal: Journal): void {
    if (!this.canPostJournals) { this.isSaving = false; return; }
    void this.confirmation.confirm({ title: `Post ${journal.journalNumber}?`, message: 'Once posted, this balanced journal affects the General Ledger and cannot be edited directly.', confirmLabel: 'Post journal' }).then(confirmed => {
      if (!confirmed) { this.isSaving = false; return; }
      this.accounting.postJournal(journal.id).subscribe({ next: posted => { this.editingJournal = posted; this.message = `${posted.journalNumber} posted to the general ledger.`; this.feedback.financialSuccess(this.message); this.isSaving = false; this.loadJournals(); this.loadLedger(); }, error: error => { this.errorMessage = error.error?.title || 'The journal could not be posted.'; this.isSaving = false; this.feedback.error(this.errorMessage); } });
    });
  }

  loadLedger(): void { this.accounting.listLedger(this.ledgerFilters).subscribe({ next: result => { this.ledger = result.items; this.ledgerTotal = result.totalCount; }, error: () => this.errorMessage = 'General ledger entries could not be loaded.' }); }
  clearLedgerFilters(): void { this.ledgerFilters = { accountId: '', sourceModule: '', fromDate: '', toDate: '', reference: '' }; this.loadLedger(); }

  saveClass(): void {
    if (!this.canConfigure || !this.classDraft.code.trim() || !this.classDraft.name.trim()) return;
    this.accounting.createClass({ code: this.classDraft.code.trim(), name: this.classDraft.name.trim(), isActive: this.classDraft.isActive }).subscribe({
      next: () => { this.classDraft = { code: '', name: '', isActive: true }; this.message = 'Class created.'; this.loadSupportingData(); },
      error: error => this.errorMessage = error.error?.title || 'The class could not be saved.'
    });
  }

  savePeriod(): void {
    if (!this.canConfigure || !this.periodDraft.name.trim()) return;
    this.accounting.createPeriod({ ...this.periodDraft, name: this.periodDraft.name.trim() }).subscribe({
      next: () => { this.message = 'Accounting period created.'; this.loadSupportingData(); },
      error: error => this.errorMessage = error.error?.title || 'The accounting period could not be saved.'
    });
  }

  setPeriodStatus(period: AccountingPeriod, status: 'Open' | 'Closed'): void {
    if (!this.canConfigure || period.status === status) return;
    const action = status === 'Closed' ? 'Close' : 'Reopen';
    void this.confirmation.confirm({ title: `${action} ${period.name}?`, message: status === 'Closed' ? 'Closing prevents future posting in this period. Historical financial data remains unchanged.' : 'Reopening makes this period available for posting again.', confirmLabel: `${action} period`, tone: status === 'Closed' ? 'warning' : 'primary' }).then(confirmed => {
      if (!confirmed) return;
      this.accounting.setPeriodStatus(period.id, status).subscribe({ next: () => { this.message = `Period ${status.toLowerCase()}.`; this.feedback.success(this.message); this.loadSupportingData(); }, error: error => { this.errorMessage = error.error?.title || 'The period could not be updated.'; this.feedback.error(this.errorMessage); } });
    });
  }

  editProfile(profile?: PostingProfile): void {
    const mappings = Object.fromEntries((profile?.mappings || []).map(mapping => [mapping.postingKey, mapping.accountId]));
    this.profileDraft = { id: profile?.id || '', name: profile?.name || 'Default operational profile', isActive: profile?.isActive ?? true, mappings };
    this.showSetupForms = true;
  }

  saveProfile(): void {
    if (!this.canConfigure || !this.profileDraft.name.trim()) return;
    const mappings = this.postingKeys.filter(key => this.profileDraft.mappings[key.key]).map(key => ({ postingKey: key.key, accountId: this.profileDraft.mappings[key.key] }));
    const input = { name: this.profileDraft.name.trim(), isActive: this.profileDraft.isActive, mappings };
    const request = this.profileDraft.id ? this.accounting.updateProfile(this.profileDraft.id, input) : this.accounting.createProfile(input);
    request.subscribe({ next: () => { this.message = 'Posting profile saved.'; this.showSetupForms = false; this.loadSupportingData(); }, error: error => this.errorMessage = error.error?.title || 'The posting profile could not be saved.' });
  }

  setCashBankMapping(mapping: CashBankGlMapping, event: Event): void {
    const postingAccountId = (event.target as HTMLSelectElement).value || null;
    this.accounting.setCashBankMapping(mapping.cashBankAccountId, postingAccountId).subscribe({ next: () => { this.message = 'Cash or bank GL mapping saved.'; this.loadSupportingData(); }, error: error => this.errorMessage = error.error?.title || 'The cash or bank mapping could not be saved.' });
  }

  beginCashBankCreate(): void {
    this.cashBankDraft = { id: '', name: '', accountType: 'Bank', isActive: true, originalIsActive: true };
    this.showCashBankForm = true;
  }

  editCashBank(account: CashBankAccount): void {
    this.cashBankDraft = { id: account.id, name: account.name, accountType: account.accountType, isActive: account.isActive, originalIsActive: account.isActive };
    this.showCashBankForm = true;
  }

  saveCashBank(): void {
    if (!this.canManageCashBank || !this.cashBankDraft.name.trim()) return;
    if (this.cashBankDraft.id && this.cashBankDraft.isActive !== this.cashBankDraft.originalIsActive) {
      const action = this.cashBankDraft.isActive ? 'activate' : 'deactivate';
      void this.confirmation.confirm({ title: `${action[0].toUpperCase()}${action.slice(1)} cash / bank account?`, message: this.cashBankDraft.isActive ? 'This account becomes available for new payments.' : 'Existing payment history stays intact, but this account cannot be used for new payments.', confirmLabel: `${action[0].toUpperCase()}${action.slice(1)} account`, tone: this.cashBankDraft.isActive ? 'primary' : 'warning' }).then(confirmed => {
        if (confirmed) this.persistCashBank();
      });
      return;
    }
    this.persistCashBank();
  }

  private persistCashBank(): void {
    const request = this.cashBankDraft.id
      ? this.payments.updateCashBankAccount(this.cashBankDraft.id, { name: this.cashBankDraft.name.trim(), isActive: this.cashBankDraft.isActive })
      : this.payments.createCashBankAccount({ name: this.cashBankDraft.name.trim(), accountType: this.cashBankDraft.accountType });
    request.subscribe({
      next: () => { this.message = this.cashBankDraft.id ? 'Cash or bank account updated.' : 'Cash or bank account created. Map its posting account before posting payments.'; this.showCashBankForm = false; this.loadSupportingData(); },
      error: error => this.errorMessage = error.error?.title || 'The cash or bank account could not be saved.'
    });
  }

  togglePending(id: string, checked: boolean): void { checked ? this.selectedPendingIds.add(id) : this.selectedPendingIds.delete(id); }
  postSelectedPending(): void {
    if (!this.canConfigure || !this.selectedPendingIds.size) return;
    const count = this.selectedPendingIds.size;
    void this.confirmation.confirm({ title: `Post ${count} pending transaction${count === 1 ? '' : 's'}?`, message: 'Each transaction will be validated against the current posting profile and accounting period before it affects the General Ledger.', confirmLabel: 'Post selected transactions' }).then(confirmed => {
      if (!confirmed) return;
      this.accounting.postPending([...this.selectedPendingIds]).subscribe({ next: result => { this.message = `${result.postedCount} transaction(s) posted.${result.failures.length ? ` ${result.failures.length} could not be posted.` : ''}`; this.errorMessage = result.failures.join(' '); this.selectedPendingIds.clear(); if (result.postedCount) this.feedback.financialSuccess(this.message); this.loadPending(); this.loadJournals(); this.loadLedger(); }, error: error => { this.errorMessage = error.error?.title || 'The selected transactions could not be posted.'; this.feedback.error(this.errorMessage); } });
    });
  }

  formatAmount(value: number): string { return formatAmount(value || 0); }
  accountIndent(account: Account): string { return `${Math.min(account.hierarchyLevel, 5) * 1.25}rem`; }
  trackIndex(index: number): number { return index; }

  private loadSupportingData(): void {
    this.accounting.listAccounts({ isActive: 'true' }).subscribe({ next: accounts => this.setAccounts(accounts) });
    this.accounting.listClasses(true).subscribe({ next: classes => this.classes = classes });
    this.accounting.listPeriods().subscribe({ next: periods => this.periods = periods });
    this.accounting.listProfiles().subscribe({ next: profiles => this.profiles = profiles });
    this.accounting.listCashBankMappings().subscribe({ next: mappings => this.cashBankMappings = mappings });
    this.payments.listCashBankAccounts().subscribe({ next: accounts => this.cashBankAccounts = accounts });
    this.masterData.list<Partner>('customers', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.customers = result.items });
    this.masterData.list<Partner>('suppliers', { isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({ next: result => this.suppliers = result.items });
  }

  private loadPending(): void { this.accounting.listPending().subscribe({ next: pending => this.pending = pending, error: () => this.errorMessage = 'Pending accounting work could not be loaded.' }); }
  private newAccountDraft() { return { code: '', name: '', accountType: 'Asset' as AccountType, accountRole: 'None' as AccountRole, normalBalanceOverride: '', isPosting: true, parentAccountId: '', isActive: true }; }
  private newJournalDraft() { return { entryDate: this.sensibleJournalDate(), description: '', reference: '', lines: [{ accountId: '', businessPartnerId: '', financialClassId: '', debit: 0, credit: 0, memo: '' }, { accountId: '', businessPartnerId: '', financialClassId: '', debit: 0, credit: 0, memo: '' }] }; }
  private setAccounts(accounts: Account[]): void {
    const knownIds = new Set(this.accounts.map(account => account.id));
    this.accounts = accounts;
    for (const account of accounts) {
      if (!account.isPosting && !knownIds.has(account.id)) this.expandedAccountIds.add(account.id);
    }
  }
  private isAccountDescendantOfEditingAccount(account: Account): boolean {
    if (!this.editingAccountId) return false;
    let parentId = account.parentAccountId;
    while (parentId) {
      if (parentId === this.editingAccountId) return true;
      parentId = this.accounts.find(candidate => candidate.id === parentId)?.parentAccountId || null;
    }
    return false;
  }
  private sensibleJournalDate(): string {
    const today = this.today();
    const openPeriod = this.periods.find(period => period.status === 'Open' && period.startDate <= today && period.endDate >= today)
      || this.periods.find(period => period.status === 'Open');
    if (!openPeriod) return today;
    if (today < openPeriod.startDate) return openPeriod.startDate;
    if (today > openPeriod.endDate) return openPeriod.endDate;
    return today;
  }
  private today(): string { return utcDateInput(); }
  private monthStart(): string { const date = new Date(); return new Date(date.getFullYear(), date.getMonth(), 1).toISOString().slice(0, 10); }
  private monthEnd(): string { const date = new Date(); return new Date(date.getFullYear(), date.getMonth() + 1, 0).toISOString().slice(0, 10); }
}
