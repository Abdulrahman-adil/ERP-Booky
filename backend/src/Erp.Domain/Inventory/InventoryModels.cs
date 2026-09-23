using Erp.Domain.Common;

namespace Erp.Domain.Inventory;

#pragma warning disable CS8618

public sealed class Warehouse : AggregateRoot
{
    private Warehouse()
    {
    }

    public Warehouse(
        Guid companyId,
        string code,
        string name,
        string? description = null,
        Address? address = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        Description = NormalizeOptionalText(description);
        Address = address;
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Address? Address { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string name, string? description, Address? address)
    {
        Name = Money.RequireText(name, nameof(name));
        Description = NormalizeOptionalText(description);
        Address = address;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum InventoryMovementType
{
    OpeningBalance,
    StockIn,
    StockOut,
    PositiveAdjustment,
    NegativeAdjustment,
    Receipt,
    Issue,
    Adjustment,
    TransferIn,
    TransferOut,
    ProductionConsumption,
    ProductionReceipt
}

public sealed class InventoryTransaction : Entity
{
    private InventoryTransaction()
    {
    }

    public InventoryTransaction(
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        string movementNumber,
        InventoryMovementType movementType,
        Quantity quantity,
        Quantity? balanceAfterQuantity,
        SourceReference sourceReference,
        DateTimeOffset occurredAt,
        Guid createdByUserId,
        string? externalReference = null,
        string? notes = null,
        DateTimeOffset? createdAt = null,
        Money? costAmount = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty || productId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Company, product, and warehouse are required.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("The user who created the movement is required.", nameof(createdByUserId));
        }

        if (quantity is null)
        {
            throw new ArgumentNullException(nameof(quantity));
        }

        if (quantity.Amount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Inventory movements cannot have a zero quantity.");
        }

        if (balanceAfterQuantity is not null && balanceAfterQuantity.UnitOfMeasureId != quantity.UnitOfMeasureId)
        {
            throw new ArgumentException("The resulting balance must use the movement unit of measure.", nameof(balanceAfterQuantity));
        }

        ValidateDirection(movementType, quantity.Amount);

        CompanyId = companyId;
        ProductId = productId;
        WarehouseId = warehouseId;
        MovementNumber = Money.RequireText(movementNumber, nameof(movementNumber)).ToUpperInvariant();
        MovementType = movementType;
        Quantity = quantity;
        BalanceAfterQuantity = balanceAfterQuantity;
        SourceReference = sourceReference ?? throw new ArgumentNullException(nameof(sourceReference));
        OccurredAt = occurredAt;
        CreatedByUserId = createdByUserId;
        ExternalReference = NormalizeOptionalText(externalReference);
        Notes = NormalizeOptionalText(notes);
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        CostAmount = costAmount;
    }

    public Guid CompanyId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public string? MovementNumber { get; private set; }

    public InventoryMovementType MovementType { get; private set; }

    public Quantity Quantity { get; private set; }

    public Quantity? BalanceAfterQuantity { get; private set; }

    public Money? CostAmount { get; private set; }

    public SourceReference SourceReference { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public string? ExternalReference { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset? CreatedAt { get; private set; }

    private static void ValidateDirection(InventoryMovementType movementType, decimal amount)
    {
        if (movementType is InventoryMovementType.OpeningBalance or InventoryMovementType.StockIn or InventoryMovementType.PositiveAdjustment or InventoryMovementType.Receipt or InventoryMovementType.TransferIn or InventoryMovementType.ProductionReceipt && amount < 0)
        {
            throw new ArgumentException("Inbound inventory movements require a positive quantity.", nameof(amount));
        }

        if (movementType is InventoryMovementType.StockOut or InventoryMovementType.NegativeAdjustment or InventoryMovementType.Issue or InventoryMovementType.TransferOut or InventoryMovementType.ProductionConsumption && amount > 0)
        {
            throw new ArgumentException("Outbound inventory movements require a negative quantity.", nameof(amount));
        }
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class InventoryBalance : Entity
{
    private InventoryBalance()
    {
    }

    public InventoryBalance(
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        Quantity onHandQuantity,
        DateTimeOffset updatedAt,
        Money? inventoryValue = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty || productId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Company, product, and warehouse are required.");
        }

        CompanyId = companyId;
        ProductId = productId;
        WarehouseId = warehouseId;
        OnHandQuantity = onHandQuantity ?? throw new ArgumentNullException(nameof(onHandQuantity));
        InventoryValue = inventoryValue;
        UpdatedAt = updatedAt;
    }

    public Guid CompanyId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Quantity OnHandQuantity { get; private set; }

    public Money? InventoryValue { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void ApplyMovement(Quantity movementQuantity, DateTimeOffset updatedAt)
    {
        if (movementQuantity is null)
        {
            throw new ArgumentNullException(nameof(movementQuantity));
        }

        if (movementQuantity.UnitOfMeasureId != OnHandQuantity.UnitOfMeasureId)
        {
            throw new ArgumentException("The movement unit of measure must match the balance unit of measure.", nameof(movementQuantity));
        }

        var resultingAmount = OnHandQuantity.Amount + movementQuantity.Amount;

        if (resultingAmount < 0)
        {
            throw new InvalidOperationException("The movement would create a negative stock balance.");
        }

        OnHandQuantity = new Quantity(resultingAmount, OnHandQuantity.UnitOfMeasureId);
        UpdatedAt = updatedAt;
    }
}

#pragma warning restore CS8618
