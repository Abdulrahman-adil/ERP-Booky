using System.ComponentModel.DataAnnotations;
using Erp.Application.Manufacturing;
using Erp.Domain.Manufacturing;

namespace Erp.Api.Contracts.Manufacturing;

public sealed class BillOfMaterialsComponentRequest
{
    [Required] public Guid ComponentProductId { get; init; }
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal Quantity { get; init; }
    public BillOfMaterialsComponentInput ToInput() => new(ComponentProductId, Quantity);
}

public sealed class BillOfMaterialsRequest : IValidatableObject
{
    [Required] public Guid FinishedProductId { get; init; }
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal OutputQuantity { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
    [MinLength(1)] public IReadOnlyCollection<BillOfMaterialsComponentRequest> Components { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom.HasValue && EffectiveTo.HasValue && EffectiveTo < EffectiveFrom)
        {
            yield return new ValidationResult("The effective end date cannot be before the effective start date.", [nameof(EffectiveTo)]);
        }
    }

    public BillOfMaterialsInput ToInput() => new(FinishedProductId, OutputQuantity, EffectiveFrom, EffectiveTo, Notes, IsActive, Components.Select(component => component.ToInput()).ToArray());
}

public sealed class BillOfMaterialsListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? FinishedProductId { get; init; }
    public bool? IsActive { get; init; }
    public DateOnly? EffectiveOn { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public BillOfMaterialsQuery ToQuery() => new(Search, FinishedProductId, IsActive, EffectiveOn, PageNumber, PageSize);
}

public sealed class ProductionOrderRequest
{
    [Required] public Guid FinishedProductId { get; init; }
    [Required] public Guid BillOfMaterialsId { get; init; }
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal PlannedQuantity { get; init; }
    [Required] public Guid SourceWarehouseId { get; init; }
    [Required] public Guid DestinationWarehouseId { get; init; }
    public DateOnly ProductionDate { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    public ProductionOrderInput ToInput() => new(FinishedProductId, BillOfMaterialsId, PlannedQuantity, SourceWarehouseId, DestinationWarehouseId, ProductionDate, Notes);
}

public sealed class ProductionOrderListRequest
{
    [MaxLength(100)] public string? Search { get; init; }
    public Guid? FinishedProductId { get; init; }
    [EnumDataType(typeof(ProductionOrderStatus))] public ProductionOrderStatus? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public ProductionOrderQuery ToQuery() => new(Search, FinishedProductId, Status, FromDate, ToDate, PageNumber, PageSize);
}

public sealed class ProductionCompletionRequest
{
    [Range(typeof(decimal), "0.000001", "9999999999999")] public decimal ActualProducedQuantity { get; init; }
}
