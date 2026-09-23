using Erp.Domain.Common;

namespace Erp.Domain.Payments;

#pragma warning disable CS8618

public enum PaymentDirection
{
    Incoming,
    Outgoing
}

public enum PaymentStatus
{
    Draft,
    Posted,
    Cancelled
}

public sealed class Payment : AggregateRoot
{
    private readonly List<PaymentAllocation> _allocations = [];

    private Payment()
    {
    }

    public Payment(
        Guid companyId,
        Guid businessPartnerId,
        Guid cashBankAccountId,
        PaymentDirection direction,
        Money amount,
        DateOnly paymentDate,
        string? externalReference = null,
        Guid id = default)
        : this(
            companyId,
            businessPartnerId,
            cashBankAccountId,
            direction,
            new DocumentNumber($"PAY-{Guid.NewGuid():N}"),
            amount,
            paymentDate,
            externalReference,
            id: id)
    {
    }

    public Payment(
        Guid companyId,
        Guid businessPartnerId,
        Guid cashBankAccountId,
        PaymentDirection direction,
        DocumentNumber documentNumber,
        Money amount,
        DateOnly paymentDate,
        string? externalReference = null,
        string? notes = null,
        Guid? createdByUserId = null,
        DateTimeOffset? createdAt = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty || businessPartnerId == Guid.Empty || cashBankAccountId == Guid.Empty)
        {
            throw new ArgumentException("Company, business partner, and cash or bank account are required.");
        }

        if (amount is null || amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payments require a positive amount.");
        }

        CompanyId = companyId;
        BusinessPartnerId = businessPartnerId;
        CashBankAccountId = cashBankAccountId;
        Direction = direction;
        DocumentNumber = documentNumber ?? throw new ArgumentNullException(nameof(documentNumber));
        Amount = amount;
        PaymentDate = paymentDate;
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        SourceReference = direction == PaymentDirection.Incoming
            ? new SourceReference("Payments", Id, "CustomerPayment")
            : new SourceReference("Purchasing", Id, "SupplierPayment");
        Status = PaymentStatus.Draft;
    }

    public Guid CompanyId { get; private set; }

    public Guid BusinessPartnerId { get; private set; }

    public Guid CashBankAccountId { get; private set; }

    public PaymentDirection Direction { get; private set; }

    public DocumentNumber DocumentNumber { get; private set; }

    public Money Amount { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public string? ExternalReference { get; private set; }

    public string? Notes { get; private set; }

    public SourceReference SourceReference { get; private set; }

    public PaymentStatus Status { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset? CreatedAt { get; private set; }

    public Guid? PostedByUserId { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations.AsReadOnly();

    public Money AllocatedAmount => new(_allocations.Sum(allocation => allocation.Amount.Amount), Amount.CurrencyCode);

    public Money UnappliedAmount => new(Amount.Amount - AllocatedAmount.Amount, Amount.CurrencyCode);

    public void UpdateDraft(
        Guid businessPartnerId,
        Guid cashBankAccountId,
        Money amount,
        DateOnly paymentDate,
        string? externalReference,
        string? notes)
    {
        EnsureDraft();
        if (businessPartnerId == Guid.Empty || cashBankAccountId == Guid.Empty)
        {
            throw new ArgumentException("Business partner and cash or bank account are required.");
        }
        if (amount is null || amount.Amount <= 0 || !string.Equals(amount.CurrencyCode, Amount.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException("The payment amount must be positive and use the original payment currency.", nameof(amount));
        }

        BusinessPartnerId = businessPartnerId;
        CashBankAccountId = cashBankAccountId;
        Amount = amount;
        PaymentDate = paymentDate;
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void ReplaceAllocations(IEnumerable<PaymentAllocation> allocations)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(allocations);

        var replacement = allocations.ToArray();
        if (replacement.GroupBy(allocation => allocation.OpenItemId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("An open item can only be allocated once per payment.", nameof(allocations));
        }
        if (replacement.Any(allocation => !string.Equals(allocation.Amount.CurrencyCode, Amount.CurrencyCode, StringComparison.Ordinal) || allocation.Amount.Amount <= 0))
        {
            throw new ArgumentException("Payment allocations must be positive and use the payment currency.", nameof(allocations));
        }
        if (replacement.Sum(allocation => allocation.Amount.Amount) > Amount.Amount)
        {
            throw new InvalidOperationException("Payment allocations cannot exceed the payment amount.");
        }

        _allocations.Clear();
        _allocations.AddRange(replacement);
    }

    public void Allocate(Guid openItemId, Money amount)
    {
        if (Status != PaymentStatus.Draft)
        {
            throw new InvalidOperationException("Allocations can only be added to draft payments.");
        }

        if (openItemId == Guid.Empty)
        {
            throw new ArgumentException("An open item is required.", nameof(openItemId));
        }

        if (amount is null || amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Allocation amounts must be positive.");
        }

        if (!string.Equals(amount.CurrencyCode, Amount.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException("Allocation currency must match payment currency.", nameof(amount));
        }

        if (_allocations.Any(allocation => allocation.OpenItemId == openItemId))
        {
            throw new ArgumentException("An open item can only be allocated once per payment.", nameof(openItemId));
        }

        if (_allocations.Sum(allocation => allocation.Amount.Amount) + amount.Amount > Amount.Amount)
        {
            throw new InvalidOperationException("Payment allocations cannot exceed the payment amount.");
        }

        _allocations.Add(new PaymentAllocation(Id, openItemId, amount));
    }

    public void MarkPosted(Guid postedByUserId, DateTimeOffset postedAt)
    {
        EnsureDraft();
        if (postedByUserId == Guid.Empty)
        {
            throw new ArgumentException("The posting user is required.", nameof(postedByUserId));
        }

        Status = PaymentStatus.Posted;
        PostedByUserId = postedByUserId;
        PostedAt = postedAt;
    }

    private void EnsureDraft()
    {
        if (Status != PaymentStatus.Draft)
        {
            throw new InvalidOperationException("Only draft payments can be changed.");
        }
    }
}

public sealed class PaymentAllocation : Entity
{
    private PaymentAllocation()
    {
    }

    public PaymentAllocation(Guid paymentId, Guid openItemId, Money amount, Guid id = default)
        : base(id)
    {
        if (paymentId == Guid.Empty || openItemId == Guid.Empty)
        {
            throw new ArgumentException("Payment and open item are required.");
        }
        if (amount is null || amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Allocation amounts must be positive.");
        }

        PaymentId = paymentId;
        OpenItemId = openItemId;
        Amount = amount;
    }

    public Guid PaymentId { get; private set; }

    public Guid OpenItemId { get; private set; }

    public Money Amount { get; private set; }
}

#pragma warning restore CS8618
