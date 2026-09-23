using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace Erp.Api.Security;

public static class ProductionSecurity
{
    public const string LoginPolicy = "authentication";

    public static IServiceCollection AddProductionSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsProduction()) ValidateProductionConfiguration(configuration);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
        services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
        services.AddHttpsRedirection(options => options.HttpsPort = 443);
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("Security:LoginPermitLimit", 10),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await Results.Problem(statusCode: 429, title: "Too many sign-in attempts. Try again in one minute.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 12 * 1024 * 1024);
        return services;
    }

    public static void ValidateProductionConfiguration(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || uri.Authority.Contains('*') || uri.UserInfo.Length > 0
            || uri.GetLeftPart(UriPartial.Authority) != origin || uri.IsLoopback))
            throw new InvalidOperationException("Production requires explicit HTTPS frontend origins in Cors:AllowedOrigins.");
        var signingKey = configuration["Authentication:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 64 || signingKey.Contains("REPLACE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production requires a randomly generated Authentication:Jwt:SigningKey of at least 64 characters.");
        if (string.IsNullOrWhiteSpace(configuration["AllowedHosts"]) || configuration["AllowedHosts"]!.Contains('*'))
            throw new InvalidOperationException("Production requires explicit AllowedHosts.");
        if (configuration.GetValue<bool>("Authentication:Google:Enabled")
            && !(configuration["Authentication:Google:ClientId"]?.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) ?? false))
            throw new InvalidOperationException("Enabled Google authentication requires a valid ClientId configuration.");
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            context.Response.Headers.CacheControl = "no-store";
        }
        else
        {
            // CSP for Angular SPA - allows inline scripts/styles for Angular, Google Sign-In frames
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://accounts.google.com; " +
                "style-src 'self' 'unsafe-inline'; " +
                "font-src 'self' data:; " +
                "img-src 'self' data: https:; " +
                "connect-src 'self' https://accounts.google.com; " +
                "frame-src https://accounts.google.com; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';";
        }
        await next(context);
    });
}
