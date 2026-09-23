using Erp.Application.MasterData;
using Erp.Application.Payments;
using Erp.Application.Common;
using Erp.Domain.Payments;
using Erp.Domain.Purchasing;

namespace Erp.Application.Purchasing;

public sealed record PurchaseInvoiceLineInput(Guid ProductId, string? Description, decimal Quantity, decimal UnitCost, decimal DiscountPercentage = 0, string? Memo = null);

public sealed record PurchaseInvoiceInput(
    Guid SupplierId,
    Guid? WarehouseId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? SupplierReference,
    string? Notes,
    IReadOnlyCollection<PurchaseInvoiceLineInput> Lines);

public sealed record PurchaseInvoiceQuery(
    string? Search,
    Guid? SupplierId,
    PurchaseInvoiceStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = true);

public sealed record PurchaseInvoiceLineDto(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string Description,
    decimal Quantity,
    Guid UnitOfMeasureId,
    string UnitOfMeasureName,
    decimal UnitCost,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    decimal NetAmount,
    string? Memo);

public sealed record PurchaseInvoiceDto(
    Guid Id,
    string PurchaseBillNumber,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid? WarehouseId,
    string? WarehouseCode,
    string? WarehouseName,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string CurrencyCode,
    PurchaseInvoiceStatus Status,
    PurchaseInvoicePaymentStatus? PaymentStatus,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal Total,
    decimal OutstandingAmount,
    string? SupplierReference,
    string? Notes,
    string? CreatedByName,
    DateTimeOffset? CreatedAt,
    string? PostedByName,
    DateTimeOffset? PostedAt,
    IReadOnlyCollection<PurchaseInvoiceLineDto> Lines);

public sealed record SupplierPayableDto(
    Guid OpenItemId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid? PurchaseBillId,
    string PurchaseBillNumber,
    string? SupplierReference,
    DateOnly? BillDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string CurrencyCode,
    string PaymentStatus,
    bool IsOverdue);

public sealed record SupplierPayableQuery(
    Guid? SupplierId,
    bool? IsOpen,
    bool? IsOverdue,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record SupplierPaymentInput(
    Guid SupplierId,
    Guid CashBankAccountId,
    DateOnly PaymentDate,
    decimal Amount,
    string? ExternalReference,
    string? Notes,
    IReadOnlyCollection<PaymentAllocationInput> Allocations);

public sealed record SupplierPaymentQuery(
    string? Search,
    Guid? SupplierId,
    PaymentStatus? Status,
    Guid? CashBankAccountId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20,
    bool SortDescending = true);

public sealed record SupplierPaymentAllocationDto(
    Guid Id,
    Guid OpenItemId,
    Guid? PurchaseBillId,
    string PurchaseBillNumber,
    string? SupplierReference,
    DateOnly? BillDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal OutstandingAmount,
    decimal AllocationAmount,
    string CurrencyCode);

public sealed record SupplierPaymentDto(
    Guid Id,
    string PaymentNumber,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid CashBankAccountId,
    string CashBankAccountName,
    string CashBankAccountType,
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
    IReadOnlyCollection<SupplierPaymentAllocationDto> Allocations);

public sealed record PurchaseBillPaymentSummaryDto(
    Guid PurchaseBillId,
    Guid? OpenItemId,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string CurrencyCode,
    string PaymentStatus,
    IReadOnlyCollection<SupplierPaymentDto> AppliedPayments);

public sealed record SupplierPurchaseSummaryDto(
    Guid SupplierId,
    decimal TotalPostedPurchases,
    decimal OutstandingPayable,
    string? CurrencyCode,
    IReadOnlyCollection<PurchaseInvoiceDto> RecentBills,
    IReadOnlyCollection<SupplierPaymentDto> RecentPayments);

public sealed record PurchaseDashboardDto(
    decimal PostedPurchasesTotal,
    decimal OutstandingPayableTotal,
    string? CurrencyCode,
    IReadOnlyCollection<PurchaseInvoiceDto> RecentPostedBills,
    IReadOnlyCollection<SupplierPaymentDto> RecentSupplierPayments,
    IReadOnlyCollection<DashboardPeriodTotalDto> MonthlyTotals);

public interface IPurchaseService
{
    Task<PagedResult<PurchaseInvoiceDto>> GetBillsAsync(Guid companyId, PurchaseInvoiceQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto?> GetBillAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> CreateDraftBillAsync(Guid companyId, Guid userId, PurchaseInvoiceInput input, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto?> UpdateDraftBillAsync(Guid companyId, Guid id, PurchaseInvoiceInput input, CancellationToken cancellationToken = default);
    Task<bool> DeleteDraftBillAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto?> PostBillAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<PagedResult<SupplierPayableDto>> GetPayablesAsync(Guid companyId, SupplierPayableQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SupplierPayableDto>> GetSupplierOpenPayablesAsync(Guid companyId, Guid supplierId, CancellationToken cancellationToken = default);
    Task<PurchaseBillPaymentSummaryDto?> GetBillPaymentSummaryAsync(Guid companyId, Guid billId, CancellationToken cancellationToken = default);
    Task<PagedResult<SupplierPaymentDto>> GetSupplierPaymentsAsync(Guid companyId, SupplierPaymentQuery query, CancellationToken cancellationToken = default);
    Task<SupplierPaymentDto?> GetSupplierPaymentAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<SupplierPaymentDto> CreateDraftSupplierPaymentAsync(Guid companyId, Guid userId, SupplierPaymentInput input, CancellationToken cancellationToken = default);
    Task<SupplierPaymentDto?> UpdateDraftSupplierPaymentAsync(Guid companyId, Guid id, SupplierPaymentInput input, CancellationToken cancellationToken = default);
    Task<SupplierPaymentDto?> PostSupplierPaymentAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<SupplierPurchaseSummaryDto?> GetSupplierSummaryAsync(Guid companyId, Guid supplierId, CancellationToken cancellationToken = default);
    Task<PurchaseDashboardDto> GetDashboardAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class PurchaseValidationException(string message) : Exception(message);
public sealed class PurchaseConflictException(string message) : Exception(message);
