using Erp.Domain.Common;

namespace Erp.Domain.Organizations;

#pragma warning disable CS8618

public sealed class Organization : AggregateRoot
{
    private Organization()
    {
    }

    public Organization(string name, string code, Guid id = default)
        : base(id)
    {
        Name = Money.RequireText(name, nameof(name));
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        IsActive = true;
    }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public bool IsActive { get; private set; }
}

public sealed class Company : AggregateRoot
{
    private Company()
    {
    }

    public Company(
        Guid organizationId,
        string name,
        string code,
        string baseCurrencyCode,
        string? taxIdentifier = null,
        Guid id = default)
        : base(id)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An organization is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        Name = Money.RequireText(name, nameof(name));
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        BaseCurrencyCode = new Money(0, baseCurrencyCode).CurrencyCode;
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : taxIdentifier.Trim();
        IsActive = true;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public string BaseCurrencyCode { get; private set; }

    public string? TaxIdentifier { get; private set; }

    public bool IsActive { get; private set; }
}

#pragma warning restore CS8618
