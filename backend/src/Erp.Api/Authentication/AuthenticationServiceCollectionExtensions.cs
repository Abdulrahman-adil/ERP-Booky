using System.Security.Claims;
using System.Text;
using Erp.Application.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddErpAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        Validate(options);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleAuthenticationOptions>(configuration.GetSection(GoogleAuthenticationOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                jwtOptions.Events = new JwtBearerEvents { OnTokenValidated = CurrentAccessTokenValidator.ValidateAsync };
            });
        services.AddAuthorization(authorizationOptions =>
        {
            foreach (var permission in ErpPermissions.All)
            {
                authorizationOptions.AddPolicy(
                    PermissionPolicies.For(permission),
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
            }
        });

        return services;
    }

    private static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("Authentication:Jwt:Issuer and Authentication:Jwt:Audience must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must be configured with at least 32 characters.");
        }

        if (options.ExpirationMinutes is < 5 or > 480)
        {
            throw new InvalidOperationException("Authentication:Jwt:ExpirationMinutes must be between 5 and 480.");
        }
    }
}
