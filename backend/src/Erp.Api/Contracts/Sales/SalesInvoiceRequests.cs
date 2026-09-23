using System.ComponentModel.DataAnnotations;
using Erp.Application.Sales;
using Erp.Domain.Sales;

namespace Erp.Api.Contracts.Sales;

public sealed class SalesInvoiceLineRequest
{
    [Required] public Guid ProductId { get; init; }
    [MaxLength(500)] public string? Description { get; init; }
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal Quantity { get; init; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal UnitPrice { get; init; }
    [Range(typeof(decimal), "0", "100")] public decimal DiscountPercentage { get; init; }
    public SalesInvoiceLineInput ToInput() => new(ProductId, Description, Quantity, UnitPrice, DiscountPercentage);
}

public sealed class SalesInvoiceRequest : IValidatableObject
{
    [Required] public Guid CustomerId { get; init; }
    public Guid? WarehouseId { get; init; }
    public DateOnly InvoiceDate { get; init; }
    public DateOnly? DueDate { get; init; }
    [MaxLength(100)] public string? Reference { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    public IReadOnlyCollection<SalesInvoiceLineRequest> Lines { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueDate.HasValue && DueDate.Value < InvoiceDate)
        {
            yield return new ValidationResult("The due date cannot be before the invoice date.", [nameof(DueDate)]);
        }
    }

    public SalesInvoiceInput ToInput() => new(CustomerId, WarehouseId, InvoiceDate, DueDate, Reference, Notes, Lines.Select(line => line.ToInput()).ToArray());
}

public sealed class SalesInvoiceListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? CustomerId { get; init; }
    [EnumDataType(typeof(SalesInvoiceStatus))] public SalesInvoiceStatus? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(30)] public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
    public SalesInvoiceQuery ToQuery() => new(Search, CustomerId, Status, FromDate, ToDate, PageNumber, PageSize, SortBy, SortDescending);
}
