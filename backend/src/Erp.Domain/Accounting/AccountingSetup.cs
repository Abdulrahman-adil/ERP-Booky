using Erp.Domain.Common;

namespace Erp.Domain.Accounting;

#pragma warning disable CS8618

public enum AccountingPeriodStatus
{
    Open,
    Closed,
    Locked
}

public enum CashBankAccountType
{
    Cash,
    Bank
}

public sealed class AccountingPeriod : AggregateRoot
{
    private AccountingPeriod()
    {
    }

    public AccountingPeriod(Guid companyId, string name, DateRange dateRange, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        CompanyId = companyId;
        Name = Money.RequireText(name, nameof(name));
        DateRange = dateRange ?? throw new ArgumentNullException(nameof(dateRange));
        Status = AccountingPeriodStatus.Open;
    }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; }

    public DateRange DateRange { get; private set; }

    public AccountingPeriodStatus Status { get; private set; }

    public bool Includes(DateOnly date) => DateRange.StartDate <= date && date <= DateRange.EndDate;

    public void SetStatus(AccountingPeriodStatus status) => Status = status;
}

public sealed class CashBankAccount : AggregateRoot
{
    private CashBankAccount()
    {
    }

    public CashBankAccount(
        Guid companyId,
        string name,
        CashBankAccountType accountType,
        Guid? postingAccountId = null,
        Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        CompanyId = companyId;
        Name = Money.RequireText(name, nameof(name));
        AccountType = accountType;
        PostingAccountId = postingAccountId;
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; }

    public CashBankAccountType AccountType { get; private set; }

    public Guid? PostingAccountId { get; private set; }

    public bool IsActive { get; private set; }

    public void SetPostingAccount(Guid? postingAccountId) => PostingAccountId = postingAccountId;

    public void Rename(string name) => Name = Money.RequireText(name, nameof(name));

    public void SetActive(bool isActive) => IsActive = isActive;
}

public sealed class PostingProfile : AggregateRoot
{
    private readonly List<PostingProfileEntry> _entries = [];

    private PostingProfile()
    {
    }

    public PostingProfile(Guid companyId, string name, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        CompanyId = companyId;
        Name = Money.RequireText(name, nameof(name));
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<PostingProfileEntry> Entries => _entries.AsReadOnly();

    public void AddMapping(string postingKey, Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("An account is required.", nameof(accountId));
        }

        var normalizedPostingKey = Money.RequireText(postingKey, nameof(postingKey)).ToUpperInvariant();

        if (_entries.Any(entry => string.Equals(entry.PostingKey, normalizedPostingKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Posting keys must be unique within a posting profile.", nameof(postingKey));
        }

        _entries.Add(new PostingProfileEntry(Id, normalizedPostingKey, accountId));
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void Rename(string name) => Name = Money.RequireText(name, nameof(name));

    public void ReplaceMappings(IEnumerable<(string PostingKey, Guid AccountId)> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        var replacement = mappings.ToArray();
        if (replacement.Any(mapping => mapping.AccountId == Guid.Empty)) throw new ArgumentException("Each posting mapping requires an account.", nameof(mappings));
        if (replacement.GroupBy(mapping => Money.RequireText(mapping.PostingKey, nameof(mappings)).ToUpperInvariant()).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Posting keys must be unique within a posting profile.", nameof(mappings));
        }

        _entries.Clear();
        foreach (var mapping in replacement) AddMapping(mapping.PostingKey, mapping.AccountId);
    }
}

public sealed class PostingProfileEntry : Entity
{
    private PostingProfileEntry()
    {
    }

    internal PostingProfileEntry(Guid postingProfileId, string postingKey, Guid accountId, Guid id = default)
        : base(id)
    {
        PostingProfileId = postingProfileId;
        PostingKey = postingKey;
        AccountId = accountId;
    }

    public Guid PostingProfileId { get; private set; }

    public string PostingKey { get; private set; }

    public Guid AccountId { get; private set; }
}

#pragma warning restore CS8618
