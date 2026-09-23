using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Erp.Application.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Api.Authentication;

public sealed class JwtAccessTokenService(IOptions<JwtOptions> options) : IAccessTokenService
{
    public AccessToken Create(AuthenticatedUser user)
    {
        var settings = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(settings.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (user.PasswordChangedAt is { } changedAt)
        {
            claims.Add(new Claim("credential_version", changedAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        claims.AddRange(user.Permissions.Select(permission => new Claim(AuthorizationClaimTypes.Permission, permission)));
        claims.AddRange(user.CompanyIds.Select(companyId => new Claim(AuthorizationClaimTypes.CompanyId, companyId.ToString())));
        claims.AddRange(user.CompanyPermissions
            .Where(permission => user.CompanyIds.Contains(permission.CompanyId))
            .Distinct()
            .Select(permission => new Claim(
                AuthorizationClaimTypes.CompanyPermission,
                AuthorizationClaimTypes.FormatCompanyPermission(permission.CompanyId, permission.Permission))));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(descriptor);

        return new AccessToken(tokenHandler.WriteToken(token), expiresAt);
    }
}
