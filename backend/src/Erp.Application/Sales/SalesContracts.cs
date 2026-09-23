using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Application.Common;
using Erp.Domain.Sales;

namespace Erp.Application.Sales;

public sealed record SalesInvoiceLineInput(Guid ProductId, string? Description, decimal Quantity, decimal UnitPrice, decimal DiscountPercentage = 0);

public sealed record SalesInvoiceInput(
    Guid CustomerId,
    Guid? WarehouseId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? Reference,
    string? Notes,
    IReadOnlyCollection<SalesInvoiceLineInput> Lines);

public sealed record SalesInvoiceQuery(
    string? Search,
    Guid? CustomerId,
    SalesInvoiceStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = true);

public sealed record SalesInvoiceLineDto(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string Description,
    decimal Quantity,
    Guid UnitOfMeasureId,
    string UnitOfMeasureName,
    decimal UnitPrice,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    decimal NetAmount);

public sealed record SalesInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid? WarehouseId,
    string? WarehouseCode,
    string? WarehouseName,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string CurrencyCode,
    SalesInvoiceStatus Status,
    SalesInvoicePaymentStatus? PaymentStatus,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal Total,
    decimal OutstandingAmount,
    string? Reference,
    string? Notes,
    string? CreatedByName,
    DateTimeOffset? CreatedAt,
    string? PostedByName,
    DateTimeOffset? PostedAt,
    IReadOnlyCollection<SalesInvoiceLineDto> Lines);

public sealed record CustomerSalesSummaryDto(
    Guid CustomerId,
    decimal TotalPostedSales,
    decimal OutstandingReceivable,
    string? CurrencyCode,
    IReadOnlyCollection<SalesInvoiceDto> RecentInvoices);

public sealed record SalesDashboardDto(
    decimal PostedSalesTotal,
    decimal OutstandingReceivableTotal,
    string? CurrencyCode,
    IReadOnlyCollection<SalesInvoiceDto> RecentPostedInvoices,
    IReadOnlyCollection<DashboardPeriodTotalDto> MonthlyTotals);

public interface ISalesService
{
    Task<PagedResult<SalesInvoiceDto>> GetInvoicesAsync(Guid companyId, SalesInvoiceQuery query, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto?> GetInvoiceAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto> CreateDraftAsync(Guid companyId, Guid userId, SalesInvoiceInput input, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto?> UpdateDraftAsync(Guid companyId, Guid id, SalesInvoiceInput input, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDto?> PostAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerSalesSummaryDto?> GetCustomerSummaryAsync(Guid companyId, Guid customerId, CancellationToken cancellationToken = default);
    Task<SalesDashboardDto> GetDashboardAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class SalesValidationException(string message) : Exception(message);

public sealed class SalesConflictException(string message) : Exception(message);
