using System.Collections.Concurrent;
using Erp.Application.Authentication;
using Erp.Domain.Identity;
using Erp.Domain.Organizations;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Api.IntegrationTests.Authentication;

public sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
{
    public const string AdministratorEmail = "admin@example.test";
    public const string AdministratorPassword = "A-strong-test-password-2026";

    private readonly string _databaseName = $"erp-auth-tests-{Guid.NewGuid()}";

    public TestGoogleIdTokenValidator GoogleIdTokenValidator { get; } = new();

    public AuthenticationApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgreSQL", "Host=unused;Database=erp_auth_tests;Username=unused");
        Environment.SetEnvironmentVariable("Authentication__Jwt__Issuer", "erp-api-tests");
        Environment.SetEnvironmentVariable("Authentication__Jwt__Audience", "erp-web-tests");
        Environment.SetEnvironmentVariable("Authentication__Jwt__SigningKey", "test-signing-key-that-is-at-least-thirty-two-characters-long");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=unused;Database=erp_auth_tests;Username=unused",
                ["Authentication:Jwt:Issuer"] = "erp-api-tests",
                ["Authentication:Jwt:Audience"] = "erp-web-tests",
                ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-at-least-thirty-two-characters-long",
                ["Authentication:Jwt:ExpirationMinutes"] = "60",
                ["Security:LoginPermitLimit"] = "10000",
                ["Authentication:Google:Enabled"] = "true",
                ["Authentication:Google:ClientId"] = "google-client-id-for-tests",
                ["Authentication:ExternalWorkspace:BaseCurrencyCode"] = "USD"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ErpDbContext>>();
            services.RemoveAll<ErpDbContext>();
            services.RemoveAll<IGoogleIdTokenValidator>();
            services.AddDbContext<ErpDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddSingleton<IGoogleIdTokenValidator>(GoogleIdTokenValidator);
        });
    }

    public async Task SeedAdministratorAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        if (await dbContext.Users.AnyAsync(user => user.Email == AdministratorEmail))
        {
            return;
        }

        var organization = new Organization("Test Organization", "TEST");
        var company = new Company(organization.Id, "Test Company", "TEST", "USD");
        var permissions = ErpPermissions.All
            .Select(permission => new Permission(permission, permission))
            .ToArray();
        var administratorRole = new Role(company.Id, "Administrator");

        foreach (var permission in permissions)
        {
            administratorRole.GrantPermission(permission.Id);
        }

        var administrator = new User("Test Administrator", AdministratorEmail);
        administrator.AssignRole(company.Id, administratorRole.Id);
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        var credential = new UserCredential(administrator.Id, passwordHasher.Hash(AdministratorPassword));

        dbContext.AddRange(permissions);
        dbContext.AddRange(organization, company, administratorRole, administrator, credential);
        await dbContext.SaveChangesAsync();
    }
}

public sealed class TestGoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly ConcurrentDictionary<string, VerifiedGoogleIdentity> identities = new(StringComparer.Ordinal);

    public void Register(string idToken, VerifiedGoogleIdentity identity) => identities[idToken] = identity;

    public Task<VerifiedGoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(identities.TryGetValue(idToken, out var identity) ? identity : null);
}
