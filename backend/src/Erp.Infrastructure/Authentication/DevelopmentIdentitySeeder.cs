using Erp.Application.Authentication;
using Erp.Domain.Identity;
using Erp.Domain.Organizations;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Authentication;

public sealed class DevelopmentIdentitySeeder(
    ErpDbContext dbContext,
    IPasswordHashingService passwordHashingService,
    IOptions<DevelopmentAdminOptions> options,
    ILogger<DevelopmentIdentitySeeder> logger)
{
    private const string AdministratorRoleName = "Administrator";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogInformation(
                "Development administrator provisioning was skipped because DevelopmentAdmin:Email and DevelopmentAdmin:Password are not configured.");
            return;
        }

        var email = settings.Email.Trim().ToLowerInvariant();
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.Code == settings.OrganizationCode.Trim().ToUpperInvariant(), cancellationToken);

        if (organization is null)
        {
            organization = new Organization(settings.OrganizationName, settings.OrganizationCode);
            dbContext.Organizations.Add(organization);
        }

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organization.Id && candidate.Code == settings.CompanyCode.Trim().ToUpperInvariant(),
                cancellationToken);

        if (company is null)
        {
            company = new Company(
                organization.Id,
                settings.CompanyName,
                settings.CompanyCode,
                settings.BaseCurrencyCode);
            dbContext.Companies.Add(company);
        }

        var existingPermissions = await dbContext.Permissions
            .Where(permission => ErpPermissions.All.Contains(permission.Key))
            .ToDictionaryAsync(permission => permission.Key, cancellationToken);

        foreach (var permissionKey in ErpPermissions.All)
        {
            if (!existingPermissions.ContainsKey(permissionKey))
            {
                var permission = new Permission(permissionKey, ToDisplayName(permissionKey));
                dbContext.Permissions.Add(permission);
                existingPermissions.Add(permissionKey, permission);
            }
        }

        var administratorRole = await dbContext.Roles
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(
                role => role.CompanyId == company.Id && role.Name == AdministratorRoleName,
                cancellationToken);

        if (administratorRole is null)
        {
            administratorRole = new Role(company.Id, AdministratorRoleName, "Development administrator role.");
            dbContext.Roles.Add(administratorRole);
        }

        foreach (var permission in existingPermissions.Values)
        {
            administratorRole.GrantPermission(permission.Id);
        }

        var user = await dbContext.Users
            .Include(candidate => candidate.RoleAssignments)
            .SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            user = new User(settings.DisplayName, email);
            dbContext.Users.Add(user);
        }

        user.AssignRole(company.Id, administratorRole.Id);

        var credential = await dbContext.UserCredentials
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        if (credential is null)
        {
            dbContext.UserCredentials.Add(new UserCredential(user.Id, passwordHashingService.Hash(settings.Password)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development administrator provisioning completed for {Email}.", email);
    }

    private static string ToDisplayName(string permissionKey)
    {
        return string.Join(' ', permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
