using Erp.Application.MasterData;
using Erp.Domain.Accounting;

namespace Erp.Application.Accounting;

public static class PostingKeys
{
    public const string SalesReceivable = "SALES_RECEIVABLE";
    public const string SalesRevenue = "SALES_REVENUE";
    public const string CustomerPaymentReceivable = "CUSTOMER_PAYMENT_RECEIVABLE";
    public const string PurchaseInventory = "PURCHASE_INVENTORY";
    public const string PurchasePayable = "PURCHASE_PAYABLE";
    public const string SupplierPaymentPayable = "SUPPLIER_PAYMENT_PAYABLE";

    public static readonly IReadOnlyCollection<string> All =
    [
        SalesReceivable,
        SalesRevenue,
        CustomerPaymentReceivable,
        PurchaseInventory,
        PurchasePayable,
        SupplierPaymentPayable
    ];
}

public sealed record AccountInput(string Code, string Name, AccountType AccountType, AccountRole AccountRole, bool IsPosting, Guid? ParentAccountId = null, bool IsActive = true, AccountNormalBalance? NormalBalanceOverride = null);

public sealed record AccountDto(Guid Id, string Code, string Name, AccountType AccountType, AccountRole AccountRole, AccountNormalBalance DefaultNormalBalance, AccountNormalBalance? NormalBalanceOverride, AccountNormalBalance NormalBalance, bool IsContraAccount, Guid? ParentAccountId, int HierarchyLevel, bool IsPosting, bool IsActive, decimal Balance);

public sealed record FinancialClassInput(string Code, string Name, bool IsActive = true);

public sealed record FinancialClassDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record AccountingPeriodInput(string Name, DateOnly StartDate, DateOnly EndDate, AccountingPeriodStatus Status = AccountingPeriodStatus.Open);

public sealed record AccountingPeriodDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, AccountingPeriodStatus Status);

public sealed record PostingProfileMappingInput(string PostingKey, Guid AccountId);

public sealed record PostingProfileInput(string Name, bool IsActive, IReadOnlyCollection<PostingProfileMappingInput> Mappings);

public sealed record PostingProfileMappingDto(string PostingKey, Guid AccountId, string AccountCode, string AccountName);

public sealed record PostingProfileDto(Guid Id, string Name, bool IsActive, IReadOnlyCollection<PostingProfileMappingDto> Mappings);

public sealed record CashBankGlMappingDto(Guid CashBankAccountId, string CashBankAccountName, string AccountType, Guid? PostingAccountId, string? PostingAccountCode, string? PostingAccountName);

public sealed record JournalEntryLineInput(Guid AccountId, decimal Debit, decimal Credit, Guid? BusinessPartnerId = null, Guid? FinancialClassId = null, string? Memo = null);

public sealed record JournalEntryInput(DateOnly EntryDate, string Description, string? Reference, IReadOnlyCollection<JournalEntryLineInput> Lines);

public sealed record JournalEntryLineDto(Guid Id, int LineNumber, Guid AccountId, string AccountCode, string AccountName, Guid? BusinessPartnerId, string? BusinessPartnerName, Guid? FinancialClassId, string? FinancialClassName, decimal Debit, decimal Credit, string? Memo);

public sealed record JournalEntryDto(Guid Id, string JournalNumber, DateOnly EntryDate, string Description, string? Reference, string SourceModule, string SourceDocumentType, Guid SourceDocumentId, JournalEntryStatus Status, decimal TotalDebit, decimal TotalCredit, Guid? CreatedByUserId, DateTimeOffset? CreatedAt, Guid? PostedByUserId, DateTimeOffset? PostedAt, IReadOnlyCollection<JournalEntryLineDto> Lines);

public sealed record JournalEntryQuery(string? Search, JournalEntryStatus? Status, DateOnly? FromDate, DateOnly? ToDate, int PageNumber = 1, int PageSize = 50);

public sealed record GeneralLedgerQuery(Guid? AccountId, Guid? BusinessPartnerId, Guid? FinancialClassId, string? SourceModule, string? Reference, DateOnly? FromDate, DateOnly? ToDate, int PageNumber = 1, int PageSize = 100);

public sealed record GeneralLedgerLineDto(Guid Id, DateOnly Date, string JournalNumber, Guid AccountId, string AccountCode, string AccountName, string Description, string? BusinessPartnerName, string? FinancialClassName, decimal Debit, decimal Credit, decimal RunningBalance, string SourceModule, string SourceDocumentType, Guid SourceDocumentId, string? Reference);

public sealed record PendingAccountingTransactionDto(Guid Id, DateOnly TransactionDate, string SourceModule, string SourceDocumentType, Guid SourceDocumentId, string Status, string? SourceDocumentNumber);

public sealed record PendingPostingResult(int PostedCount, IReadOnlyCollection<string> Failures);

public interface IAccountingService
{
    Task<IReadOnlyCollection<AccountDto>> GetAccountsAsync(Guid companyId, string? search, AccountType? accountType, bool? isActive, CancellationToken cancellationToken = default);
    Task<AccountDto> CreateAccountAsync(Guid companyId, AccountInput input, CancellationToken cancellationToken = default);
    Task<AccountDto?> UpdateAccountAsync(Guid companyId, Guid id, AccountInput input, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAccountAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FinancialClassDto>> GetFinancialClassesAsync(Guid companyId, bool? isActive, CancellationToken cancellationToken = default);
    Task<FinancialClassDto> CreateFinancialClassAsync(Guid companyId, FinancialClassInput input, CancellationToken cancellationToken = default);
    Task<FinancialClassDto?> UpdateFinancialClassAsync(Guid companyId, Guid id, FinancialClassInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AccountingPeriodDto>> GetPeriodsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<AccountingPeriodDto> CreatePeriodAsync(Guid companyId, AccountingPeriodInput input, CancellationToken cancellationToken = default);
    Task<AccountingPeriodDto?> SetPeriodStatusAsync(Guid companyId, Guid id, AccountingPeriodStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostingProfileDto>> GetPostingProfilesAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<PostingProfileDto> SavePostingProfileAsync(Guid companyId, Guid? id, PostingProfileInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CashBankGlMappingDto>> GetCashBankMappingsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CashBankGlMappingDto?> SetCashBankMappingAsync(Guid companyId, Guid cashBankAccountId, Guid? postingAccountId, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryDto>> GetJournalsAsync(Guid companyId, JournalEntryQuery query, CancellationToken cancellationToken = default);
    Task<JournalEntryDto?> GetJournalAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> CreateManualJournalAsync(Guid companyId, Guid userId, JournalEntryInput input, CancellationToken cancellationToken = default);
    Task<JournalEntryDto?> UpdateManualJournalAsync(Guid companyId, Guid id, JournalEntryInput input, CancellationToken cancellationToken = default);
    Task<JournalEntryDto?> PostJournalAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<PagedResult<GeneralLedgerLineDto>> GetGeneralLedgerAsync(Guid companyId, GeneralLedgerQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PendingAccountingTransactionDto>> GetPendingTransactionsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<PendingPostingResult> PostPendingTransactionsAsync(Guid companyId, Guid userId, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default);
    Task PostOperationalTransactionIfConfiguredAsync(Guid companyId, Guid accountingTransactionId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AccountingValidationException(string message) : Exception(message);

public sealed class AccountingConflictException(string message) : Exception(message);
