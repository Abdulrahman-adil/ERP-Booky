using Erp.Application.Accounting;
using Erp.Application.MasterData;
using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.MasterData;
using Erp.Domain.Payments;
using Erp.Domain.Purchasing;
using Erp.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Persistence;

public sealed class EfAccountingService(ErpDbContext dbContext) : IAccountingService
{
    public async Task<IReadOnlyCollection<AccountDto>> GetAccountsAsync(Guid companyId, string? search, AccountType? accountType, bool? isActive, CancellationToken cancellationToken = default)
    {
        var chartId = await dbContext.ChartsOfAccounts.AsNoTracking().Where(chart => chart.CompanyId == companyId).Select(chart => (Guid?)chart.Id).SingleOrDefaultAsync(cancellationToken);
        if (!chartId.HasValue) return [];
        var query = dbContext.Accounts.AsNoTracking().Where(account => account.ChartOfAccountsId == chartId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(account => account.Code.ToLower().Contains(term) || account.Name.ToLower().Contains(term));
        }
        if (accountType.HasValue) query = query.Where(account => account.AccountType == accountType.Value);
        if (isActive.HasValue) query = query.Where(account => account.IsActive == isActive.Value);
        return await ToAccountDtosAsync(await query.OrderBy(account => account.Code).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<AccountDto> CreateAccountAsync(Guid companyId, AccountInput input, CancellationToken cancellationToken = default)
    {
        var chart = await dbContext.ChartsOfAccounts.Include(item => item.Accounts).SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (chart is null)
        {
            chart = new ChartOfAccounts(companyId, "Main Chart of Accounts");
            dbContext.ChartsOfAccounts.Add(chart);
        }

        var account = chart.AddAccount(input.Code, input.Name, input.AccountType, input.AccountRole, input.IsPosting, input.ParentAccountId, normalBalanceOverride: input.NormalBalanceOverride);
        if (!input.IsActive) account.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToAccountDtosAsync([account], cancellationToken)).Single();
    }

    public async Task<AccountDto?> UpdateAccountAsync(Guid companyId, Guid id, AccountInput input, CancellationToken cancellationToken = default)
    {
        var chart = await dbContext.ChartsOfAccounts.SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (chart is null) return null;
        var accounts = await dbContext.Accounts.Where(account => account.ChartOfAccountsId == chart.Id).ToListAsync(cancellationToken);
        var account = accounts.SingleOrDefault(item => item.Id == id);
        if (account is null) return null;
        if (accounts.Any(item => item.Id != id && string.Equals(item.Code, input.Code.Trim(), StringComparison.OrdinalIgnoreCase))) throw new AccountingConflictException("Account codes must be unique within the chart of accounts.");
        var parent = input.ParentAccountId is null ? null : accounts.SingleOrDefault(item => item.Id == input.ParentAccountId.Value);
        if (input.ParentAccountId is not null && parent is null) throw new AccountingValidationException("The parent account must belong to this chart of accounts.");
        if (parent?.IsPosting == true) throw new AccountingValidationException("Posting accounts cannot contain child accounts.");
        if (parent is not null && parent.AccountType != input.AccountType) throw new AccountingValidationException("Child accounts must use the same account type as their parent.");
        if (input.ParentAccountId == id || IsDescendantOf(accounts, input.ParentAccountId, id)) throw new AccountingValidationException("An account cannot be its own parent or a child of itself.");
        if (input.IsPosting && accounts.Any(item => item.ParentAccountId == id)) throw new AccountingValidationException("Accounts with child accounts must remain header accounts.");
        var level = parent is null ? 0 : parent.HierarchyLevel + 1;
        account.Update(input.Code, input.Name, input.AccountType, input.AccountRole, input.IsPosting, input.ParentAccountId, level, input.NormalBalanceOverride);
        if (input.IsActive) account.Activate(); else account.Deactivate();
        RelevelDescendants(accounts, account.Id, account.HierarchyLevel);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToAccountDtosAsync([account], cancellationToken)).Single();
    }

