using Erp.Application.MasterData;
using Erp.Domain.Accounting;
using Erp.Domain.Payments;

namespace Erp.Application.Payments;

public sealed record PaymentAllocationInput(Guid OpenItemId, decimal Amount);

public sealed record CustomerPaymentInput(
    Guid CustomerId,
    Guid CashBankAccountId,
    DateOnly PaymentDate,
    decimal Amount,
    string? ExternalReference,
    string? Notes,
    IReadOnlyCollection<PaymentAllocationInput> Allocations);

public sealed record CustomerPaymentQuery(
    string? Search,
    Guid? CustomerId,
    PaymentStatus? Status,
    Guid? CashBankAccountId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = true);

public sealed record ReceivableQuery(
    Guid? CustomerId,
    bool? IsOpen,
    bool? IsOverdue,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record PaymentAllocationDto(
    Guid Id,
    Guid OpenItemId,
    Guid? InvoiceId,
    string DocumentNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal OutstandingAmount,
    decimal AllocationAmount,
    string CurrencyCode);

public sealed record CustomerPaymentDto(
    Guid Id,
    string PaymentNumber,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid CashBankAccountId,
    string CashBankAccountName,
    CashBankAccountType CashBankAccountType,
    PaymentDirection Direction,
    DateOnly PaymentDate,
    string CurrencyCode,
    decimal Amount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    PaymentStatus Status,
    string? ExternalReference,
    string? Notes,
    string? CreatedByName,
    DateTimeOffset? CreatedAt,
    string? PostedByName,
    DateTimeOffset? PostedAt,
    IReadOnlyCollection<PaymentAllocationDto> Allocations);

public sealed record OpenReceivableDto(
    Guid OpenItemId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid? InvoiceId,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string CurrencyCode,
    string PaymentStatus,
    bool IsOverdue);

public sealed record InvoicePaymentSummaryDto(
    Guid InvoiceId,
    Guid? OpenItemId,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string CurrencyCode,
    string PaymentStatus,
    IReadOnlyCollection<CustomerPaymentDto> AppliedPayments);

public sealed record CashBankAccountInput(string Name, CashBankAccountType AccountType, Guid? PostingAccountId = null);

public sealed record CashBankAccountUpdateInput(string Name, bool IsActive);

public sealed record CashBankAccountDto(Guid Id, string Name, CashBankAccountType AccountType, Guid? PostingAccountId, bool IsActive);

public interface IPaymentService
{
    Task<PagedResult<CustomerPaymentDto>> GetPaymentsAsync(Guid companyId, CustomerPaymentQuery query, CancellationToken cancellationToken = default);
    Task<CustomerPaymentDto?> GetPaymentAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<CustomerPaymentDto> CreateDraftAsync(Guid companyId, Guid userId, CustomerPaymentInput input, CancellationToken cancellationToken = default);
    Task<CustomerPaymentDto?> UpdateDraftAsync(Guid companyId, Guid id, CustomerPaymentInput input, CancellationToken cancellationToken = default);
    Task<CustomerPaymentDto?> PostAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OpenReceivableDto>> GetCustomerOpenReceivablesAsync(Guid companyId, Guid customerId, CancellationToken cancellationToken = default);
    Task<PagedResult<OpenReceivableDto>> GetReceivablesAsync(Guid companyId, ReceivableQuery query, CancellationToken cancellationToken = default);
    Task<InvoicePaymentSummaryDto?> GetInvoicePaymentSummaryAsync(Guid companyId, Guid invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CashBankAccountDto>> GetCashBankAccountsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CashBankAccountDto> CreateCashBankAccountAsync(Guid companyId, CashBankAccountInput input, CancellationToken cancellationToken = default);
    Task<CashBankAccountDto?> UpdateCashBankAccountAsync(Guid companyId, Guid id, CashBankAccountUpdateInput input, CancellationToken cancellationToken = default);
}

public sealed class PaymentValidationException(string message) : Exception(message);

public sealed class PaymentConflictException(string message) : Exception(message);
