using System.ComponentModel.DataAnnotations;
using Erp.Application.Accounting;
using Erp.Domain.Accounting;

namespace Erp.Api.Contracts.Accounting;

public sealed class AccountRequest
{
    [Required, MaxLength(30)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    [EnumDataType(typeof(AccountType))] public AccountType AccountType { get; init; }
    [EnumDataType(typeof(AccountRole))] public AccountRole AccountRole { get; init; }
    [EnumDataType(typeof(AccountNormalBalance))] public AccountNormalBalance? NormalBalanceOverride { get; init; }
    public bool IsPosting { get; init; }
    public Guid? ParentAccountId { get; init; }
    public bool IsActive { get; init; } = true;
    public AccountInput ToInput() => new(Code, Name, AccountType, AccountRole, IsPosting, ParentAccountId, IsActive, NormalBalanceOverride);
}

public sealed class AccountQueryRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    [EnumDataType(typeof(AccountType))] public AccountType? AccountType { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class FinancialClassRequest
{
    [Required, MaxLength(30)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public FinancialClassInput ToInput() => new(Code, Name, IsActive);
}

public sealed class AccountingPeriodRequest
{
    [Required, MaxLength(100)] public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    [EnumDataType(typeof(AccountingPeriodStatus))] public AccountingPeriodStatus Status { get; init; } = AccountingPeriodStatus.Open;
    public AccountingPeriodInput ToInput() => new(Name, StartDate, EndDate, Status);
}

public sealed class PeriodStatusRequest
{
    [EnumDataType(typeof(AccountingPeriodStatus))] public AccountingPeriodStatus Status { get; init; }
}

public sealed class PostingProfileMappingRequest
{
    [Required, MaxLength(100)] public string PostingKey { get; init; } = string.Empty;
    [Required] public Guid AccountId { get; init; }
    public PostingProfileMappingInput ToInput() => new(PostingKey, AccountId);
}

public sealed class PostingProfileRequest
{
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public IReadOnlyCollection<PostingProfileMappingRequest> Mappings { get; init; } = [];
    public PostingProfileInput ToInput() => new(Name, IsActive, Mappings.Select(mapping => mapping.ToInput()).ToArray());
}

public sealed class CashBankMappingRequest
{
    public Guid? PostingAccountId { get; init; }
}

public sealed class JournalEntryLineRequest
{
    [Required] public Guid AccountId { get; init; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal Debit { get; init; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal Credit { get; init; }
    public Guid? BusinessPartnerId { get; init; }
    public Guid? FinancialClassId { get; init; }
    [MaxLength(500)] public string? Memo { get; init; }
    public JournalEntryLineInput ToInput() => new(AccountId, Debit, Credit, BusinessPartnerId, FinancialClassId, Memo);
}

public sealed class JournalEntryRequest
{
    public DateOnly EntryDate { get; init; }
    [Required, MaxLength(500)] public string Description { get; init; } = string.Empty;
    [MaxLength(150)] public string? Reference { get; init; }
    public IReadOnlyCollection<JournalEntryLineRequest> Lines { get; init; } = [];
    public JournalEntryInput ToInput() => new(EntryDate, Description, Reference, Lines.Select(line => line.ToInput()).ToArray());
}

public sealed class JournalEntryListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    [EnumDataType(typeof(JournalEntryStatus))] public JournalEntryStatus? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 50;
    public JournalEntryQuery ToQuery() => new(Search, Status, FromDate, ToDate, PageNumber, PageSize);
}

public sealed class GeneralLedgerListRequest
{
    public Guid? AccountId { get; init; }
    public Guid? BusinessPartnerId { get; init; }
    public Guid? FinancialClassId { get; init; }
    [MaxLength(50)] public string? SourceModule { get; init; }
    [MaxLength(150)] public string? Reference { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 100;
    public GeneralLedgerQuery ToQuery() => new(AccountId, BusinessPartnerId, FinancialClassId, SourceModule, Reference, FromDate, ToDate, PageNumber, PageSize);
}

public sealed class PendingPostingRequest
{
    public IReadOnlyCollection<Guid> TransactionIds { get; init; } = [];
}
