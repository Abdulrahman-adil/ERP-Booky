using System.ComponentModel.DataAnnotations;
using Erp.Application.Inventory;
using Erp.Domain.Inventory;

namespace Erp.Api.Contracts.Inventory;

public sealed class WarehouseListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(30)] public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public WarehouseQuery ToQuery() => new(Search, IsActive, PageNumber, PageSize, SortBy, SortDescending);
}

public sealed class WarehouseRequest : IValidatableObject
{
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    [MaxLength(500)] public string? Description { get; init; }
    [MaxLength(200)] public string? AddressLine1 { get; init; }
    [StringLength(2, MinimumLength = 2)] public string? CountryCode { get; init; }
    [MaxLength(200)] public string? AddressLine2 { get; init; }
    [MaxLength(100)] public string? City { get; init; }
    [MaxLength(30)] public string? PostalCode { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(AddressLine1) && string.IsNullOrWhiteSpace(CountryCode)) yield return new ValidationResult("Provide a country code when an address is entered.", [nameof(CountryCode)]);
        if (string.IsNullOrWhiteSpace(AddressLine1) && !string.IsNullOrWhiteSpace(CountryCode)) yield return new ValidationResult("Provide an address line when a country code is entered.", [nameof(AddressLine1)]);
    }

    public WarehouseInput ToInput() => new(Name, Description, AddressLine1, CountryCode, AddressLine2, City, PostalCode);
}

public sealed class InventoryMovementRequest : IValidatableObject
{
    [Required] public Guid ProductId { get; init; }
    [Required] public Guid WarehouseId { get; init; }
    [EnumDataType(typeof(InventoryMovementType))] public InventoryMovementType MovementType { get; init; }
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal Quantity { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    [MaxLength(100)] public string? ExternalReference { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MovementType is InventoryMovementType.PositiveAdjustment or InventoryMovementType.NegativeAdjustment && string.IsNullOrWhiteSpace(Notes))
        {
            yield return new ValidationResult("A reason is required for inventory adjustments.", [nameof(Notes)]);
        }
    }

    public InventoryMovementInput ToInput() => new(ProductId, WarehouseId, MovementType, Quantity, OccurredAt, ExternalReference, Notes);
}

public sealed class StockLedgerListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? WarehouseId { get; init; }
    [EnumDataType(typeof(InventoryMovementType))] public InventoryMovementType? MovementType { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public bool SortDescending { get; init; } = true;
    public StockLedgerQuery ToQuery() => new(Search, ProductId, WarehouseId, MovementType, FromDate, ToDate, PageNumber, PageSize, SortDescending);
}
