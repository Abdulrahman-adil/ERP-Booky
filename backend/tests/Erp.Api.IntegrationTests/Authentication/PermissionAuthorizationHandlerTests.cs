using System.Security.Claims;
using Erp.Api.Authentication;
using Erp.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Erp.Api.IntegrationTests.Authentication;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandlerRequiresThePermissionForTheSelectedCompany()
    {
        var requirement = new PermissionRequirement(ErpPermissions.UsersManage);
        var companyId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Company-Id"] = companyId.ToString();
        var handler = new PermissionAuthorizationHandler(new HttpContextAccessor { HttpContext = httpContext });
        var authorizedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(AuthorizationClaimTypes.CompanyId, companyId.ToString()),
                new Claim(AuthorizationClaimTypes.CompanyPermission, AuthorizationClaimTypes.FormatCompanyPermission(companyId, ErpPermissions.UsersManage))
            ],
            "test"));
        var authorizedContext = new AuthorizationHandlerContext([requirement], authorizedPrincipal, null);

        await handler.HandleAsync(authorizedContext);

        Assert.True(authorizedContext.HasSucceeded);

        var unauthorizedContext = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(AuthorizationClaimTypes.CompanyId, companyId.ToString()),
                    new Claim(AuthorizationClaimTypes.CompanyPermission, AuthorizationClaimTypes.FormatCompanyPermission(companyId, ErpPermissions.CustomersView))
                ],
                "test")),
            null);

        await handler.HandleAsync(unauthorizedContext);

        Assert.False(unauthorizedContext.HasSucceeded);
    }
}
