using Erp.Application.MasterData;
using Erp.Domain.Manufacturing;

namespace Erp.Application.Manufacturing;

public sealed record BillOfMaterialsComponentInput(Guid ComponentProductId, decimal Quantity);

public sealed record BillOfMaterialsInput(
    Guid FinishedProductId,
    decimal OutputQuantity,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes,
    bool IsActive,
    IReadOnlyCollection<BillOfMaterialsComponentInput> Components);

public sealed record BillOfMaterialsComponentDto(
    Guid Id,
    int LineNumber,
    Guid ComponentProductId,
    string ComponentSku,
    string ComponentName,
    decimal Quantity,
    Guid UnitOfMeasureId,
    string UnitOfMeasureName);

public sealed record BillOfMaterialsDto(
    Guid Id,
    string Code,
    Guid FinishedProductId,
    string FinishedProductSku,
    string FinishedProductName,
    decimal OutputQuantity,
    Guid OutputUnitOfMeasureId,
    string OutputUnitOfMeasureName,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<BillOfMaterialsComponentDto> Components);

public sealed record BillOfMaterialsQuery(
    string? Search,
    Guid? FinishedProductId,
    bool? IsActive,
    DateOnly? EffectiveOn,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record ProductionOrderInput(
    Guid FinishedProductId,
    Guid BillOfMaterialsId,
    decimal PlannedQuantity,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    DateOnly ProductionDate,
    string? Notes);

public sealed record ProductionOrderMaterialDto(
    Guid Id,
    int LineNumber,
    Guid ComponentProductId,
    string ComponentSku,
    string ComponentName,
    decimal QuantityPerBomOutput,
    decimal BomOutputQuantity,
    Guid UnitOfMeasureId,
    string UnitOfMeasureName);

public sealed record ProductionRequirementDto(
    Guid ComponentProductId,
    string ComponentSku,
    string ComponentName,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    Guid UnitOfMeasureId,
    string UnitOfMeasureName,
    bool IsAvailable);

public sealed record ProductionOrderDto(
    Guid Id,
    string ProductionOrderNumber,
    Guid FinishedProductId,
    string FinishedProductSku,
    string FinishedProductName,
    Guid BillOfMaterialsId,
    string BillOfMaterialsCode,
    decimal PlannedQuantity,
    decimal? ActualProducedQuantity,
    Guid OutputUnitOfMeasureId,
    string OutputUnitOfMeasureName,
    Guid SourceWarehouseId,
    string SourceWarehouseCode,
    string SourceWarehouseName,
    Guid DestinationWarehouseId,
    string DestinationWarehouseCode,
    string DestinationWarehouseName,
    DateOnly ProductionDate,
    ProductionOrderStatus Status,
    string? Notes,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    string? ReleasedByName,
    DateTimeOffset? ReleasedAt,
    string? PostedByName,
    DateTimeOffset? PostedAt,
    IReadOnlyCollection<ProductionOrderMaterialDto> Materials);

public sealed record ProductionOrderQuery(
    string? Search,
    Guid? FinishedProductId,
    ProductionOrderStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20);

public interface IManufacturingService
{
    Task<PagedResult<BillOfMaterialsDto>> GetBillsOfMaterialsAsync(Guid companyId, BillOfMaterialsQuery query, CancellationToken cancellationToken = default);
    Task<BillOfMaterialsDto?> GetBillOfMaterialsAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<BillOfMaterialsDto> CreateBillOfMaterialsAsync(Guid companyId, Guid userId, BillOfMaterialsInput input, CancellationToken cancellationToken = default);
    Task<BillOfMaterialsDto?> UpdateBillOfMaterialsAsync(Guid companyId, Guid id, BillOfMaterialsInput input, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductionOrderDto>> GetProductionOrdersAsync(Guid companyId, ProductionOrderQuery query, CancellationToken cancellationToken = default);
    Task<ProductionOrderDto?> GetProductionOrderAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<ProductionOrderDto> CreateProductionOrderAsync(Guid companyId, Guid userId, ProductionOrderInput input, CancellationToken cancellationToken = default);
    Task<ProductionOrderDto?> UpdateProductionOrderAsync(Guid companyId, Guid id, ProductionOrderInput input, CancellationToken cancellationToken = default);
    Task<ProductionOrderDto?> ReleaseProductionOrderAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ProductionRequirementDto>> GetRequirementsAsync(Guid companyId, Guid id, decimal actualProducedQuantity, CancellationToken cancellationToken = default);
    Task<ProductionOrderDto?> CompleteProductionOrderAsync(Guid companyId, Guid id, Guid userId, decimal actualProducedQuantity, CancellationToken cancellationToken = default);
}

public sealed class ManufacturingConflictException(string message) : Exception(message);
public sealed class ManufacturingValidationException(string message) : Exception(message);
