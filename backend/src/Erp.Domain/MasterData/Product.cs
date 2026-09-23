using Erp.Domain.Common;

namespace Erp.Domain.MasterData;

#pragma warning disable CS8618

public enum ProductType
{
    Stock,
    NonStock,
    Service
}

public enum InventoryItemPurpose
{
    RawMaterial,
    FinishedGood,
    PackagingMaterial,
    SemiFinishedGood,
    Consumable,
    TradingItem
}

public sealed class Product : AggregateRoot
{
    private Product()
    {
    }

    public Product(
        Guid companyId,
        string sku,
        string name,
        ProductType productType,
        Guid? stockUnitOfMeasureId = null,
        Guid? productCategoryId = null,
        Guid? postingProfileId = null,
        InventoryItemPurpose? inventoryPurpose = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        if (stockUnitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("A stock unit of measure is required.", nameof(stockUnitOfMeasureId));
        }

        CompanyId = companyId;
        Sku = Money.RequireText(sku, nameof(sku)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        ProductType = productType;
        StockUnitOfMeasureId = stockUnitOfMeasureId;
        ProductCategoryId = productCategoryId;
        PostingProfileId = postingProfileId;
        InventoryPurpose = inventoryPurpose;
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public ProductType ProductType { get; private set; }

    public Guid? StockUnitOfMeasureId { get; private set; }

    public Guid? ProductCategoryId { get; private set; }

    public Guid? PostingProfileId { get; private set; }

    public InventoryItemPurpose? InventoryPurpose { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsStockTracked => this.ProductType == global::Erp.Domain.MasterData.ProductType.Stock && StockUnitOfMeasureId.HasValue;

    public void Update(
        string sku,
        string name,
        ProductType productType,
        Guid? stockUnitOfMeasureId,
        Guid? productCategoryId,
        Guid? postingProfileId = null,
        InventoryItemPurpose? inventoryPurpose = null)
    {
        if (stockUnitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("A stock unit of measure is required.", nameof(stockUnitOfMeasureId));
        }

        Sku = Money.RequireText(sku, nameof(sku)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        ProductType = productType;
        StockUnitOfMeasureId = stockUnitOfMeasureId;
        ProductCategoryId = productCategoryId;
        PostingProfileId = postingProfileId;
        InventoryPurpose = inventoryPurpose;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

#pragma warning restore CS8618
