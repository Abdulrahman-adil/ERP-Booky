using Erp.Application.Authentication;
using Erp.Api.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Erp.Api.Authentication;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null
            || !CompanyContextResolver.TryGetCompanyId(context.User, httpContext.Request.Headers, out var companyId))
        {
            return Task.CompletedTask;
        }

        var hasCompanyScopedPermissions = context.User.HasClaim(claim => claim.Type == AuthorizationClaimTypes.CompanyPermission);
        var hasRequiredPermission = context.User.HasClaim(
            AuthorizationClaimTypes.CompanyPermission,
            AuthorizationClaimTypes.FormatCompanyPermission(companyId, requirement.Permission));

        if (hasRequiredPermission
            || (!hasCompanyScopedPermissions
                && context.User.FindAll(AuthorizationClaimTypes.CompanyId).Select(claim => claim.Value).Distinct().Count() == 1
                && context.User.HasClaim(AuthorizationClaimTypes.Permission, requirement.Permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
