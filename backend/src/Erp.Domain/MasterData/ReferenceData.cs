using Erp.Domain.Common;

namespace Erp.Domain.MasterData;

#pragma warning disable CS8618

public sealed class Currency : Entity
{
    private Currency()
    {
    }

    public Currency(string code, string name, int decimalPlaces, Guid id = default)
        : base(id)
    {
        Code = new Money(0, code).CurrencyCode;
        Name = Money.RequireText(name, nameof(name));

        if (decimalPlaces is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        }

        DecimalPlaces = decimalPlaces;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public int DecimalPlaces { get; private set; }

}

public sealed class UnitOfMeasure : Entity
{
    private UnitOfMeasure()
    {
    }

    public UnitOfMeasure(
        string code,
        string name,
        string dimension,
        decimal conversionFactorToBase,
        int decimalPlaces,
        Guid? companyId = null,
        Guid id = default)
        : base(id)
    {
        if (conversionFactorToBase <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversionFactorToBase));
        }

        if (decimalPlaces is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        }

        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        Dimension = Money.RequireText(dimension, nameof(dimension));
        ConversionFactorToBase = conversionFactorToBase;
        DecimalPlaces = decimalPlaces;
    }

    public Guid? CompanyId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string Dimension { get; private set; }

    public decimal ConversionFactorToBase { get; private set; }

    public int DecimalPlaces { get; private set; }

    public void Update(
        string code,
        string name,
        string dimension,
        decimal conversionFactorToBase,
        int decimalPlaces)
    {
        if (conversionFactorToBase <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversionFactorToBase));
        }

        if (decimalPlaces is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        }

        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        Dimension = Money.RequireText(dimension, nameof(dimension));
        ConversionFactorToBase = conversionFactorToBase;
        DecimalPlaces = decimalPlaces;
    }
}

public sealed class ProductCategory : Entity
{
    private ProductCategory()
    {
    }

    public ProductCategory(Guid companyId, string code, string name, Guid? parentCategoryId = null, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        if (parentCategoryId == id && id != Guid.Empty)
        {
            throw new ArgumentException("A category cannot be its own parent.", nameof(parentCategoryId));
        }

        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        ParentCategoryId = parentCategoryId;
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string code, string name, Guid? parentCategoryId)
    {
        if (parentCategoryId == Id)
        {
            throw new ArgumentException("A category cannot be its own parent.", nameof(parentCategoryId));
        }

        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        ParentCategoryId = parentCategoryId;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

public sealed class TaxCode : Entity
{
    private TaxCode()
    {
    }

    public TaxCode(
        Guid companyId,
        string code,
        decimal rate,
        bool isRecoverable,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        if (rate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(rate));
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("The effective end date cannot be before the start date.", nameof(effectiveTo));
        }

        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Rate = rate;
        IsRecoverable = isRecoverable;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Code { get; private set; }

    public decimal Rate { get; private set; }

    public bool IsRecoverable { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public bool IsActive { get; private set; }
}

#pragma warning restore CS8618
