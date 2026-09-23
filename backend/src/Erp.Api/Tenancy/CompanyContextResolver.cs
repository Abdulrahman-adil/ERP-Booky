using System.Security.Claims;
using Erp.Application.Authentication;
using Microsoft.Extensions.Primitives;

namespace Erp.Api.Tenancy;

public static class CompanyContextResolver
{
    public const string HeaderName = "X-Company-Id";

    public static bool TryGetCompanyId(ClaimsPrincipal user, IHeaderDictionary headers, out Guid companyId)
    {
        var companyIds = user.FindAll(AuthorizationClaimTypes.CompanyId)
            .Select(claim => Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (companyIds.Length == 0)
        {
            companyId = Guid.Empty;
            return false;
        }

        if (!headers.TryGetValue(HeaderName, out StringValues requestedCompanyValues))
        {
            companyId = companyIds[0];
            return true;
        }

        if (requestedCompanyValues.Count != 1
            || !Guid.TryParse(requestedCompanyValues[0], out var requestedCompanyId)
            || !companyIds.Contains(requestedCompanyId))
        {
            companyId = Guid.Empty;
            return false;
        }

        companyId = requestedCompanyId;
        return true;
    }
}
