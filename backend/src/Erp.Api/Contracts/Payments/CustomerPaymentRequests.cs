using System.ComponentModel.DataAnnotations;
using Erp.Application.Payments;
using Erp.Domain.Accounting;
using Erp.Domain.Payments;

namespace Erp.Api.Contracts.Payments;

public sealed class PaymentAllocationRequest
{
    [Required] public Guid OpenItemId { get; init; }
    [Range(typeof(decimal), "0.0001", "9999999999999")] public decimal Amount { get; init; }
    public PaymentAllocationInput ToInput() => new(OpenItemId, Amount);
}

public sealed class CustomerPaymentRequest
{
    [Required] public Guid CustomerId { get; init; }
    [Required] public Guid CashBankAccountId { get; init; }
    public DateOnly PaymentDate { get; init; }
    [Range(typeof(decimal), "0.0001", "9999999999999")] public decimal Amount { get; init; }
    [MaxLength(100)] public string? ExternalReference { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    public IReadOnlyCollection<PaymentAllocationRequest> Allocations { get; init; } = [];
    public CustomerPaymentInput ToInput() => new(CustomerId, CashBankAccountId, PaymentDate, Amount, ExternalReference, Notes, Allocations.Select(allocation => allocation.ToInput()).ToArray());
}

public sealed class CustomerPaymentListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? CustomerId { get; init; }
    [EnumDataType(typeof(PaymentStatus))] public PaymentStatus? Status { get; init; }
    public Guid? CashBankAccountId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(30)] public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
    public CustomerPaymentQuery ToQuery() => new(Search, CustomerId, Status, CashBankAccountId, FromDate, ToDate, PageNumber, PageSize, SortBy, SortDescending);
}

public sealed class ReceivableListRequest
{
    public Guid? CustomerId { get; init; }
    public bool? IsOpen { get; init; }
    public bool? IsOverdue { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public ReceivableQuery ToQuery() => new(CustomerId, IsOpen, IsOverdue, FromDate, ToDate, PageNumber, PageSize);
}

public sealed class CashBankAccountRequest
{
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    [EnumDataType(typeof(CashBankAccountType))] public CashBankAccountType AccountType { get; init; }
    public Guid? PostingAccountId { get; init; }
    public CashBankAccountInput ToInput() => new(Name, AccountType, PostingAccountId);
}

public sealed class CashBankAccountUpdateRequest
{
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public CashBankAccountUpdateInput ToInput() => new(Name, IsActive);
}
