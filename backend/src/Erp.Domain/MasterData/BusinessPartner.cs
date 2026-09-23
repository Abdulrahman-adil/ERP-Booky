using Erp.Domain.Common;

namespace Erp.Domain.MasterData;

#pragma warning disable CS8618

public sealed class BusinessPartner : AggregateRoot
{
    private BusinessPartner()
    {
    }

    public BusinessPartner(
        Guid companyId,
        string code,
        string legalName,
        string? taxIdentifier = null,
        int paymentTermsDays = 0,
        Address? address = null,
        ContactDetails? contactDetails = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        if (paymentTermsDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays));
        }

        CompanyId = companyId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        LegalName = Money.RequireText(legalName, nameof(legalName));
        Address = address;
        ContactDetails = contactDetails;
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : taxIdentifier.Trim();
        PaymentTermsDays = paymentTermsDays;
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Code { get; private set; }

    public string LegalName { get; private set; }

    public string? TaxIdentifier { get; private set; }

    public Address? Address { get; private set; }

    public ContactDetails? ContactDetails { get; private set; }

    public int PaymentTermsDays { get; private set; }

    public bool IsActive { get; private set; }

    public CustomerProfile? CustomerProfile { get; private set; }

    public SupplierProfile? SupplierProfile { get; private set; }

    public void EnableCustomer(Money? creditLimit = null, Guid? receivableAccountId = null)
    {
        CustomerProfile ??= new CustomerProfile(Id, creditLimit, receivableAccountId);
    }

    public void EnableSupplier(string? supplierReference = null, Guid? payableAccountId = null)
    {
        SupplierProfile ??= new SupplierProfile(Id, supplierReference, payableAccountId);
    }

    public void Update(
        string code,
        string legalName,
        Address? address,
        ContactDetails? contactDetails,
        string? taxIdentifier,
        int paymentTermsDays)
    {
        if (paymentTermsDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays));
        }

        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        LegalName = Money.RequireText(legalName, nameof(legalName));
        Address = address;
        ContactDetails = contactDetails;
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : taxIdentifier.Trim();
        PaymentTermsDays = paymentTermsDays;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

public sealed class CustomerProfile : Entity
{
    private CustomerProfile()
    {
    }

    internal CustomerProfile(Guid businessPartnerId, Money? creditLimit, Guid? receivableAccountId, Guid id = default)
        : base(id)
    {
        BusinessPartnerId = businessPartnerId;
        CreditLimit = creditLimit;
        ReceivableAccountId = receivableAccountId;
    }

    public Guid BusinessPartnerId { get; private set; }

    public Money? CreditLimit { get; private set; }

    public Guid? ReceivableAccountId { get; private set; }
}

public sealed class SupplierProfile : Entity
{
    private SupplierProfile()
    {
    }

    internal SupplierProfile(Guid businessPartnerId, string? supplierReference, Guid? payableAccountId, Guid id = default)
        : base(id)
    {
        BusinessPartnerId = businessPartnerId;
        SupplierReference = string.IsNullOrWhiteSpace(supplierReference) ? null : supplierReference.Trim();
        PayableAccountId = payableAccountId;
    }

    public Guid BusinessPartnerId { get; private set; }

    public string? SupplierReference { get; private set; }

    public Guid? PayableAccountId { get; private set; }
}

#pragma warning restore CS8618
