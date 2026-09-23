using Erp.Application.Administration;
using Erp.Application.Authentication;
using Erp.Application.Documents;
using Erp.Domain.Accounting;
using Erp.Domain.Identity;
using Erp.Domain.Payments;
using Erp.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Persistence;

public sealed class EfAdministrationService(
    ErpDbContext dbContext,
    IPasswordHashingService passwordHashingService) : IAdministrationService
{
    public async Task<IReadOnlyCollection<AdministrationPermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Permissions.AsNoTracking()
            .OrderBy(permission => permission.Key)
            .Select(permission => new AdministrationPermissionDto(permission.Id, permission.Key, permission.Name, permission.Description, permission.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AdministrationRoleDto>> GetRolesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.Roles.AsNoTracking().Include(role => role.Permissions)
            .Where(role => role.CompanyId == companyId)
            .OrderBy(role => role.Name)
            .ToArrayAsync(cancellationToken);
        var assignments = await dbContext.UserRoleAssignments.AsNoTracking()
            .Where(assignment => assignment.CompanyId == companyId)
            .GroupBy(assignment => assignment.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RoleId, item => item.Count, cancellationToken);

        return roles.Select(role => ToRoleDto(role, assignments.GetValueOrDefault(role.Id))).ToArray();
    }

    public async Task<AdministrationRoleDto> CreateRoleAsync(Guid companyId, AdministrationRoleInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        await EnsureRoleNameAvailableAsync(companyId, input.Name, null, cancellationToken);
        await EnsurePermissionsExistAsync(input.PermissionIds, cancellationToken);

        var role = new Role(companyId, input.Name, input.Description);
        role.ReplacePermissions(input.PermissionIds);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRoleDto(role, 0);
    }

    public async Task<AdministrationRoleDto?> UpdateRoleAsync(Guid companyId, Guid id, AdministrationRoleInput input, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (role is null) return null;

        await EnsureRoleNameAvailableAsync(companyId, input.Name, id, cancellationToken);
        await EnsurePermissionsExistAsync(input.PermissionIds, cancellationToken);
        role.Update(input.Name, input.Description);
        role.ReplacePermissions(input.PermissionIds);
        await dbContext.SaveChangesAsync(cancellationToken);
        var assignmentCount = await dbContext.UserRoleAssignments.CountAsync(assignment => assignment.CompanyId == companyId && assignment.RoleId == id, cancellationToken);
        return ToRoleDto(role, assignmentCount);
    }

    public async Task<bool> SetRoleActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (role is null) return false;
        if (!isActive && string.Equals(role.Name, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            throw new AdministrationConflictException("The Administrator role cannot be deactivated.");
        }

        role.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<AdministrationUserDto>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var assignments = await dbContext.UserRoleAssignments.AsNoTracking()
            .Where(assignment => assignment.CompanyId == companyId)
            .ToArrayAsync(cancellationToken);
        var userIds = assignments.Select(assignment => assignment.UserId).Distinct().ToArray();
        if (userIds.Length == 0) return Array.Empty<AdministrationUserDto>();

        var users = await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToArrayAsync(cancellationToken);
        var roles = await dbContext.Roles.AsNoTracking().Where(role => role.CompanyId == companyId).ToDictionaryAsync(role => role.Id, cancellationToken);
        var passwordDates = await dbContext.UserCredentials.AsNoTracking().Where(credential => userIds.Contains(credential.UserId))
            .ToDictionaryAsync(credential => credential.UserId, credential => credential.PasswordChangedAt, cancellationToken);

        return users.OrderBy(user => user.DisplayName).Select(user =>
        {
            var userAssignments = assignments.Where(assignment => assignment.UserId == user.Id).ToArray();
            var roleIds = userAssignments.Select(assignment => assignment.RoleId).ToArray();
            return new AdministrationUserDto(
                user.Id,
                user.DisplayName,
                user.Email,
                user.IsActive,
                roleIds,
                roleIds.Where(roles.ContainsKey).Select(roleId => roles[roleId].Name).OrderBy(name => name).ToArray(),
                passwordDates.GetValueOrDefault(user.Id));
        }).ToArray();
    }

    public async Task<AdministrationUserDto> CreateUserAsync(Guid companyId, AdministrationUserInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        ValidatePassword(input.Password, required: true);
        await EnsureUserEmailAvailableAsync(input.Email, null, cancellationToken);
        var roles = await GetAssignableRolesAsync(companyId, input.RoleIds, cancellationToken);

        var user = new User(input.DisplayName, input.Email);
        user.ReplaceRoles(companyId, roles.Select(role => role.Id));
        dbContext.Users.Add(user);
        dbContext.UserCredentials.Add(new UserCredential(user.Id, passwordHashingService.Hash(input.Password!)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToUserDto(user, roles, DateTimeOffset.UtcNow);
    }

    public async Task<AdministrationUserDto?> UpdateUserAsync(Guid companyId, Guid id, AdministrationUserInput input, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.Include(item => item.RoleAssignments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null || !user.RoleAssignments.Any(assignment => assignment.CompanyId == companyId)) return null;

        if (user.RoleAssignments.Any(assignment => assignment.CompanyId != companyId)
            && (user.DisplayName != input.DisplayName?.Trim()
                || user.Email != input.Email?.Trim().ToLowerInvariant()
                || !string.IsNullOrWhiteSpace(input.Password)))
        {
            throw new AdministrationConflictException("Shared users can only have their roles in this company updated. Global account changes require a separate account-ownership process.");
        }

        ValidatePassword(input.Password, required: false);
        await EnsureUserEmailAvailableAsync(input.Email, id, cancellationToken);
        var roles = await GetAssignableRolesAsync(companyId, input.RoleIds, cancellationToken);
        user.Update(input.DisplayName, input.Email);
        user.ReplaceRoles(companyId, roles.Select(role => role.Id));

        var credential = await dbContext.UserCredentials.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(input.Password))
        {
            if (credential is null)
            {
                credential = new UserCredential(user.Id, passwordHashingService.Hash(input.Password));
                dbContext.UserCredentials.Add(credential);
            }
            else
            {
                credential.SetPasswordHash(passwordHashingService.Hash(input.Password));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToUserDto(user, roles, credential?.PasswordChangedAt);
    }

    public async Task<bool> SetUserActiveAsync(Guid companyId, Guid actorUserId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.Include(item => item.RoleAssignments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null || !user.RoleAssignments.Any(assignment => assignment.CompanyId == companyId)) return false;
        if (user.RoleAssignments.Any(assignment => assignment.CompanyId != companyId))
        {
            throw new AdministrationConflictException("A company administrator cannot change the global status of a shared user.");
        }
        if (!isActive && id == actorUserId)
        {
            throw new AdministrationValidationException("You cannot deactivate the account currently being used.");
        }

        user.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken))
        {
            throw new AdministrationValidationException("The selected company is unavailable.");
        }
    }

    private async Task EnsurePermissionsExistAsync(IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken)
    {
        var requestedIds = (permissionIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (requestedIds.Any(id => id == Guid.Empty)) throw new AdministrationValidationException("A permission is invalid.");
        var count = requestedIds.Length == 0 ? 0 : await dbContext.Permissions.CountAsync(permission => permission.IsActive && requestedIds.Contains(permission.Id), cancellationToken);
        if (count != requestedIds.Length) throw new AdministrationValidationException("One or more selected permissions are unavailable.");
    }

    private async Task<IReadOnlyCollection<Role>> GetAssignableRolesAsync(Guid companyId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var requestedIds = (roleIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (requestedIds.Length == 0) throw new AdministrationValidationException("Assign at least one active role.");
        if (requestedIds.Any(id => id == Guid.Empty)) throw new AdministrationValidationException("A role is invalid.");
        var roles = await dbContext.Roles.Where(role => role.CompanyId == companyId && role.IsActive && requestedIds.Contains(role.Id)).ToArrayAsync(cancellationToken);
        if (roles.Length != requestedIds.Length) throw new AdministrationValidationException("One or more selected roles are unavailable.");
        return roles;
    }

    private async Task EnsureRoleNameAvailableAsync(Guid companyId, string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new AdministrationValidationException("A role name is required.");
        var duplicate = await dbContext.Roles.AnyAsync(role => role.CompanyId == companyId && role.Name == normalized && (!excludedId.HasValue || role.Id != excludedId), cancellationToken);
        if (duplicate) throw new AdministrationConflictException("A role with this name already exists.");
    }

    private async Task EnsureUserEmailAvailableAsync(string email, Guid? excludedId, CancellationToken cancellationToken)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@')) throw new AdministrationValidationException("A valid email address is required.");
        var duplicate = await dbContext.Users.AnyAsync(user => user.Email == normalized && (!excludedId.HasValue || user.Id != excludedId), cancellationToken);
        if (duplicate) throw new AdministrationConflictException("A user with this email already exists.");
    }

    private static void ValidatePassword(string? password, bool required)
    {
        if (required && string.IsNullOrWhiteSpace(password)) throw new AdministrationValidationException("A temporary password is required.");
        if (!string.IsNullOrWhiteSpace(password) && password.Length < 10) throw new AdministrationValidationException("Passwords must contain at least 10 characters.");
    }

    private static AdministrationRoleDto ToRoleDto(Role role, int assignedUserCount) =>
        new(role.Id, role.Name, role.Description, role.IsActive, role.Permissions.Select(permission => permission.PermissionId).ToArray(), assignedUserCount);

    private static AdministrationUserDto ToUserDto(User user, IReadOnlyCollection<Role> roles, DateTimeOffset? passwordChangedAt) =>
        new(user.Id, user.DisplayName, user.Email, user.IsActive, roles.Select(role => role.Id).ToArray(), roles.Select(role => role.Name).OrderBy(name => name).ToArray(), passwordChangedAt);
}

public sealed class EfDevelopmentDataResetService(
    ErpDbContext dbContext,
    IAttachmentStorage attachmentStorage,
    ILogger<EfDevelopmentDataResetService> logger) : IDevelopmentDataResetService
{
    public const string ConfirmationPhrase = "RESET DEVELOPMENT DATA";

    public async Task<DevelopmentResetPreview> GetPreviewAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var salesInvoiceIds = dbContext.SalesInvoices.Where(invoice => invoice.CompanyId == companyId).Select(invoice => invoice.Id);
        var purchaseInvoiceIds = dbContext.PurchaseInvoices.Where(invoice => invoice.CompanyId == companyId).Select(invoice => invoice.Id);
        var paymentIds = dbContext.Payments.Where(payment => payment.CompanyId == companyId).Select(payment => payment.Id);
        var journalIds = dbContext.JournalEntries.Where(entry => entry.CompanyId == companyId).Select(entry => entry.Id);
        var documentIds = salesInvoiceIds.Concat(purchaseInvoiceIds).Concat(paymentIds).Concat(journalIds);

        return new DevelopmentResetPreview(
            await dbContext.SalesInvoices.CountAsync(invoice => invoice.CompanyId == companyId, cancellationToken),
            await dbContext.PurchaseInvoices.CountAsync(invoice => invoice.CompanyId == companyId, cancellationToken),
            await dbContext.Payments.CountAsync(payment => payment.CompanyId == companyId && payment.Direction == PaymentDirection.Incoming, cancellationToken),
            await dbContext.Payments.CountAsync(payment => payment.CompanyId == companyId && payment.Direction == PaymentDirection.Outgoing, cancellationToken),
            await dbContext.InventoryTransactions.CountAsync(transaction => transaction.CompanyId == companyId, cancellationToken),
            await dbContext.OpenItems.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            await dbContext.JournalEntries.CountAsync(entry => entry.CompanyId == companyId, cancellationToken),
            await dbContext.GeneralLedgerEntries.CountAsync(entry => entry.CompanyId == companyId, cancellationToken),
            await dbContext.AccountingTransactions.CountAsync(transaction => transaction.CompanyId == companyId && transaction.Status == AccountingTransactionStatus.Pending, cancellationToken),
            await dbContext.DocumentAttachments.CountAsync(attachment => documentIds.Contains(attachment.SourceReference.AggregateId), cancellationToken),
            await dbContext.BusinessPartners.CountAsync(partner => partner.CompanyId == companyId && partner.CustomerProfile != null, cancellationToken),
            await dbContext.BusinessPartners.CountAsync(partner => partner.CompanyId == companyId && partner.SupplierProfile != null, cancellationToken),
            await dbContext.Products.CountAsync(product => product.CompanyId == companyId, cancellationToken),
            await dbContext.Warehouses.CountAsync(warehouse => warehouse.CompanyId == companyId, cancellationToken));
    }

    public async Task<DevelopmentResetResult> ResetAsync(Guid companyId, string confirmation, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirmation?.Trim(), ConfirmationPhrase, StringComparison.Ordinal))
        {
            throw new AdministrationValidationException($"Type '{ConfirmationPhrase}' to confirm the development reset.");
        }

        var preview = await GetPreviewAsync(companyId, cancellationToken);
        var salesInvoiceIds = await dbContext.SalesInvoices.Where(invoice => invoice.CompanyId == companyId).Select(invoice => invoice.Id).ToArrayAsync(cancellationToken);
        var purchaseInvoiceIds = await dbContext.PurchaseInvoices.Where(invoice => invoice.CompanyId == companyId).Select(invoice => invoice.Id).ToArrayAsync(cancellationToken);
        var paymentIds = await dbContext.Payments.Where(payment => payment.CompanyId == companyId).Select(payment => payment.Id).ToArrayAsync(cancellationToken);
        var journalIds = await dbContext.JournalEntries.Where(entry => entry.CompanyId == companyId).Select(entry => entry.Id).ToArrayAsync(cancellationToken);
        var partnerIds = await dbContext.BusinessPartners.Where(partner => partner.CompanyId == companyId).Select(partner => partner.Id).ToArrayAsync(cancellationToken);
        var documentIds = salesInvoiceIds.Concat(purchaseInvoiceIds).Concat(paymentIds).Concat(journalIds).Distinct().ToArray();
        var attachmentKeys = documentIds.Length == 0
            ? Array.Empty<string>()
            : await dbContext.DocumentAttachments.Where(attachment => documentIds.Contains(attachment.SourceReference.AggregateId)).Select(attachment => attachment.StorageKey).ToArrayAsync(cancellationToken);

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (documentIds.Length > 0) await dbContext.DocumentAttachments.Where(attachment => documentIds.Contains(attachment.SourceReference.AggregateId)).ExecuteDeleteAsync(cancellationToken);
            if (paymentIds.Length > 0) await dbContext.PaymentAllocations.Where(allocation => paymentIds.Contains(allocation.PaymentId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.GeneralLedgerEntries.Where(entry => entry.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            if (journalIds.Length > 0) await dbContext.JournalEntryLines.Where(line => journalIds.Contains(EF.Property<Guid>(line, "JournalEntryId"))).ExecuteDeleteAsync(cancellationToken);
            await dbContext.JournalEntries.Where(entry => entry.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.AccountingTransactions.Where(transaction => transaction.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.OpenItems.Where(item => item.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Payments.Where(payment => payment.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            if (salesInvoiceIds.Length > 0)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM erp.sales_invoice_lines WHERE \"SalesInvoiceId\" = ANY({salesInvoiceIds})", cancellationToken);
            }
            await dbContext.SalesInvoices.Where(invoice => invoice.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            if (purchaseInvoiceIds.Length > 0)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM erp.purchase_invoice_lines WHERE \"PurchaseInvoiceId\" = ANY({purchaseInvoiceIds})", cancellationToken);
            }
            await dbContext.PurchaseInvoices.Where(invoice => invoice.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.InventoryTransactions.Where(transaction => transaction.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.InventoryBalances.Where(balance => balance.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            if (partnerIds.Length > 0) await dbContext.CustomerProfiles.Where(profile => partnerIds.Contains(profile.BusinessPartnerId)).ExecuteDeleteAsync(cancellationToken);
            if (partnerIds.Length > 0) await dbContext.SupplierProfiles.Where(profile => partnerIds.Contains(profile.BusinessPartnerId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.BusinessPartners.Where(partner => partner.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Products.Where(product => product.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Warehouses.Where(warehouse => warehouse.CompanyId == companyId).ExecuteDeleteAsync(cancellationToken);
            await ResetDocumentSequencesAsync(cancellationToken);

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        foreach (var attachmentKey in attachmentKeys)
        {
            try
            {
                await attachmentStorage.DeleteAsync(attachmentKey, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Development reset removed attachment metadata but could not remove storage object {StorageKey}.", attachmentKey);
            }
        }

        logger.LogWarning("Development data reset completed for company {CompanyId}.", companyId);
        return new DevelopmentResetResult(preview, DateTimeOffset.UtcNow);
    }

    private async Task ResetDocumentSequencesAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true) return;

        foreach (var sequence in new[]
        {
            "customer_code_sequence", "supplier_code_sequence", "product_sku_sequence", "warehouse_code_sequence",
            "inventory_movement_number_sequence", "sales_invoice_number_sequence", "customer_payment_number_sequence",
            "purchase_invoice_number_sequence", "supplier_payment_number_sequence", "journal_entry_number_sequence"
        })
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT setval('erp." + sequence + "', 1, false);", cancellationToken);
        }
    }
}
