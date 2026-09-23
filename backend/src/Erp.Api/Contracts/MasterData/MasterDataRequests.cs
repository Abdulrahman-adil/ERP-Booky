using System.ComponentModel.DataAnnotations;
using Erp.Application.MasterData;
using Erp.Domain.MasterData;

namespace Erp.Api.Contracts.MasterData;

public sealed class MasterDataListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(30)] public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public Guid? ProductCategoryId { get; init; }
    public Guid? UnitOfMeasureId { get; init; }
    public MasterDataQuery ToQuery() => new(Search, IsActive, PageNumber, PageSize, SortBy, SortDescending, ProductCategoryId, UnitOfMeasureId);
}

public sealed class BusinessPartnerRequest : IValidatableObject
{
    [Required, MaxLength(200)] public string LegalName { get; init; } = string.Empty;
    [MaxLength(200)] public string? AddressLine1 { get; init; }
    [StringLength(2, MinimumLength = 2)] public string? CountryCode { get; init; }
    [MaxLength(200)] public string? AddressLine2 { get; init; }
    [MaxLength(100)] public string? City { get; init; }
    [MaxLength(30)] public string? PostalCode { get; init; }
    [EmailAddress, MaxLength(254)] public string? Email { get; init; }
    [MaxLength(50)] public string? Phone { get; init; }
    [MaxLength(100)] public string? TaxIdentifier { get; init; }
    [Range(0, 3650)] public int PaymentTermsDays { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(AddressLine1) && string.IsNullOrWhiteSpace(CountryCode))
        {
            yield return new ValidationResult("Provide a country code when an address is entered.", [nameof(CountryCode)]);
        }
        if (string.IsNullOrWhiteSpace(AddressLine1) && !string.IsNullOrWhiteSpace(CountryCode))
        {
            yield return new ValidationResult("Provide an address line when a country code is entered.", [nameof(AddressLine1)]);
        }
    }

    public BusinessPartnerInput ToInput() => new(LegalName, AddressLine1, CountryCode, AddressLine2, City, PostalCode, Email, Phone, TaxIdentifier, PaymentTermsDays);
}

public sealed class ProductCategoryRequest
{
    [Required, MaxLength(30)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    public Guid? ParentCategoryId { get; init; }
    public ProductCategoryInput ToInput() => new(Code, Name, ParentCategoryId);
}

public sealed class UnitOfMeasureRequest
{
    [Required, MaxLength(20)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(50)] public string Dimension { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal ConversionFactorToBase { get; init; }
    [Range(0, 6)] public int DecimalPlaces { get; init; }
    public UnitOfMeasureInput ToInput() => new(Code, Name, Dimension, ConversionFactorToBase, DecimalPlaces);
}

public sealed class ProductRequest
{
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    [EnumDataType(typeof(ProductType))] public ProductType ProductType { get; init; }
    public Guid? StockUnitOfMeasureId { get; init; }
    public Guid? ProductCategoryId { get; init; }
    [EnumDataType(typeof(InventoryItemPurpose))] public InventoryItemPurpose? InventoryPurpose { get; init; }
    public ProductInput ToInput() => new(Name, ProductType, StockUnitOfMeasureId, ProductCategoryId, InventoryPurpose);
}

public sealed class StatusRequest
{
    public bool IsActive { get; init; }
}
