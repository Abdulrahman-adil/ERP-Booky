using Erp.Domain.Common;

namespace Erp.Domain.Manufacturing;

#pragma warning disable CS8618

public enum ProductionOrderStatus
{
    Draft,
    Released,
    Completed
}

public sealed class BillOfMaterials : AggregateRoot
{
    private readonly List<BillOfMaterialsComponent> _components = [];

    private BillOfMaterials()
    {
    }

    public BillOfMaterials(
        Guid companyId,
        string code,
        Guid finishedProductId,
        decimal outputQuantity,
        Guid outputUnitOfMeasureId,
        Guid createdByUserId,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null,
        string? notes = null,
        DateTimeOffset? createdAt = null,
        Guid id = default)
        : base(id)
    {
        ValidateHeader(companyId, code, finishedProductId, outputQuantity, outputUnitOfMeasureId, createdByUserId, effectiveFrom, effectiveTo);
        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        FinishedProductId = finishedProductId;
        OutputQuantity = outputQuantity;
        OutputUnitOfMeasureId = outputUnitOfMeasureId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Notes = NormalizeOptionalText(notes);
        IsActive = true;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; }
    public Guid FinishedProductId { get; private set; }
    public decimal OutputQuantity { get; private set; }
    public Guid OutputUnitOfMeasureId { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<BillOfMaterialsComponent> Components => _components.AsReadOnly();

    public bool IsEffectiveOn(DateOnly date) =>
        IsActive && (!EffectiveFrom.HasValue || EffectiveFrom.Value <= date) && (!EffectiveTo.HasValue || EffectiveTo.Value >= date);

    public void Update(
        Guid finishedProductId,
        decimal outputQuantity,
        Guid outputUnitOfMeasureId,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        string? notes,
        bool isActive,
        DateTimeOffset updatedAt)
    {
        ValidateHeader(CompanyId, Code, finishedProductId, outputQuantity, outputUnitOfMeasureId, CreatedByUserId, effectiveFrom, effectiveTo);
        FinishedProductId = finishedProductId;
        OutputQuantity = outputQuantity;
        OutputUnitOfMeasureId = outputUnitOfMeasureId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Notes = NormalizeOptionalText(notes);
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    public void ReplaceComponents(IEnumerable<BillOfMaterialsComponent> components, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(components);
        var replacement = components.ToArray();
        if (replacement.Length == 0) throw new ArgumentException("A bill of materials requires at least one component.", nameof(components));
        if (replacement.Any(component => component.ComponentProductId == FinishedProductId)) throw new ArgumentException("A finished product cannot be its own component.", nameof(components));
        if (replacement.GroupBy(component => component.ComponentProductId).Any(group => group.Count() > 1)) throw new ArgumentException("A component can appear only once in a bill of materials.", nameof(components));

        _components.Clear();
        _components.AddRange(replacement.OrderBy(component => component.LineNumber));
        UpdatedAt = updatedAt;
    }

    private static void ValidateHeader(Guid companyId, string code, Guid finishedProductId, decimal outputQuantity, Guid outputUnitOfMeasureId, Guid createdByUserId, DateOnly? effectiveFrom, DateOnly? effectiveTo)
    {
        if (companyId == Guid.Empty || finishedProductId == Guid.Empty || outputUnitOfMeasureId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Company, finished product, output unit, and creator are required.");
        }
        if (outputQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(outputQuantity), "Output quantity must be greater than zero.");
        if (effectiveTo.HasValue && effectiveFrom.HasValue && effectiveTo < effectiveFrom) throw new ArgumentException("The effective end date cannot be before the effective start date.", nameof(effectiveTo));
        _ = Money.RequireText(code, nameof(code));
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class BillOfMaterialsComponent : Entity
{
    private BillOfMaterialsComponent()
    {
    }

    public BillOfMaterialsComponent(int lineNumber, Guid componentProductId, decimal quantity, Guid unitOfMeasureId, Guid id = default)
        : base(id)
    {
        if (lineNumber <= 0) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (componentProductId == Guid.Empty || unitOfMeasureId == Guid.Empty) throw new ArgumentException("A component product and unit of measure are required.");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Component quantity must be greater than zero.");
        LineNumber = lineNumber;
        ComponentProductId = componentProductId;
        Quantity = quantity;
        UnitOfMeasureId = unitOfMeasureId;
    }

    public int LineNumber { get; private set; }
    public Guid ComponentProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid UnitOfMeasureId { get; private set; }
}

public sealed class ProductionOrder : AggregateRoot
{
    private readonly List<ProductionOrderMaterial> _materials = [];

    private ProductionOrder()
    {
    }

    public ProductionOrder(
        Guid companyId,
        string productionOrderNumber,
        Guid finishedProductId,
        Guid billOfMaterialsId,
        decimal plannedQuantity,
        Guid outputUnitOfMeasureId,
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        DateOnly productionDate,
        Guid createdByUserId,
        string? notes = null,
        DateTimeOffset? createdAt = null,
        Guid id = default)
        : base(id)
    {
        ValidateDraft(companyId, productionOrderNumber, finishedProductId, billOfMaterialsId, plannedQuantity, outputUnitOfMeasureId, sourceWarehouseId, destinationWarehouseId, createdByUserId);
        CompanyId = companyId;
        ProductionOrderNumber = Money.RequireText(productionOrderNumber, nameof(productionOrderNumber)).ToUpperInvariant();
        FinishedProductId = finishedProductId;
        BillOfMaterialsId = billOfMaterialsId;
        PlannedQuantity = plannedQuantity;
        OutputUnitOfMeasureId = outputUnitOfMeasureId;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        ProductionDate = productionDate;
        Notes = NormalizeOptionalText(notes);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        Status = ProductionOrderStatus.Draft;
    }

    public Guid CompanyId { get; private set; }
    public string ProductionOrderNumber { get; private set; }
    public Guid FinishedProductId { get; private set; }
    public Guid BillOfMaterialsId { get; private set; }
    public decimal PlannedQuantity { get; private set; }
    public decimal? ActualProducedQuantity { get; private set; }
    public Guid OutputUnitOfMeasureId { get; private set; }
    public Guid SourceWarehouseId { get; private set; }
    public Guid DestinationWarehouseId { get; private set; }
    public DateOnly ProductionDate { get; private set; }
    public ProductionOrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ReleasedByUserId { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public IReadOnlyCollection<ProductionOrderMaterial> Materials => _materials.AsReadOnly();

    public void UpdateDraft(Guid finishedProductId, Guid billOfMaterialsId, decimal plannedQuantity, Guid outputUnitOfMeasureId, Guid sourceWarehouseId, Guid destinationWarehouseId, DateOnly productionDate, string? notes)
    {
        EnsureDraft();
        ValidateDraft(CompanyId, ProductionOrderNumber, finishedProductId, billOfMaterialsId, plannedQuantity, outputUnitOfMeasureId, sourceWarehouseId, destinationWarehouseId, CreatedByUserId);
        FinishedProductId = finishedProductId;
        BillOfMaterialsId = billOfMaterialsId;
        PlannedQuantity = plannedQuantity;
        OutputUnitOfMeasureId = outputUnitOfMeasureId;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        ProductionDate = productionDate;
        Notes = NormalizeOptionalText(notes);
    }

    public void ReplaceMaterials(IEnumerable<ProductionOrderMaterial> materials)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(materials);
        var replacement = materials.ToArray();
        if (replacement.Length == 0) throw new ArgumentException("A production order requires material requirements.", nameof(materials));
        if (replacement.Any(material => material.ComponentProductId == FinishedProductId)) throw new ArgumentException("A production order cannot consume its finished product as a material.", nameof(materials));
        if (replacement.GroupBy(material => material.ComponentProductId).Any(group => group.Count() > 1)) throw new ArgumentException("A material can appear only once in a production order.", nameof(materials));
        _materials.Clear();
        _materials.AddRange(replacement.OrderBy(material => material.LineNumber));
    }

    public void Release(Guid releasedByUserId, DateTimeOffset releasedAt)
    {
        EnsureDraft();
        if (_materials.Count == 0) throw new InvalidOperationException("A production order requires material requirements before release.");
        if (releasedByUserId == Guid.Empty) throw new ArgumentException("The releasing user is required.", nameof(releasedByUserId));
        Status = ProductionOrderStatus.Released;
        ReleasedByUserId = releasedByUserId;
        ReleasedAt = releasedAt;
    }

    public void Complete(decimal actualProducedQuantity, Guid postedByUserId, DateTimeOffset postedAt)
    {
        if (Status != ProductionOrderStatus.Released) throw new InvalidOperationException("Only released production orders can be completed.");
        if (actualProducedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(actualProducedQuantity), "Actual produced quantity must be greater than zero.");
        if (postedByUserId == Guid.Empty) throw new ArgumentException("The posting user is required.", nameof(postedByUserId));
        Status = ProductionOrderStatus.Completed;
        ActualProducedQuantity = actualProducedQuantity;
        PostedByUserId = postedByUserId;
        PostedAt = postedAt;
    }

    private void EnsureDraft()
    {
        if (Status != ProductionOrderStatus.Draft) throw new InvalidOperationException("Only draft production orders can be changed.");
    }

    private static void ValidateDraft(Guid companyId, string productionOrderNumber, Guid finishedProductId, Guid billOfMaterialsId, decimal plannedQuantity, Guid outputUnitOfMeasureId, Guid sourceWarehouseId, Guid destinationWarehouseId, Guid createdByUserId)
    {
        if (companyId == Guid.Empty || finishedProductId == Guid.Empty || billOfMaterialsId == Guid.Empty || outputUnitOfMeasureId == Guid.Empty || sourceWarehouseId == Guid.Empty || destinationWarehouseId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Company, product, bill of materials, units, warehouses, and creator are required.");
        }
        if (sourceWarehouseId == destinationWarehouseId) throw new ArgumentException("Source and destination warehouses must be different.");
        if (plannedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(plannedQuantity), "Planned quantity must be greater than zero.");
        _ = Money.RequireText(productionOrderNumber, nameof(productionOrderNumber));
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ProductionOrderMaterial : Entity
{
    private ProductionOrderMaterial()
    {
    }

    public ProductionOrderMaterial(int lineNumber, Guid componentProductId, decimal quantityPerBomOutput, decimal bomOutputQuantity, Guid unitOfMeasureId, Guid id = default)
        : base(id)
    {
        if (lineNumber <= 0) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (componentProductId == Guid.Empty || unitOfMeasureId == Guid.Empty) throw new ArgumentException("A material product and unit of measure are required.");
        if (quantityPerBomOutput <= 0 || bomOutputQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantityPerBomOutput), "Material and bill output quantities must be greater than zero.");
        LineNumber = lineNumber;
        ComponentProductId = componentProductId;
        QuantityPerBomOutput = quantityPerBomOutput;
        BomOutputQuantity = bomOutputQuantity;
        UnitOfMeasureId = unitOfMeasureId;
    }

    public int LineNumber { get; private set; }
    public Guid ComponentProductId { get; private set; }
    public decimal QuantityPerBomOutput { get; private set; }
    public decimal BomOutputQuantity { get; private set; }
    public Guid UnitOfMeasureId { get; private set; }

    public decimal QuantityRequiredFor(decimal actualProducedQuantity) => decimal.Round(QuantityPerBomOutput * actualProducedQuantity / BomOutputQuantity, 6, MidpointRounding.AwayFromZero);
}

#pragma warning restore CS8618