    public async Task<bool> DeactivateAccountAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var chartId = await dbContext.ChartsOfAccounts.Where(chart => chart.CompanyId == companyId).Select(chart => (Guid?)chart.Id).SingleOrDefaultAsync(cancellationToken);
        if (!chartId.HasValue) return false;
        var account = await dbContext.Accounts.SingleOrDefaultAsync(item => item.Id == id && item.ChartOfAccountsId == chartId.Value, cancellationToken);
        if (account is null) return false;
        if (await dbContext.Accounts.AnyAsync(item => item.ParentAccountId == id && item.IsActive, cancellationToken)) throw new AccountingConflictException("Deactivate child accounts before deactivating a header account.");
        if (await dbContext.JournalEntryLines.AnyAsync(line => line.AccountId == id, cancellationToken) ||
            await dbContext.PostingProfileEntries.AnyAsync(entry => entry.AccountId == id, cancellationToken) ||
            await dbContext.CashBankAccounts.AnyAsync(cashBank => cashBank.PostingAccountId == id, cancellationToken) ||
            await dbContext.CustomerProfiles.AnyAsync(profile => profile.ReceivableAccountId == id, cancellationToken) ||
            await dbContext.SupplierProfiles.AnyAsync(profile => profile.PayableAccountId == id, cancellationToken))
        {
            throw new AccountingConflictException("Accounts used by financial history or active configuration cannot be deactivated.");
        }
        account.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<FinancialClassDto>> GetFinancialClassesAsync(Guid companyId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = dbContext.FinancialClasses.AsNoTracking().Where(item => item.CompanyId == companyId);
        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);
        return await query.OrderBy(item => item.Code).Select(item => new FinancialClassDto(item.Id, item.Code, item.Name, item.IsActive)).ToListAsync(cancellationToken);
    }

    public async Task<FinancialClassDto> CreateFinancialClassAsync(Guid companyId, FinancialClassInput input, CancellationToken cancellationToken = default)
    {
        var financialClass = new FinancialClass(companyId, input.Code, input.Name);
        financialClass.Update(input.Code, input.Name, input.IsActive);
        dbContext.FinancialClasses.Add(financialClass);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new FinancialClassDto(financialClass.Id, financialClass.Code, financialClass.Name, financialClass.IsActive);
    }

    public async Task<FinancialClassDto?> UpdateFinancialClassAsync(Guid companyId, Guid id, FinancialClassInput input, CancellationToken cancellationToken = default)
    {
        var financialClass = await dbContext.FinancialClasses.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (financialClass is null) return null;
        financialClass.Update(input.Code, input.Name, input.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new FinancialClassDto(financialClass.Id, financialClass.Code, financialClass.Name, financialClass.IsActive);
    }

    public async Task<IReadOnlyCollection<AccountingPeriodDto>> GetPeriodsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.AccountingPeriods.AsNoTracking().Where(period => period.CompanyId == companyId).OrderByDescending(period => period.DateRange.StartDate)
            .Select(period => new AccountingPeriodDto(period.Id, period.Name, period.DateRange.StartDate, period.DateRange.EndDate, period.Status)).ToListAsync(cancellationToken);

    public async Task<AccountingPeriodDto> CreatePeriodAsync(Guid companyId, AccountingPeriodInput input, CancellationToken cancellationToken = default)
    {
        if (input.EndDate < input.StartDate) throw new AccountingValidationException("The period end date cannot be before its start date.");
        if (await dbContext.AccountingPeriods.AnyAsync(period => period.CompanyId == companyId && period.DateRange.StartDate <= input.EndDate && period.DateRange.EndDate >= input.StartDate, cancellationToken))
        {
            throw new AccountingConflictException("Accounting periods cannot overlap.");
        }
        var period = new AccountingPeriod(companyId, input.Name, new DateRange(input.StartDate, input.EndDate));
        period.SetStatus(input.Status);
        dbContext.AccountingPeriods.Add(period);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AccountingPeriodDto(period.Id, period.Name, period.DateRange.StartDate, period.DateRange.EndDate, period.Status);
    }

    public async Task<AccountingPeriodDto?> SetPeriodStatusAsync(Guid companyId, Guid id, AccountingPeriodStatus status, CancellationToken cancellationToken = default)
    {
        var period = await dbContext.AccountingPeriods.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (period is null) return null;
        period.SetStatus(status);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AccountingPeriodDto(period.Id, period.Name, period.DateRange.StartDate, period.DateRange.EndDate, period.Status);
    }

    public async Task<IReadOnlyCollection<PostingProfileDto>> GetPostingProfilesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var profiles = await dbContext.PostingProfiles.AsNoTracking().Include(profile => profile.Entries).Where(profile => profile.CompanyId == companyId).OrderBy(profile => profile.Name).ToListAsync(cancellationToken);
        return await ToPostingProfileDtosAsync(profiles, cancellationToken);
    }

    public async Task<PostingProfileDto> SavePostingProfileAsync(Guid companyId, Guid? id, PostingProfileInput input, CancellationToken cancellationToken = default)
    {
        ValidatePostingKeys(input.Mappings);
        var chartId = await GetChartIdAsync(companyId, cancellationToken);
        var accountIds = input.Mappings.Select(mapping => mapping.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts.Where(account => accountIds.Contains(account.Id) && account.ChartOfAccountsId == chartId).ToDictionaryAsync(account => account.Id, cancellationToken);
        if (accounts.Count != accountIds.Length || accounts.Values.Any(account => !account.IsActive || !account.IsPosting)) throw new AccountingValidationException("Posting profile mappings must use active posting accounts from the company chart.");
        var profile = id.HasValue
            ? await dbContext.PostingProfiles.Include(item => item.Entries).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id.Value, cancellationToken)
            : null;
        if (id.HasValue && profile is null) throw new AccountingValidationException("The posting profile was not found.");
        if (profile is null)
        {
            profile = new PostingProfile(companyId, input.Name);
            dbContext.PostingProfiles.Add(profile);
        }
        else
        {
            dbContext.PostingProfileEntries.RemoveRange(profile.Entries.ToArray());
            profile.Rename(input.Name);
        }

        if (input.IsActive)
        {
            var others = await dbContext.PostingProfiles.Where(item => item.CompanyId == companyId && item.Id != profile.Id && item.IsActive).ToListAsync(cancellationToken);
            foreach (var other in others) other.SetActive(false);
        }
        profile.SetActive(input.IsActive);
        profile.ReplaceMappings(input.Mappings.Select(mapping => (mapping.PostingKey, mapping.AccountId)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToPostingProfileDtosAsync([profile], cancellationToken)).Single();
    }

    public async Task<IReadOnlyCollection<CashBankGlMappingDto>> GetCashBankMappingsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var chartId = await dbContext.ChartsOfAccounts.Where(chart => chart.CompanyId == companyId).Select(chart => (Guid?)chart.Id).SingleOrDefaultAsync(cancellationToken);
        var accounts = chartId.HasValue ? await dbContext.Accounts.AsNoTracking().Where(account => account.ChartOfAccountsId == chartId.Value).ToDictionaryAsync(account => account.Id, cancellationToken) : new Dictionary<Guid, Account>();
        var cashBanks = await dbContext.CashBankAccounts.AsNoTracking().Where(account => account.CompanyId == companyId).OrderBy(account => account.Name).ToListAsync(cancellationToken);
        return cashBanks.Select(cashBank => accounts.TryGetValue(cashBank.PostingAccountId ?? Guid.Empty, out var account)
            ? new CashBankGlMappingDto(cashBank.Id, cashBank.Name, cashBank.AccountType.ToString(), account.Id, account.Code, account.Name)
            : new CashBankGlMappingDto(cashBank.Id, cashBank.Name, cashBank.AccountType.ToString(), null, null, null)).ToArray();
    }

    public async Task<CashBankGlMappingDto?> SetCashBankMappingAsync(Guid companyId, Guid cashBankAccountId, Guid? postingAccountId, CancellationToken cancellationToken = default)
    {
        var cashBank = await dbContext.CashBankAccounts.SingleOrDefaultAsync(account => account.CompanyId == companyId && account.Id == cashBankAccountId, cancellationToken);
        if (cashBank is null) return null;
        Account? postingAccount = null;
        if (postingAccountId.HasValue)
        {
            var chartId = await GetChartIdAsync(companyId, cancellationToken);
            postingAccount = await dbContext.Accounts.SingleOrDefaultAsync(account => account.Id == postingAccountId.Value && account.ChartOfAccountsId == chartId, cancellationToken);
            if (postingAccount is null || !postingAccount.IsActive || !postingAccount.IsPosting || postingAccount.AccountRole is not AccountRole.Cash and not AccountRole.Bank)
            {
                throw new AccountingValidationException("Cash and bank accounts must map to an active Cash or Bank posting account.");
            }
        }
        cashBank.SetPostingAccount(postingAccountId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return postingAccount is null
            ? new CashBankGlMappingDto(cashBank.Id, cashBank.Name, cashBank.AccountType.ToString(), null, null, null)
            : new CashBankGlMappingDto(cashBank.Id, cashBank.Name, cashBank.AccountType.ToString(), postingAccount.Id, postingAccount.Code, postingAccount.Name);
    }

    public async Task<PagedResult<JournalEntryDto>> GetJournalsAsync(Guid companyId, JournalEntryQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query.PageNumber, query.PageSize);
        var entries = dbContext.JournalEntries.AsNoTracking().Include(entry => entry.Lines).Where(entry => entry.CompanyId == companyId);
        if (query.Status.HasValue) entries = entries.Where(entry => entry.Status == query.Status.Value);
        if (query.FromDate.HasValue) entries = entries.Where(entry => entry.EntryDate >= query.FromDate.Value);
        if (query.ToDate.HasValue) entries = entries.Where(entry => entry.EntryDate <= query.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var term = search.ToLowerInvariant();
            var documentNumber = new DocumentNumber(search);
            entries = entries.Where(entry =>
                entry.Description.ToLower().Contains(term) ||
                (entry.Reference ?? string.Empty).ToLower().Contains(term) ||
                entry.DocumentNumber == documentNumber);
        }
        var total = await entries.CountAsync(cancellationToken);
        var page = await entries.OrderByDescending(entry => entry.EntryDate).ThenByDescending(entry => entry.DocumentNumber).Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<JournalEntryDto>(await ToJournalDtosAsync(page, cancellationToken), normalized.PageNumber, normalized.PageSize, total);
    }

    public async Task<JournalEntryDto?> GetJournalAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var journal = await dbContext.JournalEntries.AsNoTracking().Include(entry => entry.Lines).SingleOrDefaultAsync(entry => entry.CompanyId == companyId && entry.Id == id, cancellationToken);
        return journal is null ? null : (await ToJournalDtosAsync([journal], cancellationToken)).Single();
    }

    public async Task<JournalEntryDto> CreateManualJournalAsync(Guid companyId, Guid userId, JournalEntryInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new AccountingValidationException("The authenticated user could not be identified.");
        var company = await GetCompanyAsync(companyId, cancellationToken);
        var period = await FindPeriodAsync(companyId, input.EntryDate, false, cancellationToken);
        var journalId = Guid.NewGuid();
        var source = new AccountingTransaction(companyId, new SourceReference("Accounting", journalId, "ManualJournal"), input.EntryDate);
        var number = new DocumentNumber(await NextJournalNumberAsync(input.EntryDate, cancellationToken));
        var journal = new JournalEntry(companyId, source.Id, period.Id, input.EntryDate, company.BaseCurrencyCode, input.Description, number, input.Reference, userId, DateTimeOffset.UtcNow, journalId);
        journal.ReplaceLines(await CreateJournalLinesAsync(companyId, company.BaseCurrencyCode, input.Lines, cancellationToken));
        dbContext.AccountingTransactions.Add(source);
        dbContext.JournalEntries.Add(journal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetJournalAsync(companyId, journal.Id, cancellationToken))!;
    }

    public async Task<JournalEntryDto?> UpdateManualJournalAsync(Guid companyId, Guid id, JournalEntryInput input, CancellationToken cancellationToken = default)
    {
        var journal = await dbContext.JournalEntries.Include(entry => entry.Lines).SingleOrDefaultAsync(entry => entry.CompanyId == companyId && entry.Id == id, cancellationToken);
        if (journal is null) return null;
        if (journal.Status != JournalEntryStatus.Draft) throw new AccountingConflictException("Posted journal entries are immutable. Use a correcting entry in a later workflow.");
        var source = await dbContext.AccountingTransactions.SingleAsync(transaction => transaction.Id == journal.AccountingTransactionId, cancellationToken);
        if (!string.Equals(source.SourceReference.Module, "Accounting", StringComparison.Ordinal)) throw new AccountingConflictException("Only manual journals can be edited here.");
        var company = await GetCompanyAsync(companyId, cancellationToken);
        await FindPeriodAsync(companyId, input.EntryDate, false, cancellationToken);
        dbContext.JournalEntryLines.RemoveRange(journal.Lines.ToArray());
        journal.UpdateDraft(input.EntryDate, input.Description, input.Reference);
        journal.ReplaceLines(await CreateJournalLinesAsync(companyId, company.BaseCurrencyCode, input.Lines, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetJournalAsync(companyId, id, cancellationToken);
    }

    public async Task<JournalEntryDto?> PostJournalAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var journal = await dbContext.JournalEntries.Include(entry => entry.Lines).SingleOrDefaultAsync(entry => entry.CompanyId == companyId && entry.Id == id, cancellationToken);
        if (journal is null) return null;
        await PostJournalCoreAsync(journal, userId, cancellationToken);
        return await GetJournalAsync(companyId, id, cancellationToken);
    }

    public async Task<PagedResult<GeneralLedgerLineDto>> GetGeneralLedgerAsync(Guid companyId, GeneralLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from ledger in dbContext.GeneralLedgerEntries.AsNoTracking()
            join journal in dbContext.JournalEntries.AsNoTracking() on ledger.JournalEntryId equals journal.Id
            join line in dbContext.JournalEntryLines.AsNoTracking() on ledger.JournalEntryLineId equals line.Id
            join account in dbContext.Accounts.AsNoTracking() on ledger.AccountId equals account.Id
            join source in dbContext.AccountingTransactions.AsNoTracking() on journal.AccountingTransactionId equals source.Id
            where ledger.CompanyId == companyId && journal.Status == JournalEntryStatus.Posted
            select new { ledger, journal, line, account, source })
            .Where(item => !query.AccountId.HasValue || item.ledger.AccountId == query.AccountId.Value)
            .Where(item => !query.BusinessPartnerId.HasValue || item.line.BusinessPartnerId == query.BusinessPartnerId.Value)
            .Where(item => !query.FinancialClassId.HasValue || item.line.FinancialClassId == query.FinancialClassId.Value)
            .Where(item => string.IsNullOrWhiteSpace(query.SourceModule) || item.source.SourceReference.Module == query.SourceModule)
            .Where(item => !query.FromDate.HasValue || item.ledger.PostedDate >= query.FromDate.Value)
            .Where(item => !query.ToDate.HasValue || item.ledger.PostedDate <= query.ToDate.Value)
            .OrderBy(item => item.ledger.PostedDate).ThenBy(item => item.journal.Id).ThenBy(item => item.line.LineNumber)
            .ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.Reference))
        {
            var term = query.Reference.Trim();
            rows = rows.Where(item => (item.journal.Reference ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) || item.journal.Description.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var partnerIds = rows.Where(item => item.line.BusinessPartnerId.HasValue).Select(item => item.line.BusinessPartnerId!.Value).Distinct().ToArray();
        var classIds = rows.Where(item => item.line.FinancialClassId.HasValue).Select(item => item.line.FinancialClassId!.Value).Distinct().ToArray();
        var partners = partnerIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.BusinessPartners.AsNoTracking().Where(partner => partnerIds.Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, partner => partner.LegalName, cancellationToken);
        var classes = classIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.FinancialClasses.AsNoTracking().Where(item => classIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var balances = new Dictionary<Guid, decimal>();
        var results = new List<GeneralLedgerLineDto>();
        foreach (var row in rows)
        {
            var movement = row.account.NormalBalance == AccountNormalBalance.Debit ? row.ledger.Debit.Amount - row.ledger.Credit.Amount : row.ledger.Credit.Amount - row.ledger.Debit.Amount;
            balances[row.account.Id] = balances.GetValueOrDefault(row.account.Id) + movement;
            results.Add(new GeneralLedgerLineDto(row.ledger.Id, row.ledger.PostedDate, row.journal.DocumentNumber?.Value ?? "—", row.account.Id, row.account.Code, row.account.Name, row.line.Memo ?? row.journal.Description, row.line.BusinessPartnerId.HasValue && partners.TryGetValue(row.line.BusinessPartnerId.Value, out var partner) ? partner : null, row.line.FinancialClassId.HasValue && classes.TryGetValue(row.line.FinancialClassId.Value, out var financialClass) ? financialClass : null, row.ledger.Debit.Amount, row.ledger.Credit.Amount, balances[row.account.Id], row.source.SourceReference.Module, row.source.SourceReference.EventType, row.source.SourceReference.AggregateId, row.journal.Reference));
        }
        var normalized = Normalize(query.PageNumber, query.PageSize);
        return new PagedResult<GeneralLedgerLineDto>(results.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToArray(), normalized.PageNumber, normalized.PageSize, results.Count);
    }

    public async Task<IReadOnlyCollection<PendingAccountingTransactionDto>> GetPendingTransactionsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var sources = await dbContext.AccountingTransactions.AsNoTracking().Where(transaction => transaction.CompanyId == companyId && transaction.Status == AccountingTransactionStatus.Pending).OrderBy(transaction => transaction.TransactionDate).ToListAsync(cancellationToken);
        var journalIds = sources.Where(source => string.Equals(source.SourceReference.Module, "Accounting", StringComparison.Ordinal)).Select(source => source.SourceReference.AggregateId).ToArray();
        var journalNumbers = journalIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.JournalEntries.AsNoTracking().Where(journal => journalIds.Contains(journal.Id)).ToDictionaryAsync(journal => journal.Id, journal => journal.DocumentNumber!.Value, cancellationToken);
        return sources.Select(source => new PendingAccountingTransactionDto(source.Id, source.TransactionDate, source.SourceReference.Module, source.SourceReference.EventType, source.SourceReference.AggregateId, source.Status.ToString(), journalNumbers.GetValueOrDefault(source.SourceReference.AggregateId))).ToArray();
    }

    public async Task<PendingPostingResult> PostPendingTransactionsAsync(Guid companyId, Guid userId, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        var requested = transactionIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        var sources = await dbContext.AccountingTransactions.AsNoTracking().Where(source => source.CompanyId == companyId && source.Status == AccountingTransactionStatus.Pending && (requested.Length == 0 || requested.Contains(source.Id))).OrderBy(source => source.TransactionDate).Select(source => source.Id).ToListAsync(cancellationToken);
        var posted = 0;
        var failures = new List<string>();
        foreach (var sourceId in sources)
        {
            await using var transaction = dbContext.Database.IsRelational() ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                await PostAccountingTransactionAsync(companyId, sourceId, userId, cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                posted++;
            }
            catch (Exception exception) when (exception is AccountingValidationException or AccountingConflictException or InvalidOperationException)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                failures.Add($"{sourceId}: {exception.Message}");
            }
        }
        return new PendingPostingResult(posted, failures);
    }

    public async Task PostOperationalTransactionIfConfiguredAsync(Guid companyId, Guid accountingTransactionId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.PostingProfiles.AnyAsync(profile => profile.CompanyId == companyId && profile.IsActive, cancellationToken)) return;
        await PostAccountingTransactionAsync(companyId, accountingTransactionId, userId, cancellationToken);
    }

    private async Task PostAccountingTransactionAsync(Guid companyId, Guid accountingTransactionId, Guid userId, CancellationToken cancellationToken)
    {
        var source = await dbContext.AccountingTransactions.SingleOrDefaultAsync(transaction => transaction.CompanyId == companyId && transaction.Id == accountingTransactionId, cancellationToken)
            ?? throw new AccountingValidationException("The accounting transaction was not found.");
        if (source.Status == AccountingTransactionStatus.Posted) return;
        if (string.Equals(source.SourceReference.Module, "Accounting", StringComparison.Ordinal))
        {
            var manualJournal = await dbContext.JournalEntries.Include(entry => entry.Lines).SingleOrDefaultAsync(entry => entry.Id == source.SourceReference.AggregateId, cancellationToken)
                ?? throw new AccountingValidationException("The manual journal for this accounting transaction was not found.");
            await PostJournalCoreAsync(manualJournal, userId, cancellationToken);
            return;
        }
        var existing = await dbContext.JournalEntries.AnyAsync(journal => journal.AccountingTransactionId == source.Id, cancellationToken);
        if (existing)
        {
            source.MarkPosted();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var company = await GetCompanyAsync(companyId, cancellationToken);
        var profile = await dbContext.PostingProfiles.Include(item => item.Entries).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.IsActive, cancellationToken)
            ?? throw new AccountingValidationException("Activate a posting profile before posting operational accounting transactions.");
        var posting = await BuildOperationalPostingAsync(companyId, source, profile, cancellationToken);
        var period = await FindPeriodAsync(companyId, source.TransactionDate, true, cancellationToken);
        var journal = new JournalEntry(companyId, source.Id, period.Id, source.TransactionDate, company.BaseCurrencyCode, posting.Description, new DocumentNumber(await NextJournalNumberAsync(source.TransactionDate, cancellationToken)), posting.Reference, userId, DateTimeOffset.UtcNow);
        journal.ReplaceLines(await CreateJournalLinesAsync(companyId, company.BaseCurrencyCode, posting.Lines, cancellationToken));
        dbContext.JournalEntries.Add(journal);
        await PostJournalCoreAsync(journal, userId, cancellationToken, source);
    }

    private async Task PostJournalCoreAsync(JournalEntry journal, Guid userId, CancellationToken cancellationToken, AccountingTransaction? suppliedSource = null)
    {
        if (journal.Status != JournalEntryStatus.Draft) throw new AccountingConflictException("Only draft journal entries can be posted.");
        var period = await FindPeriodAsync(journal.CompanyId, journal.EntryDate, true, cancellationToken);
        if (period.Id != journal.AccountingPeriodId) throw new AccountingValidationException("The journal entry date must remain within its accounting period.");
        await ValidateJournalLinesAsync(journal.CompanyId, journal.CurrencyCode, journal.Lines.Select(line => new JournalEntryLineInput(line.AccountId, line.Debit.Amount, line.Credit.Amount, line.BusinessPartnerId, line.FinancialClassId, line.Memo)).ToArray(), cancellationToken);
        journal.MarkPosted(userId, DateTimeOffset.UtcNow);
        var source = suppliedSource ?? await dbContext.AccountingTransactions.SingleAsync(transaction => transaction.Id == journal.AccountingTransactionId, cancellationToken);
        source.MarkPosted();
        foreach (var line in journal.Lines)
        {
            dbContext.GeneralLedgerEntries.Add(new GeneralLedgerEntry(
                journal.CompanyId,
                period.Id,
                journal.Id,
                line.Id,
                line.AccountId,
                new Money(line.Debit.Amount, line.Debit.CurrencyCode),
                new Money(line.Credit.Amount, line.Credit.CurrencyCode),
                journal.EntryDate));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<OperationalPosting> BuildOperationalPostingAsync(Guid companyId, AccountingTransaction source, PostingProfile profile, CancellationToken cancellationToken)
    {
        var mappings = await ResolveMappingsAsync(companyId, profile, cancellationToken);
        var reference = source.SourceReference;
        if (reference.Module == "Sales" && reference.EventType == "SalesInvoicePosted")
        {
            var invoice = await dbContext.SalesInvoices.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == reference.AggregateId && item.Status == SalesInvoiceStatus.Posted, cancellationToken) ?? throw new AccountingValidationException("The posted sales invoice source was not found.");
            return new OperationalPosting($"Sales invoice {invoice.DocumentNumber.Value}", invoice.DocumentNumber.Value,
            [
                new JournalEntryLineInput(GetMapping(mappings, PostingKeys.SalesReceivable, AccountRole.AccountsReceivable), invoice.TotalAmount.Amount, 0, invoice.CustomerId, null, "Accounts receivable"),
                new JournalEntryLineInput(GetMapping(mappings, PostingKeys.SalesRevenue, AccountRole.SalesRevenue), 0, invoice.TotalAmount.Amount, null, null, "Sales revenue")
            ]);
        }
        if (reference.Module == "Payments" && reference.EventType == "CustomerPaymentPosted")
        {
            var payment = await dbContext.Payments.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == reference.AggregateId && item.Direction == PaymentDirection.Incoming && item.Status == PaymentStatus.Posted, cancellationToken) ?? throw new AccountingValidationException("The posted customer payment source was not found.");
            var cash = await ResolveCashBankPostingAccountAsync(companyId, payment.CashBankAccountId, cancellationToken);
            return new OperationalPosting($"Customer payment {payment.DocumentNumber.Value}", payment.DocumentNumber.Value,
            [
                new JournalEntryLineInput(cash.Id, payment.Amount.Amount, 0, null, null, "Cash or bank received"),
                new JournalEntryLineInput(GetMapping(mappings, PostingKeys.CustomerPaymentReceivable, AccountRole.AccountsReceivable), 0, payment.Amount.Amount, payment.BusinessPartnerId, null, "Accounts receivable")
            ]);
        }
        if (reference.Module == "Purchasing" && reference.EventType == "PurchaseInvoicePosted")
        {
            var bill = await dbContext.PurchaseInvoices.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == reference.AggregateId && item.Status == PurchaseInvoiceStatus.Posted, cancellationToken) ?? throw new AccountingValidationException("The posted purchase bill source was not found.");
            return new OperationalPosting($"Purchase bill {bill.DocumentNumber.Value}", bill.DocumentNumber.Value,
            [
                new JournalEntryLineInput(GetMapping(mappings, PostingKeys.PurchaseInventory, AccountRole.Inventory), bill.TotalAmount.Amount, 0, null, null, "Inventory or purchase cost"),
                new JournalEntryLineInput(GetMapping(mappings, PostingKeys.PurchasePayable, AccountRole.AccountsPayable), 0, bill.TotalAmount.Amount, bill.SupplierId, null, "Accounts payable")
            ]);
        }
        if (reference.Module == "Purchasing" && reference.EventType == "SupplierPaymentPosted")
        {
            var payment = await dbContext.Payments.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == reference.AggregateId && item.Direction == PaymentDirection.Outgoing && item.Status == PaymentStatus.Posted, cancellationToken) ?? throw new AccountingValidationException("The posted supplier payment source was not found.");
            var cash = await ResolveCashBankPostingAccountAsync(companyId, payment.CashBankAccountId, cancellationToken);
            return new OperationalPosting($"Supplier payment {payment.DocumentNumber.Value}", payment.DocumentNumber.Value,
            [
                new JournalEntryLineInput(GetMapping(mappings, PostingKeys.SupplierPaymentPayable, AccountRole.AccountsPayable), payment.Amount.Amount, 0, payment.BusinessPartnerId, null, "Accounts payable"),
                new JournalEntryLineInput(cash.Id, 0, payment.Amount.Amount, null, null, "Cash or bank paid")
            ]);
        }
        throw new AccountingValidationException($"No accounting posting strategy is registered for {reference.Module}/{reference.EventType}.");
    }

    private async Task<IReadOnlyCollection<JournalEntryLine>> CreateJournalLinesAsync(Guid companyId, string currencyCode, IReadOnlyCollection<JournalEntryLineInput> inputs, CancellationToken cancellationToken)
    {
        await ValidateJournalLinesAsync(companyId, currencyCode, inputs, cancellationToken);
        return inputs.Select((line, index) => new JournalEntryLine(index + 1, line.AccountId, new Money(line.Debit, currencyCode), new Money(line.Credit, currencyCode), line.BusinessPartnerId, null, line.FinancialClassId, line.Memo)).ToArray();
    }

    private async Task ValidateJournalLinesAsync(Guid companyId, string currencyCode, IReadOnlyCollection<JournalEntryLineInput> inputs, CancellationToken cancellationToken)
    {
        if (inputs is null || inputs.Count == 0) throw new AccountingValidationException("A journal entry requires at least one line.");
        if (inputs.Any(line => line.AccountId == Guid.Empty || line.Debit < 0 || line.Credit < 0 || (line.Debit > 0) == (line.Credit > 0))) throw new AccountingValidationException("Each journal line requires one account and exactly one positive debit or credit amount.");
        var totalDebit = inputs.Sum(line => line.Debit);
        var totalCredit = inputs.Sum(line => line.Credit);
        if (totalDebit <= 0 || totalDebit != totalCredit) throw new AccountingValidationException("Journal entries require non-zero, equal debit and credit totals.");
        var chartId = await GetChartIdAsync(companyId, cancellationToken);
        var accountIds = inputs.Select(line => line.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts.AsNoTracking().Where(account => account.ChartOfAccountsId == chartId && accountIds.Contains(account.Id)).ToDictionaryAsync(account => account.Id, cancellationToken);
        if (accounts.Count != accountIds.Length || accounts.Values.Any(account => !account.IsActive || !account.IsPosting)) throw new AccountingValidationException("Journal lines must use active posting accounts from the company chart.");
        var partnerIds = inputs.Where(line => line.BusinessPartnerId.HasValue).Select(line => line.BusinessPartnerId!.Value).Distinct().ToArray();
        var partners = partnerIds.Length == 0 ? new Dictionary<Guid, BusinessPartner>() : await dbContext.BusinessPartners.AsNoTracking().Include(partner => partner.CustomerProfile).Include(partner => partner.SupplierProfile).Where(partner => partner.CompanyId == companyId && partnerIds.Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, cancellationToken);
        var classIds = inputs.Where(line => line.FinancialClassId.HasValue).Select(line => line.FinancialClassId!.Value).Distinct().ToArray();
        var classes = classIds.Length == 0 ? new Dictionary<Guid, FinancialClass>() : await dbContext.FinancialClasses.AsNoTracking().Where(item => item.CompanyId == companyId && item.IsActive && classIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (classes.Count != classIds.Length) throw new AccountingValidationException("Journal line classes must be active and belong to the company.");
        foreach (var line in inputs)
        {
            var account = accounts[line.AccountId];
            if (account.RequiresBusinessPartner && !line.BusinessPartnerId.HasValue) throw new AccountingValidationException($"A business partner is required for {account.Name}.");
            if (!line.BusinessPartnerId.HasValue) continue;
            if (!partners.TryGetValue(line.BusinessPartnerId.Value, out var partner) || !partner.IsActive) throw new AccountingValidationException("The selected business partner is unavailable.");
            if (account.AccountRole == AccountRole.AccountsReceivable && partner.CustomerProfile is null) throw new AccountingValidationException("Accounts receivable lines require a valid customer.");
            if (account.AccountRole == AccountRole.AccountsPayable && partner.SupplierProfile is null) throw new AccountingValidationException("Accounts payable lines require a valid supplier.");
        }
    }

    private async Task<Dictionary<string, Account>> ResolveMappingsAsync(Guid companyId, PostingProfile profile, CancellationToken cancellationToken)
    {
        var chartId = await GetChartIdAsync(companyId, cancellationToken);
        var accountIds = profile.Entries.Select(entry => entry.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts.AsNoTracking().Where(account => account.ChartOfAccountsId == chartId && accountIds.Contains(account.Id)).ToDictionaryAsync(account => account.Id, cancellationToken);
        return profile.Entries.ToDictionary(entry => entry.PostingKey, entry => accounts.TryGetValue(entry.AccountId, out var account) ? account : throw new AccountingValidationException($"The account mapped to {entry.PostingKey} is unavailable."), StringComparer.OrdinalIgnoreCase);
    }

    private static Guid GetMapping(IReadOnlyDictionary<string, Account> mappings, string key, AccountRole expectedRole)
    {
        if (!mappings.TryGetValue(key, out var account)) throw new AccountingValidationException($"The active posting profile is missing the {key} mapping.");
        if (!account.IsActive || !account.IsPosting || account.AccountRole != expectedRole) throw new AccountingValidationException($"The {key} mapping must use an active {expectedRole} posting account.");
        return account.Id;
    }

    private async Task<Account> ResolveCashBankPostingAccountAsync(Guid companyId, Guid cashBankAccountId, CancellationToken cancellationToken)
    {
        var cashBank = await dbContext.CashBankAccounts.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == cashBankAccountId, cancellationToken) ?? throw new AccountingValidationException("The cash or bank account was not found.");
        if (!cashBank.PostingAccountId.HasValue) throw new AccountingValidationException($"Map a general ledger account to cash or bank account '{cashBank.Name}' before posting payments.");
        var chartId = await GetChartIdAsync(companyId, cancellationToken);
        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == cashBank.PostingAccountId.Value && item.ChartOfAccountsId == chartId, cancellationToken);
        if (account is null || !account.IsActive || !account.IsPosting || account.AccountRole is not AccountRole.Cash and not AccountRole.Bank) throw new AccountingValidationException("The cash or bank mapping must reference an active Cash or Bank posting account.");
        return account;
    }

    private async Task<AccountingPeriod> FindPeriodAsync(Guid companyId, DateOnly date, bool requireOpen, CancellationToken cancellationToken)
    {
        var period = await dbContext.AccountingPeriods.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.DateRange.StartDate <= date && item.DateRange.EndDate >= date, cancellationToken)
            ?? throw new AccountingValidationException($"No accounting period covers {date:yyyy-MM-dd}.");
        if (requireOpen && period.Status != AccountingPeriodStatus.Open) throw new AccountingConflictException($"Accounting period '{period.Name}' is not open for posting.");
        return period;
    }

    private async Task<Erp.Domain.Organizations.Company> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        await dbContext.Companies.SingleOrDefaultAsync(company => company.Id == companyId && company.IsActive, cancellationToken) ?? throw new AccountingValidationException("The selected company is unavailable.");

    private async Task<Guid> GetChartIdAsync(Guid companyId, CancellationToken cancellationToken) =>
        await dbContext.ChartsOfAccounts.Where(chart => chart.CompanyId == companyId).Select(chart => (Guid?)chart.Id).SingleOrDefaultAsync(cancellationToken) ?? throw new AccountingValidationException("Create a chart of accounts before configuring accounting.");

    private async Task<string> NextJournalNumberAsync(DateOnly date, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var nextValue = await dbContext.JournalEntries.CountAsync(cancellationToken) + 1;
            return $"JE-{date:yyyy}-{nextValue:D6}";
        }

        var connection = dbContext.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT nextval('erp.journal_entry_number_sequence')";
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return $"JE-{date:yyyy}-{value:D6}";
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyCollection<AccountDto>> ToAccountDtosAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken)
    {
        if (accounts.Count == 0) return [];
        var accountIds = accounts.Select(account => account.Id).ToArray();
        var movements = await dbContext.GeneralLedgerEntries.AsNoTracking().Where(entry => accountIds.Contains(entry.AccountId)).GroupBy(entry => entry.AccountId).Select(group => new { AccountId = group.Key, Debit = group.Sum(item => item.Debit.Amount), Credit = group.Sum(item => item.Credit.Amount) }).ToDictionaryAsync(item => item.AccountId, cancellationToken);
        return accounts.Select(account =>
        {
            var movement = movements.GetValueOrDefault(account.Id);
            var balance = account.NormalBalance == AccountNormalBalance.Debit ? (movement?.Debit ?? 0) - (movement?.Credit ?? 0) : (movement?.Credit ?? 0) - (movement?.Debit ?? 0);
            return new AccountDto(account.Id, account.Code, account.Name, account.AccountType, account.AccountRole, account.DefaultNormalBalance, account.NormalBalanceOverride, account.NormalBalance, account.IsContraAccount, account.ParentAccountId, account.HierarchyLevel, account.IsPosting, account.IsActive, balance);
        }).OrderBy(account => account.Code).ToArray();
    }

    private async Task<IReadOnlyCollection<PostingProfileDto>> ToPostingProfileDtosAsync(IReadOnlyCollection<PostingProfile> profiles, CancellationToken cancellationToken)
    {
        if (profiles.Count == 0) return [];
        var accountIds = profiles.SelectMany(profile => profile.Entries).Select(entry => entry.AccountId).Distinct().ToArray();
        var accounts = accountIds.Length == 0 ? new Dictionary<Guid, Account>() : await dbContext.Accounts.AsNoTracking().Where(account => accountIds.Contains(account.Id)).ToDictionaryAsync(account => account.Id, cancellationToken);
        return profiles.Select(profile => new PostingProfileDto(profile.Id, profile.Name, profile.IsActive, profile.Entries.OrderBy(entry => entry.PostingKey).Select(entry =>
        {
            var account = accounts[entry.AccountId];
            return new PostingProfileMappingDto(entry.PostingKey, account.Id, account.Code, account.Name);
        }).ToArray())).ToArray();
    }

    private async Task<IReadOnlyCollection<JournalEntryDto>> ToJournalDtosAsync(IReadOnlyCollection<JournalEntry> journals, CancellationToken cancellationToken)
    {
        if (journals.Count == 0) return [];
        var accountIds = journals.SelectMany(journal => journal.Lines).Select(line => line.AccountId).Distinct().ToArray();
        var partnerIds = journals.SelectMany(journal => journal.Lines).Where(line => line.BusinessPartnerId.HasValue).Select(line => line.BusinessPartnerId!.Value).Distinct().ToArray();
        var classIds = journals.SelectMany(journal => journal.Lines).Where(line => line.FinancialClassId.HasValue).Select(line => line.FinancialClassId!.Value).Distinct().ToArray();
        var sourceIds = journals.Select(journal => journal.AccountingTransactionId).Distinct().ToArray();
        var accounts = await dbContext.Accounts.AsNoTracking().Where(account => accountIds.Contains(account.Id)).ToDictionaryAsync(account => account.Id, cancellationToken);
        var partners = partnerIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.BusinessPartners.AsNoTracking().Where(partner => partnerIds.Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, partner => partner.LegalName, cancellationToken);
        var classes = classIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.FinancialClasses.AsNoTracking().Where(item => classIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var sources = await dbContext.AccountingTransactions.AsNoTracking().Where(source => sourceIds.Contains(source.Id)).ToDictionaryAsync(source => source.Id, cancellationToken);
        return journals.Select(journal =>
        {
            var source = sources[journal.AccountingTransactionId].SourceReference;
            return new JournalEntryDto(journal.Id, journal.DocumentNumber?.Value ?? "—", journal.EntryDate, journal.Description, journal.Reference, source.Module, source.EventType, source.AggregateId, journal.Status, journal.TotalDebit, journal.TotalCredit, journal.CreatedByUserId, journal.CreatedAt, journal.PostedByUserId, journal.PostedAt, journal.Lines.OrderBy(line => line.LineNumber).Select(line =>
            {
                var account = accounts[line.AccountId];
                return new JournalEntryLineDto(line.Id, line.LineNumber, account.Id, account.Code, account.Name, line.BusinessPartnerId, line.BusinessPartnerId.HasValue && partners.TryGetValue(line.BusinessPartnerId.Value, out var partner) ? partner : null, line.FinancialClassId, line.FinancialClassId.HasValue && classes.TryGetValue(line.FinancialClassId.Value, out var financialClass) ? financialClass : null, line.Debit.Amount, line.Credit.Amount, line.Memo);
            }).ToArray());
        }).ToArray();
    }

    private static void ValidatePostingKeys(IReadOnlyCollection<PostingProfileMappingInput> mappings)
    {
        if (mappings is null || mappings.Count == 0) throw new AccountingValidationException("A posting profile requires at least one mapping.");
        if (mappings.Any(mapping => string.IsNullOrWhiteSpace(mapping.PostingKey) || !PostingKeys.All.Contains(mapping.PostingKey.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase))) throw new AccountingValidationException("Posting profiles can only use supported accounting mapping keys.");
        if (mappings.GroupBy(mapping => mapping.PostingKey.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new AccountingValidationException("Posting profile mapping keys must be unique.");
    }

    private static bool IsDescendantOf(IReadOnlyCollection<Account> accounts, Guid? candidateParentId, Guid accountId)
    {
        var parentId = candidateParentId;
        while (parentId.HasValue)
        {
            if (parentId.Value == accountId) return true;
            parentId = accounts.SingleOrDefault(item => item.Id == parentId.Value)?.ParentAccountId;
        }
        return false;
    }

    private static void RelevelDescendants(IReadOnlyCollection<Account> accounts, Guid parentId, int parentLevel)
    {
        foreach (var child in accounts.Where(item => item.ParentAccountId == parentId))
        {
            child.Update(child.Code, child.Name, child.AccountType, child.AccountRole, child.IsPosting, child.ParentAccountId, parentLevel + 1, child.NormalBalanceOverride);
            RelevelDescendants(accounts, child.Id, child.HierarchyLevel);
        }
    }

    private static (int PageNumber, int PageSize) Normalize(int pageNumber, int pageSize) => (Math.Max(1, pageNumber), Math.Clamp(pageSize, 1, 200));

    private sealed record OperationalPosting(string Description, string Reference, IReadOnlyCollection<JournalEntryLineInput> Lines);
}
