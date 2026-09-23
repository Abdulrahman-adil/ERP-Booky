using System.Data;
using Erp.Application.Accounting;
using Erp.Application.Authentication;
using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Identity;
using Erp.Domain.Organizations;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Erp.Infrastructure.Authentication;

public sealed class EfExternalWorkspaceProvisioner(
    ErpDbContext dbContext,
    IUserAuthenticationStore userAuthenticationStore,
    IOptions<ExternalWorkspaceOptions> options) : IExternalWorkspaceProvisioner
{
    private const string OwnerRoleName = "Owner";
    private const string ChartName = "Main Chart of Accounts";
    private const string PostingProfileName = "Default Operational Posting";
    private const string OperatingBankName = "Operating Bank";
    private const int MaximumProvisionAttempts = 3;

    public async Task<ExternalWorkspaceProvisioningResult> FindOrProvisionGoogleWorkspaceAsync(
        VerifiedGoogleIdentity identity,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaximumProvisionAttempts; attempt++)
        {
            try
            {
                return await FindOrProvisionOnceAsync(identity, cancellationToken);
            }
            catch (DbUpdateException exception) when (attempt < MaximumProvisionAttempts - 1 && IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (PostgresException exception) when (attempt < MaximumProvisionAttempts - 1 && exception.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("External workspace provisioning did not complete.");
    }

    private async Task<ExternalWorkspaceProvisioningResult> FindOrProvisionOnceAsync(
        VerifiedGoogleIdentity identity,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await FindOrProvisionCoreAsync(identity, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await FindOrProvisionCoreAsync(identity, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<ExternalWorkspaceProvisioningResult> FindOrProvisionCoreAsync(
        VerifiedGoogleIdentity identity,
        CancellationToken cancellationToken)
    {
        var existingIdentity = await dbContext.ExternalIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Provider == ExternalIdentityProvider.Google && candidate.ProviderSubject == identity.Subject,
                cancellationToken);

        if (existingIdentity is not null)
        {
            var existingUser = await userAuthenticationStore.FindActiveUserAsync(existingIdentity.UserId, cancellationToken);
            return existingUser is null
                ? new ExternalWorkspaceProvisioningResult(ExternalWorkspaceProvisioningStatus.AccountInactive, null, false)
                : new ExternalWorkspaceProvisioningResult(ExternalWorkspaceProvisioningStatus.Authenticated, existingUser, false);
        }

        var normalizedEmail = identity.Email.Trim().ToLowerInvariant();
        var existingUserWithEmail = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);

        if (existingUserWithEmail)
        {
            return new ExternalWorkspaceProvisioningResult(ExternalWorkspaceProvisioningStatus.ExistingAccountRequiresLink, null, false);
        }

        var displayName = NormalizeDisplayName(identity.DisplayName);
        var currencyCode = GetBaseCurrencyCode();
        var workspaceCode = $"EXT-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var workspaceName = BuildWorkspaceName(displayName);
        var organization = new Organization(workspaceName, workspaceCode);
        var company = new Company(organization.Id, workspaceName, workspaceCode, currencyCode);
        var permissions = await EnsureWorkspaceOwnerPermissionsAsync(cancellationToken);
        var ownerRole = new Role(company.Id, OwnerRoleName, "Workspace owner role.");

        foreach (var permission in permissions)
        {
            ownerRole.GrantPermission(permission.Id);
        }

        var user = new User(displayName, normalizedEmail);
        user.AssignRole(company.Id, ownerRole.Id);

        var externalIdentity = new ExternalIdentity(user.Id, ExternalIdentityProvider.Google, identity.Subject);
        var setup = CreateAccountingSetup(company.Id, currencyCode);

        dbContext.AddRange(
            organization,
            company,
            ownerRole,
            user,
            externalIdentity,
            setup.Chart,
            setup.AccountingPeriod,
            setup.CashBankAccount,
            setup.PostingProfile);

        await dbContext.SaveChangesAsync(cancellationToken);

        var grantedPermissions = ErpPermissions.WorkspaceOwner
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
        var authenticatedUser = new AuthenticatedUser(
            user.Id,
            user.DisplayName,
            user.Email,
            [ownerRole.Name],
            grantedPermissions,
            [company.Id],
            grantedPermissions.Select(permission => new CompanyPermission(company.Id, permission)).ToArray());

        return new ExternalWorkspaceProvisioningResult(ExternalWorkspaceProvisioningStatus.Authenticated, authenticatedUser, true);
    }

    private async Task<IReadOnlyCollection<Permission>> EnsureWorkspaceOwnerPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingPermissions = await dbContext.Permissions
            .Where(permission => ErpPermissions.WorkspaceOwner.Contains(permission.Key))
            .ToDictionaryAsync(permission => permission.Key, StringComparer.Ordinal, cancellationToken);

        var inactivePermission = existingPermissions.Values.FirstOrDefault(permission => !permission.IsActive);

        if (inactivePermission is not null)
        {
            throw new InvalidOperationException(
                $"The required workspace permission '{inactivePermission.Key}' is inactive and cannot be granted to a new workspace owner.");
        }

        foreach (var permissionKey in ErpPermissions.WorkspaceOwner)
        {
            if (existingPermissions.ContainsKey(permissionKey))
            {
                continue;
            }

            var permission = new Permission(permissionKey, ToDisplayName(permissionKey));
            dbContext.Permissions.Add(permission);
            existingPermissions.Add(permissionKey, permission);
        }

        return existingPermissions.Values.ToArray();
    }

    private static WorkspaceAccountingSetup CreateAccountingSetup(Guid companyId, string currencyCode)
    {
        var chart = new ChartOfAccounts(companyId, ChartName);
        var operatingBank = chart.AddAccount("1000", OperatingBankName, AccountType.Asset, AccountRole.Bank, true);
        var receivables = chart.AddAccount("1100", "Accounts Receivable", AccountType.Asset, AccountRole.AccountsReceivable, true);
        var inventory = chart.AddAccount("1200", "Inventory", AccountType.Asset, AccountRole.Inventory, true);
        var payables = chart.AddAccount("2000", "Accounts Payable", AccountType.Liability, AccountRole.AccountsPayable, true);
        chart.AddAccount("3000", "Current Year Earnings", AccountType.Equity, AccountRole.CurrentYearEarnings, true);
        var salesRevenue = chart.AddAccount("4000", "Sales Revenue", AccountType.Revenue, AccountRole.SalesRevenue, true);

        var year = DateTime.UtcNow.Year;
        var period = new AccountingPeriod(
            companyId,
            year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new DateRange(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31)));
        var cashBankAccount = new CashBankAccount(companyId, OperatingBankName, CashBankAccountType.Bank, operatingBank.Id);
        var postingProfile = new PostingProfile(companyId, PostingProfileName);
        postingProfile.AddMapping(PostingKeys.SalesReceivable, receivables.Id);
        postingProfile.AddMapping(PostingKeys.SalesRevenue, salesRevenue.Id);
        postingProfile.AddMapping(PostingKeys.CustomerPaymentReceivable, receivables.Id);
        postingProfile.AddMapping(PostingKeys.PurchaseInventory, inventory.Id);
        postingProfile.AddMapping(PostingKeys.PurchasePayable, payables.Id);
        postingProfile.AddMapping(PostingKeys.SupplierPaymentPayable, payables.Id);

        return new WorkspaceAccountingSetup(chart, period, cashBankAccount, postingProfile);
    }

    private string GetBaseCurrencyCode()
    {
        var currencyCode = options.Value.BaseCurrencyCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3 || currencyCode.Any(character => !char.IsLetter(character)))
        {
            throw new InvalidOperationException("Authentication:ExternalWorkspace:BaseCurrencyCode must contain a three-letter currency code.");
        }

        return currencyCode;
    }

    private static string BuildWorkspaceName(string displayName)
    {
        var normalizedName = string.Join(' ', displayName
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var ownerName = string.IsNullOrWhiteSpace(normalizedName) ? "New" : normalizedName;
        const string suffix = " Workspace";
        var maximumOwnerNameLength = 200 - suffix.Length;

        return ownerName.Length <= maximumOwnerNameLength
            ? ownerName + suffix
            : ownerName[..maximumOwnerNameLength] + suffix;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        var normalizedName = string.Join(' ', displayName
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return "Google Workspace Owner";
        }

        return normalizedName.Length <= 200 ? normalizedName : normalizedName[..200];
    }

    private static bool IsRetryable(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure;

    private static string ToDisplayName(string permissionKey) =>
        string.Join(' ', permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private sealed record WorkspaceAccountingSetup(
        ChartOfAccounts Chart,
        AccountingPeriod AccountingPeriod,
        CashBankAccount CashBankAccount,
        PostingProfile PostingProfile);
}
