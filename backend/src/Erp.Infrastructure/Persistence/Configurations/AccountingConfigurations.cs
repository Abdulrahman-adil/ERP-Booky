using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Identity;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class ChartOfAccountsConfiguration : IEntityTypeConfiguration<ChartOfAccounts>
{
    public void Configure(EntityTypeBuilder<ChartOfAccounts> builder)
    {
        builder.ConfigureEntity("charts_of_accounts");
        builder.Property(chart => chart.CompanyId).IsRequired();
        builder.Property(chart => chart.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(chart => chart.CompanyId).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(chart => chart.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(chart => chart.Accounts)
            .WithOne()
            .HasForeignKey(account => account.ChartOfAccountsId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(chart => chart.Accounts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ConfigureEntity("accounts");
        builder.Property(account => account.ChartOfAccountsId).IsRequired();
        builder.Property(account => account.Code).HasMaxLength(30).IsRequired();
        builder.Property(account => account.Name).HasMaxLength(150).IsRequired();
        builder.Property(account => account.AccountType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(account => account.AccountRole).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(account => account.NormalBalanceOverride).HasConversion<string>().HasMaxLength(10);
        builder.Property(account => account.HierarchyLevel).IsRequired();
        builder.Property(account => account.IsPosting).IsRequired();
        builder.Property(account => account.IsActive).IsRequired();
        builder.Ignore(account => account.NormalBalance);
        builder.Ignore(account => account.DefaultNormalBalance);
        builder.Ignore(account => account.IsContraAccount);
        builder.Ignore(account => account.RequiresBusinessPartner);
        builder.HasIndex(account => new { account.ChartOfAccountsId, account.Code }).IsUnique();
        builder.HasIndex(account => account.ParentAccountId);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(account => account.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FinancialClassConfiguration : IEntityTypeConfiguration<FinancialClass>
{
    public void Configure(EntityTypeBuilder<FinancialClass> builder)
    {
        builder.ConfigureEntity("financial_classes");
        builder.Property(financialClass => financialClass.CompanyId).IsRequired();
        builder.Property(financialClass => financialClass.Code).HasMaxLength(30).IsRequired();
        builder.Property(financialClass => financialClass.Name).HasMaxLength(150).IsRequired();
        builder.Property(financialClass => financialClass.IsActive).IsRequired();
        builder.HasIndex(financialClass => new { financialClass.CompanyId, financialClass.Code }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(financialClass => financialClass.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ConfigureEntity("accounting_periods");
        builder.Property(period => period.CompanyId).IsRequired();
        builder.Property(period => period.Name).HasMaxLength(100).IsRequired();
        builder.Property(period => period.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(period => new { period.CompanyId, period.Name }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(period => period.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(period => period.DateRange, range => range.ConfigureDateRange());
        builder.Navigation(period => period.DateRange).IsRequired();
    }
}

internal sealed class CashBankAccountConfiguration : IEntityTypeConfiguration<CashBankAccount>
{
    public void Configure(EntityTypeBuilder<CashBankAccount> builder)
    {
        builder.ConfigureEntity("cash_bank_accounts");
        builder.Property(account => account.CompanyId).IsRequired();
        builder.Property(account => account.Name).HasMaxLength(150).IsRequired();
        builder.Property(account => account.AccountType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(account => account.PostingAccountId);
        builder.Property(account => account.IsActive).IsRequired();
        builder.HasIndex(account => new { account.CompanyId, account.Name }).IsUnique();
        builder.HasIndex(account => new { account.CompanyId, account.PostingAccountId }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(account => account.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(account => account.PostingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PostingProfileConfiguration : IEntityTypeConfiguration<PostingProfile>
{
    public void Configure(EntityTypeBuilder<PostingProfile> builder)
    {
        builder.ConfigureEntity("posting_profiles");
        builder.Property(profile => profile.CompanyId).IsRequired();
        builder.Property(profile => profile.Name).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.IsActive).IsRequired();
        builder.HasIndex(profile => new { profile.CompanyId, profile.Name }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(profile => profile.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(profile => profile.Entries)
            .WithOne()
            .HasForeignKey(entry => entry.PostingProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(profile => profile.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PostingProfileEntryConfiguration : IEntityTypeConfiguration<PostingProfileEntry>
{
    public void Configure(EntityTypeBuilder<PostingProfileEntry> builder)
    {
        builder.ConfigureEntity("posting_profile_entries");
        builder.Property(entry => entry.PostingProfileId).IsRequired();
        builder.Property(entry => entry.PostingKey).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.AccountId).IsRequired();
        builder.HasIndex(entry => new { entry.PostingProfileId, entry.PostingKey }).IsUnique();
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountingTransactionConfiguration : IEntityTypeConfiguration<AccountingTransaction>
{
    public void Configure(EntityTypeBuilder<AccountingTransaction> builder)
    {
        builder.ConfigureEntity("accounting_transactions");
        builder.Property(transaction => transaction.CompanyId).IsRequired();
        builder.Property(transaction => transaction.TransactionDate).HasColumnType("date").IsRequired();
        builder.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(transaction => transaction.SourceReference, reference =>
        {
            reference.ConfigureSourceReference(true);
            reference.HasIndex(item => new { item.Module, item.AggregateId, item.EventType }).IsUnique();
        });
        builder.Navigation(transaction => transaction.SourceReference).IsRequired();
    }
}

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ConfigureEntity("journal_entries");
        builder.Property(entry => entry.CompanyId).IsRequired();
        builder.Property(entry => entry.AccountingTransactionId).IsRequired();
        builder.Property(entry => entry.AccountingPeriodId).IsRequired();
        builder.Property(entry => entry.EntryDate).HasColumnType("date").IsRequired();
        builder.Property(entry => entry.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(500).IsRequired();
        builder.Property(entry => entry.Reference).HasMaxLength(150);
        builder.Property(entry => entry.CreatedByUserId);
        builder.Property(entry => entry.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(entry => entry.PostedByUserId);
        builder.Property(entry => entry.PostedAt).HasColumnType("timestamp with time zone");
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.DocumentNumber)
            .HasConversion(number => number == null ? null : number.Value, value => value == null ? null : new DocumentNumber(value))
            .HasColumnName("document_number")
            .HasMaxLength(40);
        builder.HasIndex(entry => new { entry.CompanyId, entry.AccountingPeriodId, entry.EntryDate });
        builder.HasIndex(entry => entry.AccountingTransactionId).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(entry => entry.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingTransaction>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountingTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingPeriod>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entry => entry.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entry => entry.PostedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entry => entry.Lines)
            .WithOne()
            .HasForeignKey("JournalEntryId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(entry => entry.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ConfigureEntity("journal_entry_lines");
        builder.Property(line => line.LineNumber).IsRequired();
        builder.Property(line => line.AccountId).IsRequired();
        builder.Property(line => line.BusinessPartnerId);
        builder.Property(line => line.OpenItemId);
        builder.Property(line => line.FinancialClassId);
        builder.Property(line => line.Memo).HasMaxLength(500);
        builder.HasIndex("JournalEntryId", nameof(JournalEntryLine.LineNumber)).IsUnique();
        builder.HasIndex(line => line.AccountId);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(line => line.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(line => line.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OpenItem>()
            .WithMany()
            .HasForeignKey(line => line.OpenItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialClass>()
            .WithMany()
            .HasForeignKey(line => line.FinancialClassId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(line => line.Debit, money => money.ConfigureMoney("debit"));
        builder.Navigation(line => line.Debit).IsRequired();
        builder.OwnsOne(line => line.Credit, money => money.ConfigureMoney("credit"));
        builder.Navigation(line => line.Credit).IsRequired();
    }
}

internal sealed class OpenItemConfiguration : IEntityTypeConfiguration<OpenItem>
{
    public void Configure(EntityTypeBuilder<OpenItem> builder)
    {
        builder.ConfigureEntity("open_items");
        builder.Property(item => item.CompanyId).IsRequired();
        builder.Property(item => item.BusinessPartnerId).IsRequired();
        builder.Property(item => item.DueDate).HasColumnType("date");
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.DocumentNumber)
            .HasConversion(number => number.Value, value => new DocumentNumber(value))
            .HasColumnName("document_number")
            .HasMaxLength(40)
            .IsRequired();
        builder.Ignore(item => item.OutstandingAmount);
        builder.HasIndex(item => new { item.CompanyId, item.BusinessPartnerId, item.Type });
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(item => item.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(item => item.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(item => item.OriginalAmount, money => money.ConfigureMoney("original"));
        builder.Navigation(item => item.OriginalAmount).IsRequired();
        builder.OwnsOne(item => item.SettledAmount, money => money.ConfigureMoney("settled"));
        builder.Navigation(item => item.SettledAmount).IsRequired();
        builder.OwnsOne(item => item.SourceReference, reference => reference.ConfigureSourceReference(true));
        builder.Navigation(item => item.SourceReference).IsRequired();
    }
}

internal sealed class GeneralLedgerEntryConfiguration : IEntityTypeConfiguration<GeneralLedgerEntry>
{
    public void Configure(EntityTypeBuilder<GeneralLedgerEntry> builder)
    {
        builder.ConfigureEntity("general_ledger_entries");
        builder.Property(entry => entry.CompanyId).IsRequired();
        builder.Property(entry => entry.AccountingPeriodId).IsRequired();
        builder.Property(entry => entry.JournalEntryId).IsRequired();
        builder.Property(entry => entry.JournalEntryLineId).IsRequired();
        builder.Property(entry => entry.AccountId).IsRequired();
        builder.Property(entry => entry.PostedDate).HasColumnType("date").IsRequired();
        builder.HasIndex(entry => entry.JournalEntryLineId).IsUnique();
        builder.HasIndex(entry => new { entry.CompanyId, entry.AccountId, entry.AccountingPeriodId, entry.PostedDate });
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(entry => entry.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingPeriod>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(entry => entry.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntryLine>()
            .WithMany()
            .HasForeignKey(entry => entry.JournalEntryLineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(entry => entry.Debit, money => money.ConfigureMoney("debit"));
        builder.Navigation(entry => entry.Debit).IsRequired();
        builder.OwnsOne(entry => entry.Credit, money => money.ConfigureMoney("credit"));
        builder.Navigation(entry => entry.Credit).IsRequired();
    }
}
