using Erp.Application.MasterData;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;

namespace Erp.Application.Inventory;

public sealed record WarehouseInput(
    string Name,
    string? Description,
    string? AddressLine1,
    string? CountryCode,
    string? AddressLine2,
    string? City,
    string? PostalCode);

public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? PostalCode,
    string? CountryCode,
    bool IsActive);

public sealed record WarehouseQuery(
    string? Search,
    bool? IsActive,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false);

public sealed record InventoryMovementInput(
    Guid ProductId,
    Guid WarehouseId,
    InventoryMovementType MovementType,
    decimal Quantity,
    DateTimeOffset? OccurredAt,
    string? ExternalReference,
    string? Notes);

public sealed record InventorySystemStockOutInput(
    Guid ProductId,
    Guid WarehouseId,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    Erp.Domain.Common.SourceReference SourceReference,
    string? ExternalReference,
    string? Notes);

public sealed record InventorySystemStockInInput(
    Guid ProductId,
    Guid WarehouseId,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    Erp.Domain.Common.SourceReference SourceReference,
    decimal CostAmount,
    string CurrencyCode,
    string? ExternalReference,
    string? Notes);

public sealed record InventorySystemMovementInput(
    Guid ProductId,
    Guid WarehouseId,
    InventoryMovementType MovementType,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    Erp.Domain.Common.SourceReference SourceReference,
    string? ExternalReference,
    string? Notes,
    decimal? CostAmount = null,
    string? CurrencyCode = null);

public sealed record StockLedgerQuery(
    string? Search,
    Guid? ProductId,
    Guid? WarehouseId,
    InventoryMovementType? MovementType,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20,
    bool SortDescending = true);

public sealed record StockMovementDto(
    Guid Id,
    string MovementNumber,
    InventoryMovementType MovementType,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid WarehouseId,
    string WarehouseName,
    string WarehouseCode,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal? BalanceAfter,
    string UnitOfMeasureName,
    DateTimeOffset OccurredAt,
    string? ExternalReference,
    string? Notes,
    string CreatedByName,
    DateTimeOffset? CreatedAt,
    string SourceModule,
    string SourceEventType);

public sealed record StockBalanceDto(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid WarehouseId,
    string WarehouseName,
    string WarehouseCode,
    decimal QuantityOnHand,
    string UnitOfMeasureName,
    InventoryItemPurpose? InventoryPurpose,
    DateTimeOffset UpdatedAt);

public sealed record InventoryPurposeQuantityDto(string Purpose, decimal QuantityOnHand, int StockedProductCount);

public sealed record InventoryOverviewDto(
    int ActiveWarehouseCount,
    int StockedProductCount,
    int MovementCount,
    IReadOnlyCollection<StockMovementDto> RecentMovements,
    IReadOnlyCollection<InventoryPurposeQuantityDto> QuantityByPurpose);

public sealed record WarehouseDetailsDto(
    WarehouseDto Warehouse,
    IReadOnlyCollection<StockBalanceDto> StockedProducts,
    IReadOnlyCollection<StockMovementDto> RecentMovements);

public sealed record ProductInventoryDto(
    Guid ProductId,
    decimal TotalQuantityOnHand,
    string? UnitOfMeasureName,
    IReadOnlyCollection<StockBalanceDto> WarehouseBalances,
    IReadOnlyCollection<StockMovementDto> RecentMovements);

public interface IInventoryService
{
    Task<PagedResult<WarehouseDto>> GetWarehousesAsync(Guid companyId, WarehouseQuery query, CancellationToken cancellationToken = default);
    Task<WarehouseDto?> GetWarehouseAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<WarehouseDto> CreateWarehouseAsync(Guid companyId, WarehouseInput input, CancellationToken cancellationToken = default);
    Task<WarehouseDto?> UpdateWarehouseAsync(Guid companyId, Guid id, WarehouseInput input, CancellationToken cancellationToken = default);
    Task<bool> SetWarehouseActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<WarehouseDetailsDto?> GetWarehouseDetailsAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);

    Task<InventoryOverviewDto> GetOverviewAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<PagedResult<StockMovementDto>> GetLedgerAsync(Guid companyId, StockLedgerQuery query, CancellationToken cancellationToken = default);
    Task<StockMovementDto> RecordMovementAsync(Guid companyId, Guid userId, InventoryMovementInput input, CancellationToken cancellationToken = default);
    Task<StockMovementDto> RecordSystemStockOutAsync(Guid companyId, Guid userId, InventorySystemStockOutInput input, CancellationToken cancellationToken = default);
    Task<StockMovementDto> RecordSystemStockInAsync(Guid companyId, Guid userId, InventorySystemStockInInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StockMovementDto>> RecordSystemMovementsAsync(Guid companyId, Guid userId, IReadOnlyCollection<InventorySystemMovementInput> inputs, CancellationToken cancellationToken = default);
    Task<ProductInventoryDto?> GetProductInventoryAsync(Guid companyId, Guid productId, CancellationToken cancellationToken = default);
}

public sealed class InventoryValidationException(string message) : Exception(message);

public sealed class InventoryConflictException(string message) : Exception(message);
