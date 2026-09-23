using Erp.Domain.Common;

namespace Erp.Domain.Accounting;

#pragma warning disable CS8618

public sealed class FinancialClass : AggregateRoot
{
    private FinancialClass()
    {
    }

    public FinancialClass(Guid companyId, string code, string name, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("A company is required.", nameof(companyId));
        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string code, string name, bool isActive)
    {
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        IsActive = isActive;
    }
}

#pragma warning restore CS8618
