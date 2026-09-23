using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Erp.Application.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Erp.Api.Authentication;

public static class CurrentAccessTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity
            || !Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            context.Fail("Invalid session.");
            return;
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<IUserAuthenticationStore>();
        var current = await store.FindActiveUserAsync(userId, context.HttpContext.RequestAborted);
        if (current is null)
        {
            context.Fail("Invalid session.");
            return;
        }
        if (current.PasswordChangedAt is { } changedAt)
        {
            var stamp = identity.FindFirst("credential_version")?.Value;
            var invalid = stamp is not null
                ? stamp != changedAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : !long.TryParse(identity.FindFirst(JwtRegisteredClaimNames.Iat)?.Value, out var issuedAt)
                    || issuedAt < changedAt.ToUnixTimeSeconds();
            if (invalid)
            {
                context.Fail("Invalid session.");
                return;
            }
        }

        var companies = current.CompanyIds.Select(company => company.ToString()).ToHashSet(StringComparer.Ordinal);
        var signedCompanies = identity.FindAll(AuthorizationClaimTypes.CompanyId).Select(claim => claim.Value).ToHashSet();
        companies.IntersectWith(signedCompanies);
        var scopedPermissions = current.CompanyPermissions
            .Where(permission => companies.Contains(permission.CompanyId.ToString()))
            .Select(permission => AuthorizationClaimTypes.FormatCompanyPermission(permission.CompanyId, permission.Permission))
            .ToHashSet(StringComparer.Ordinal);
        var permissions = current.CompanyPermissions
            .Where(permission => companies.Contains(permission.CompanyId.ToString()))
            .Select(permission => permission.Permission).ToHashSet(StringComparer.Ordinal);
        foreach (var claim in identity.Claims.ToArray())
        {
            var keep = claim.Type switch
            {
                AuthorizationClaimTypes.CompanyId => companies.Contains(claim.Value),
                AuthorizationClaimTypes.CompanyPermission => scopedPermissions.Contains(claim.Value),
                AuthorizationClaimTypes.Permission => permissions.Contains(claim.Value),
                ClaimTypes.Role => current.Roles.Contains(claim.Value),
                _ => true
            };
            if (!keep) identity.RemoveClaim(claim);
        }
    }
}
