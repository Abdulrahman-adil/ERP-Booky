using Microsoft.AspNetCore.Mvc.Testing;

namespace Erp.Api.IntegrationTests.Infrastructure;

public sealed class ErpApiFactory : WebApplicationFactory<Program>
{
    public ErpApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__PostgreSQL",
            "Host=localhost;Port=5432;Database=erp_tests;Username=erp;Password=erp");
        Environment.SetEnvironmentVariable("Authentication__Jwt__Issuer", "erp-api-tests");
        Environment.SetEnvironmentVariable("Authentication__Jwt__Audience", "erp-web-tests");
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            "test-signing-key-that-is-at-least-thirty-two-characters-long");
    }
}
