using Erp.Domain.MasterData;

namespace Erp.Application.MasterData;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record MasterDataQuery(
    string? Search,
    bool? IsActive,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false,
    Guid? ProductCategoryId = null,
    Guid? UnitOfMeasureId = null);

public sealed record BusinessPartnerInput(
    string LegalName,
    string? AddressLine1,
    string? CountryCode,
    string? AddressLine2,
    string? City,
    string? PostalCode,
    string? Email,
    string? Phone,
    string? TaxIdentifier,
    int PaymentTermsDays);

public sealed record BusinessPartnerDto(
    Guid Id,
    string Code,
    string LegalName,
    string? TaxIdentifier,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? PostalCode,
    string? CountryCode,
    string? Email,
    string? Phone,
    int PaymentTermsDays,
    bool IsActive);

public sealed record ProductCategoryInput(string Code, string Name, Guid? ParentCategoryId);

public sealed record ProductCategoryDto(Guid Id, string Code, string Name, Guid? ParentCategoryId, string? ParentCategoryName, bool IsActive);

public sealed record UnitOfMeasureInput(string Code, string Name, string Dimension, decimal ConversionFactorToBase, int DecimalPlaces);

public sealed record UnitOfMeasureDto(Guid Id, string Code, string Name, string Dimension, decimal ConversionFactorToBase, int DecimalPlaces);

public sealed record ProductInput(
    string Name,
    ProductType ProductType,
    Guid? StockUnitOfMeasureId,
    Guid? ProductCategoryId,
    InventoryItemPurpose? InventoryPurpose = null);

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    ProductType ProductType,
    Guid? StockUnitOfMeasureId,
    string? UnitOfMeasureName,
    Guid? ProductCategoryId,
    string? ProductCategoryName,
    InventoryItemPurpose? InventoryPurpose,
    bool IsActive);

public interface IMasterDataService
{
    Task<PagedResult<BusinessPartnerDto>> GetCustomersAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default);
    Task<BusinessPartnerDto?> GetCustomerAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<BusinessPartnerDto> CreateCustomerAsync(Guid companyId, BusinessPartnerInput input, CancellationToken cancellationToken = default);
    Task<BusinessPartnerDto?> UpdateCustomerAsync(Guid companyId, Guid id, BusinessPartnerInput input, CancellationToken cancellationToken = default);
    Task<bool> SetCustomerActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<PagedResult<BusinessPartnerDto>> GetSuppliersAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default);
    Task<BusinessPartnerDto?> GetSupplierAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<BusinessPartnerDto> CreateSupplierAsync(Guid companyId, BusinessPartnerInput input, CancellationToken cancellationToken = default);
    Task<BusinessPartnerDto?> UpdateSupplierAsync(Guid companyId, Guid id, BusinessPartnerInput input, CancellationToken cancellationToken = default);
    Task<bool> SetSupplierActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> DeleteSupplierAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductCategoryDto>> GetCategoriesAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default);
    Task<ProductCategoryDto?> GetCategoryAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<ProductCategoryDto> CreateCategoryAsync(Guid companyId, ProductCategoryInput input, CancellationToken cancellationToken = default);
    Task<ProductCategoryDto?> UpdateCategoryAsync(Guid companyId, Guid id, ProductCategoryInput input, CancellationToken cancellationToken = default);
    Task<bool> SetCategoryActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<PagedResult<UnitOfMeasureDto>> GetUnitsAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default);
    Task<UnitOfMeasureDto?> GetUnitAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<UnitOfMeasureDto> CreateUnitAsync(Guid companyId, UnitOfMeasureInput input, CancellationToken cancellationToken = default);
    Task<UnitOfMeasureDto?> UpdateUnitAsync(Guid companyId, Guid id, UnitOfMeasureInput input, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductDto>> GetProductsAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(Guid companyId, ProductInput input, CancellationToken cancellationToken = default);
    Task<ProductDto?> UpdateProductAsync(Guid companyId, Guid id, ProductInput input, CancellationToken cancellationToken = default);
    Task<bool> SetProductActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public sealed class MasterDataConflictException(string message) : Exception(message);

public sealed class MasterDataValidationException(string message) : Exception(message);
