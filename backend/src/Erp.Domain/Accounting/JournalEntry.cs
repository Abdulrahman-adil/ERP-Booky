using Erp.Domain.Common;

namespace Erp.Domain.Accounting;

#pragma warning disable CS8618

public enum JournalEntryStatus
{
    Draft,
    Posted,
    Reversed
}

public sealed class JournalEntry : AggregateRoot
{
    private readonly List<JournalEntryLine> _lines = [];

    private JournalEntry()
    {
    }

    public JournalEntry(
        Guid companyId,
        Guid accountingTransactionId,
        Guid accountingPeriodId,
        DateOnly entryDate,
        string currencyCode,
        string description,
        DocumentNumber? documentNumber = null,
        string? reference = null,
        Guid? createdByUserId = null,
        DateTimeOffset? createdAt = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty || accountingTransactionId == Guid.Empty || accountingPeriodId == Guid.Empty)
        {
            throw new ArgumentException("Company, accounting transaction, and accounting period are required.");
        }

        CompanyId = companyId;
        AccountingTransactionId = accountingTransactionId;
        AccountingPeriodId = accountingPeriodId;
        EntryDate = entryDate;
        CurrencyCode = new Money(0, currencyCode).CurrencyCode;
        Description = Money.RequireText(description, nameof(description));
        DocumentNumber = documentNumber;
        Reference = NormalizeOptionalText(reference);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Status = JournalEntryStatus.Draft;
    }

    public Guid CompanyId { get; private set; }

    public Guid AccountingTransactionId { get; private set; }

    public Guid AccountingPeriodId { get; private set; }

    public DateOnly EntryDate { get; private set; }

    public string CurrencyCode { get; private set; }

    public string Description { get; private set; }

    public string? Reference { get; private set; }

    public DocumentNumber? DocumentNumber { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset? CreatedAt { get; private set; }

    public Guid? PostedByUserId { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public JournalEntryStatus Status { get; private set; }

    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    public decimal TotalDebit => _lines.Sum(line => line.Debit.Amount);

    public decimal TotalCredit => _lines.Sum(line => line.Credit.Amount);

    public bool IsBalanced => TotalDebit > 0 && TotalDebit == TotalCredit;

    public void UpdateDraft(DateOnly entryDate, string description, string? reference)
    {
        EnsureDraft();
        EntryDate = entryDate;
        Description = Money.RequireText(description, nameof(description));
        Reference = NormalizeOptionalText(reference);
    }

    public void ReplaceLines(IEnumerable<JournalEntryLine> lines)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(lines);
        var replacement = lines.OrderBy(line => line.LineNumber).ToArray();
        if (replacement.Length == 0) throw new ArgumentException("A journal entry requires at least one line.", nameof(lines));
        if (replacement.Any(line => !string.Equals(line.Debit.CurrencyCode, CurrencyCode, StringComparison.Ordinal) || !string.Equals(line.Credit.CurrencyCode, CurrencyCode, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Journal line currency must match the journal entry currency.", nameof(lines));
        }
        if (replacement.GroupBy(line => line.LineNumber).Any(group => group.Count() > 1)) throw new ArgumentException("Journal line numbers must be unique.", nameof(lines));

        _lines.Clear();
        _lines.AddRange(replacement);
    }

    public void AddLine(JournalEntryLine line)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);
        if (!string.Equals(line.Debit.CurrencyCode, CurrencyCode, StringComparison.Ordinal) || !string.Equals(line.Credit.CurrencyCode, CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException("Journal line currency must match the journal entry currency.", nameof(line));
        }
        if (_lines.Any(existingLine => existingLine.LineNumber == line.LineNumber)) throw new ArgumentException("Journal line numbers must be unique.", nameof(line));

        _lines.Add(line);
    }

    public void MarkPosted(Guid postedByUserId, DateTimeOffset postedAt)
    {
        EnsureDraft();
        if (postedByUserId == Guid.Empty) throw new ArgumentException("The posting user is required.", nameof(postedByUserId));
        EnsureBalanced();
        Status = JournalEntryStatus.Posted;
        PostedByUserId = postedByUserId;
        PostedAt = postedAt;
    }

    public void EnsureBalanced()
    {
        if (!IsBalanced) throw new InvalidOperationException("Journal entries require a non-zero, balanced debit and credit total before posting.");
    }

    private void EnsureDraft()
    {
        if (Status != JournalEntryStatus.Draft) throw new InvalidOperationException("Only draft journal entries can be changed.");
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class JournalEntryLine : Entity
{
    private JournalEntryLine()
    {
    }

    public JournalEntryLine(
        int lineNumber,
        Guid accountId,
        Money debit,
        Money credit,
        Guid? businessPartnerId = null,
        Guid? openItemId = null,
        Guid? financialClassId = null,
        string? memo = null,
        Guid id = default)
        : base(id)
    {
        if (lineNumber <= 0) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (accountId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(accountId));
        if (debit is null || credit is null || debit.Amount < 0 || credit.Amount < 0) throw new ArgumentException("Journal debit and credit amounts must be non-negative.");
        if (!string.Equals(debit.CurrencyCode, credit.CurrencyCode, StringComparison.Ordinal)) throw new ArgumentException("Journal debit and credit amounts must use the same currency.");
        if ((debit.Amount > 0) == (credit.Amount > 0)) throw new ArgumentException("Each journal line requires exactly one debit or credit amount.");

        LineNumber = lineNumber;
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        BusinessPartnerId = businessPartnerId;
        OpenItemId = openItemId;
        FinancialClassId = financialClassId;
        Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
    }

    public int LineNumber { get; private set; }

    public Guid AccountId { get; private set; }

    public Money Debit { get; private set; }

    public Money Credit { get; private set; }

    public Guid? BusinessPartnerId { get; private set; }

    public Guid? OpenItemId { get; private set; }

    public Guid? FinancialClassId { get; private set; }

    public string? Memo { get; private set; }
}

#pragma warning restore CS8618
