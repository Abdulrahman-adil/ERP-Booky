namespace Erp.Domain.Common;

public sealed record Money
{
    public Money(decimal amount, string currencyCode)
    {
        if (decimal.Round(amount, 4) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Monetary amounts support up to four decimal places.");
        }

        Amount = amount;
        CurrencyCode = NormalizeCurrencyCode(currencyCode);
    }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    private static string NormalizeCurrencyCode(string currencyCode)
    {
        var normalizedCode = RequireText(currencyCode, nameof(currencyCode)).ToUpperInvariant();

        if (normalizedCode.Length != 3)
        {
            throw new ArgumentException("Currency codes must contain three characters.", nameof(currencyCode));
        }

        return normalizedCode;
    }

    internal static string RequireText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}

public sealed record Quantity
{
    public Quantity(decimal amount, Guid unitOfMeasureId)
    {
        if (unitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("A unit of measure is required.", nameof(unitOfMeasureId));
        }

        Amount = amount;
        UnitOfMeasureId = unitOfMeasureId;
    }

    public decimal Amount { get; }

    public Guid UnitOfMeasureId { get; }
}

public sealed record Address
{
    public Address(string line1, string countryCode, string? line2 = null, string? city = null, string? postalCode = null)
    {
        Line1 = Money.RequireText(line1, nameof(line1));
        CountryCode = Money.RequireText(countryCode, nameof(countryCode)).ToUpperInvariant();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
    }

    public string Line1 { get; }

    public string? Line2 { get; }

    public string? City { get; }

    public string? PostalCode { get; }

    public string CountryCode { get; }
}

public sealed record ContactDetails
{
    public ContactDetails(string? email = null, string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("At least one contact method is required.");
        }

        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    public string? Email { get; }

    public string? Phone { get; }
}

public sealed record SourceReference
{
    public SourceReference(string module, Guid aggregateId, string eventType)
    {
        if (aggregateId == Guid.Empty)
        {
            throw new ArgumentException("A source aggregate is required.", nameof(aggregateId));
        }

        Module = Money.RequireText(module, nameof(module));
        AggregateId = aggregateId;
        EventType = Money.RequireText(eventType, nameof(eventType));
    }

    public string Module { get; }

    public Guid AggregateId { get; }

    public string EventType { get; }
}

public sealed record DocumentNumber
{
    public DocumentNumber(string value)
    {
        Value = Money.RequireText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record DateRange
{
    public DateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot be before the start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }
}

public sealed record TaxSnapshot
{
    public TaxSnapshot(Guid taxCodeId, decimal rate)
    {
        if (taxCodeId == Guid.Empty)
        {
            throw new ArgumentException("A tax code is required.", nameof(taxCodeId));
        }

        if (rate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Tax rates must be between zero and 100.");
        }

        TaxCodeId = taxCodeId;
        Rate = rate;
    }

    public Guid TaxCodeId { get; }

    public decimal Rate { get; }
}
