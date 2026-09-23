using Erp.Domain.Common;

namespace Erp.Domain.Accounting;

#pragma warning disable CS8618

public enum AccountingTransactionStatus
{
    Pending,
    Posted,
    Reversed
}

public enum OpenItemType
{
    Receivable,
    Payable
}

public sealed class AccountingTransaction : AggregateRoot
{
    private AccountingTransaction()
    {
    }

    public AccountingTransaction(Guid companyId, SourceReference sourceReference, DateOnly transactionDate, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        CompanyId = companyId;
        SourceReference = sourceReference ?? throw new ArgumentNullException(nameof(sourceReference));
        TransactionDate = transactionDate;
        Status = AccountingTransactionStatus.Pending;
    }

    public Guid CompanyId { get; private set; }

    public SourceReference SourceReference { get; private set; }

    public DateOnly TransactionDate { get; private set; }

    public AccountingTransactionStatus Status { get; private set; }

    public void MarkPosted()
    {
        if (Status != AccountingTransactionStatus.Pending) throw new InvalidOperationException("Only pending accounting transactions can be posted.");
        Status = AccountingTransactionStatus.Posted;
    }
}

public sealed class OpenItem : Entity
{
    private OpenItem()
    {
    }

    public OpenItem(
        Guid companyId,
        Guid businessPartnerId,
        OpenItemType type,
        SourceReference sourceReference,
        DocumentNumber documentNumber,
        Money originalAmount,
        Money? settledAmount = null,
        DateOnly? dueDate = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty || businessPartnerId == Guid.Empty)
        {
            throw new ArgumentException("Company and business partner are required.");
        }

        if (originalAmount is null || originalAmount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "An open item requires a positive original amount.");
        }

        settledAmount ??= new Money(0, originalAmount.CurrencyCode);

        if (!string.Equals(originalAmount.CurrencyCode, settledAmount.CurrencyCode, StringComparison.Ordinal) ||
            settledAmount.Amount < 0)
        {
            throw new ArgumentException("Settled amounts must use the original currency and be non-negative.", nameof(settledAmount));
        }

        if (settledAmount.Amount > originalAmount.Amount)
        {
            throw new ArgumentException("Settled amounts cannot exceed the original amount.", nameof(settledAmount));
        }

        CompanyId = companyId;
        BusinessPartnerId = businessPartnerId;
        Type = type;
        SourceReference = sourceReference ?? throw new ArgumentNullException(nameof(sourceReference));
        DocumentNumber = documentNumber ?? throw new ArgumentNullException(nameof(documentNumber));
        OriginalAmount = originalAmount;
        SettledAmount = settledAmount;
        DueDate = dueDate;
    }

    public Guid CompanyId { get; private set; }

    public Guid BusinessPartnerId { get; private set; }

    public OpenItemType Type { get; private set; }

    public SourceReference SourceReference { get; private set; }

    public DocumentNumber DocumentNumber { get; private set; }

    public Money OriginalAmount { get; private set; }

    public Money SettledAmount { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Money OutstandingAmount => new(OriginalAmount.Amount - SettledAmount.Amount, OriginalAmount.CurrencyCode);

    public void ApplySettlement(Money amount)
    {
        if (amount is null || amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Settlement amounts must be positive.");
        }
        if (!string.Equals(amount.CurrencyCode, OriginalAmount.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException("Settlement currency must match the open item currency.", nameof(amount));
        }
        if (amount.Amount > OutstandingAmount.Amount)
        {
            throw new InvalidOperationException("Settlement cannot exceed the outstanding amount.");
        }

        SettledAmount = new Money(SettledAmount.Amount + amount.Amount, OriginalAmount.CurrencyCode);
    }
}

public sealed class GeneralLedgerEntry : Entity
{
    private GeneralLedgerEntry()
    {
    }

    public GeneralLedgerEntry(
        Guid companyId,
        Guid accountingPeriodId,
        Guid journalEntryId,
        Guid journalEntryLineId,
        Guid accountId,
        Money debit,
        Money credit,
        DateOnly postedDate,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty || accountingPeriodId == Guid.Empty || journalEntryId == Guid.Empty ||
            journalEntryLineId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new ArgumentException("Company, period, journal, line, and account are required.");
        }

        CompanyId = companyId;
        AccountingPeriodId = accountingPeriodId;
        JournalEntryId = journalEntryId;
        JournalEntryLineId = journalEntryLineId;
        AccountId = accountId;
        Debit = debit ?? throw new ArgumentNullException(nameof(debit));
        Credit = credit ?? throw new ArgumentNullException(nameof(credit));
        PostedDate = postedDate;
    }

    public Guid CompanyId { get; private set; }

    public Guid AccountingPeriodId { get; private set; }

    public Guid JournalEntryId { get; private set; }

    public Guid JournalEntryLineId { get; private set; }

    public Guid AccountId { get; private set; }

    public Money Debit { get; private set; }

    public Money Credit { get; private set; }

    public DateOnly PostedDate { get; private set; }
}

#pragma warning restore CS8618
