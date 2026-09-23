using System.ComponentModel.DataAnnotations;
using Erp.Application.Payments;
using Erp.Application.Purchasing;
using Erp.Domain.Payments;
using Erp.Domain.Purchasing;

namespace Erp.Api.Contracts.Purchasing;

public sealed class PurchaseInvoiceLineRequest
{
    [Required] public Guid ProductId { get; init; }
    [MaxLength(500)] public string? Description { get; init; }
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal Quantity { get; init; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal UnitCost { get; init; }
    [Range(typeof(decimal), "0", "100")] public decimal DiscountPercentage { get; init; }
    [MaxLength(1000)] public string? Memo { get; init; }
    public PurchaseInvoiceLineInput ToInput() => new(ProductId, Description, Quantity, UnitCost, DiscountPercentage, Memo);
}

public sealed class PurchaseInvoiceRequest : IValidatableObject
{
    [Required] public Guid SupplierId { get; init; }
    public Guid? WarehouseId { get; init; }
    public DateOnly InvoiceDate { get; init; }
    public DateOnly? DueDate { get; init; }
    [MaxLength(100)] public string? SupplierReference { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    public IReadOnlyCollection<PurchaseInvoiceLineRequest> Lines { get; init; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueDate.HasValue && DueDate < InvoiceDate) yield return new ValidationResult("The due date cannot be before the bill date.", [nameof(DueDate)]);
    }
    public PurchaseInvoiceInput ToInput() => new(SupplierId, WarehouseId, InvoiceDate, DueDate, SupplierReference, Notes, Lines.Select(line => line.ToInput()).ToArray());
}

public sealed class PurchaseInvoiceListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? SupplierId { get; init; }
    [EnumDataType(typeof(PurchaseInvoiceStatus))] public PurchaseInvoiceStatus? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(30)] public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
    public PurchaseInvoiceQuery ToQuery() => new(Search, SupplierId, Status, FromDate, ToDate, PageNumber, PageSize, SortBy, SortDescending);
}

public sealed class SupplierPaymentAllocationRequest
{
    [Required] public Guid OpenItemId { get; init; }
    [Range(typeof(decimal), "0.0001", "9999999999999")] public decimal Amount { get; init; }
    public PaymentAllocationInput ToInput() => new(OpenItemId, Amount);
}

public sealed class SupplierPaymentRequest
{
    [Required] public Guid SupplierId { get; init; }
    [Required] public Guid CashBankAccountId { get; init; }
    public DateOnly PaymentDate { get; init; }
    [Range(typeof(decimal), "0.0001", "9999999999999")] public decimal Amount { get; init; }
    [MaxLength(100)] public string? ExternalReference { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    public IReadOnlyCollection<SupplierPaymentAllocationRequest> Allocations { get; init; } = [];
    public SupplierPaymentInput ToInput() => new(SupplierId, CashBankAccountId, PaymentDate, Amount, ExternalReference, Notes, Allocations.Select(allocation => allocation.ToInput()).ToArray());
}

public sealed class SupplierPaymentListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? SupplierId { get; init; }
    [EnumDataType(typeof(PaymentStatus))] public PaymentStatus? Status { get; init; }
    public Guid? CashBankAccountId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public bool SortDescending { get; init; } = true;
    public SupplierPaymentQuery ToQuery() => new(Search, SupplierId, Status, CashBankAccountId, FromDate, ToDate, PageNumber, PageSize, SortDescending);
}

public sealed class SupplierPayableListRequest
{
    public Guid? SupplierId { get; init; }
    public bool? IsOpen { get; init; }
    public bool? IsOverdue { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public SupplierPayableQuery ToQuery() => new(SupplierId, IsOpen, IsOverdue, FromDate, ToDate, PageNumber, PageSize);
}
