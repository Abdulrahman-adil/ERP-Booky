using System.Security.Claims;
using Erp.Api.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

public abstract class CompanyScopedControllerBase : ControllerBase
{
    protected bool TryGetCompanyId(out Guid companyId) =>
        CompanyContextResolver.TryGetCompanyId(User, Request.Headers, out companyId);

    protected bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    protected bool TryCompany(out Guid companyId) => TryGetCompanyId(out companyId);

    protected bool TryUser(out Guid userId) => TryGetUserId(out userId);
}
