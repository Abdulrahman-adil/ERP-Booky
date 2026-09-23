using Erp.Application.Authentication;
using Erp.Domain.Identity;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Authentication;

public sealed class EfUserAuthenticationStore(ErpDbContext dbContext) : IUserAuthenticationStore
{
    public async Task<UserAuthenticationRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == email && candidate.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var credential = await dbContext.UserCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        if (credential is null)
        {
            return null;
        }

        var authenticatedUser = await BuildAuthenticatedUserAsync(user, cancellationToken);

        return authenticatedUser is null
            ? null
            : new UserAuthenticationRecord(authenticatedUser, credential.PasswordHash);
    }

    public async Task<AuthenticatedUser?> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);

        return user is null ? null : await BuildAuthenticatedUserAsync(user, cancellationToken);
    }

    private async Task<AuthenticatedUser?> BuildAuthenticatedUserAsync(User user, CancellationToken cancellationToken)
    {
        var assignments = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UserId == user.Id)
            .Join(
                dbContext.Roles.AsNoTracking().Where(role => role.IsActive),
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { assignment.CompanyId, RoleCompanyId = role.CompanyId, RoleId = role.Id, role.Name })
            .Where(assignment => assignment.CompanyId == assignment.RoleCompanyId
                && dbContext.Companies.Any(company => company.Id == assignment.CompanyId && company.IsActive))
            .ToListAsync(cancellationToken);

        var roleIds = assignments.Select(assignment => assignment.RoleId).Distinct().ToArray();
        var rolePermissions = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
            .Join(
                dbContext.Permissions.AsNoTracking().Where(permission => permission.IsActive),
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (rolePermission, permission) => new { rolePermission.RoleId, permission.Key })
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var companyPermissions = assignments
            .Join(
                rolePermissions,
                assignment => assignment.RoleId,
                permission => permission.RoleId,
                (assignment, permission) => new CompanyPermission(assignment.CompanyId, permission.Key))
            .Distinct()
            .OrderBy(permission => permission.CompanyId)
            .ThenBy(permission => permission.Permission, StringComparer.Ordinal)
            .ToArray();

        var permissions = companyPermissions
            .Select(permission => permission.Permission)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();

        return new AuthenticatedUser(
            user.Id,
            user.DisplayName,
            user.Email,
            assignments.Select(assignment => assignment.Name).Distinct().OrderBy(name => name).ToArray(),
            permissions,
            assignments.Select(assignment => assignment.CompanyId).Distinct().OrderBy(companyId => companyId).ToArray(),
            companyPermissions,
            await dbContext.UserCredentials.AsNoTracking().Where(credential => credential.UserId == user.Id)
                .Select(credential => (DateTimeOffset?)credential.PasswordChangedAt).SingleOrDefaultAsync(cancellationToken));
    }
}
