import { of } from 'rxjs';
import { AccountingWorkspaceComponent } from './accounting-workspace.component';

describe('AccountingWorkspaceComponent journal validation', () => {
  function createComponent(): AccountingWorkspaceComponent {
    return new AccountingWorkspaceComponent(
      {} as never,
      {} as never,
      {} as never,
      { snapshot: { data: {} } } as never,
      { currentUser: { permissions: ['journals.manage'] } } as never,
      {} as never,
      {} as never);
  }

  function line(accountId: string, debit: number, credit: number) {
    return { accountId, debit, credit, businessPartnerId: '', financialClassId: '', memo: '' };
  }

  it('rejects a zero-value journal line before sending a request', () => {
    const component = createComponent();
    component.journalDraft = {
      entryDate: '2026-08-30',
      description: 'Office expense',
      reference: '',
      lines: [line('expense', 200, 0), line('bank', 0, 200), line('extra', 0, 0)]
    };

    expect(component.journalValidationMessage).toBe('Enter a positive debit or credit amount on every journal line.');
    expect(component.journalCanSave).toBeFalse();
  });

  it('clears the opposing amount when a debit or credit is entered', () => {
    const component = createComponent();
    const journalLine = line('expense', 0, 50);

    component.setJournalAmount(journalLine, 'debit', 200);

    expect(journalLine.debit).toBe(200);
    expect(journalLine.credit).toBe(0);
  });

  it('uses the supported page size when loading journal partners', () => {
    const masterData = {
      list: jasmine.createSpy('list').and.returnValue(of({ items: [] }))
    };
    const accounting = {
      listAccounts: () => of([]),
      listClasses: () => of([]),
      listPeriods: () => of([]),
      listProfiles: () => of([]),
      listCashBankMappings: () => of([])
    };
    const payments = { listCashBankAccounts: () => of([]) };
    const component = new AccountingWorkspaceComponent(
      accounting as never,
      masterData as never,
      payments as never,
      { snapshot: { data: {} } } as never,
      { currentUser: { permissions: ['journals.manage'] } } as never,
      {} as never,
      {} as never);

    (component as any).loadSupportingData();

    expect(masterData.list).toHaveBeenCalledWith('customers', { isActive: true, pageNumber: 1, pageSize: 100 });
    expect(masterData.list).toHaveBeenCalledWith('suppliers', { isActive: true, pageNumber: 1, pageSize: 100 });
  });
});
