using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Security;
using Erp.Application.Authentication;
using Erp.Domain.Identity;
using Erp.Domain.Organizations;
using Erp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests.Authentication;

public sealed class ProductionSecurityTests
{
    [Theory]
    [InlineData("GET", "/api/customers")]
    [InlineData("POST", "/api/customers")]
    [InlineData("PUT", "/api/customers/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/suppliers/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/sales-invoices/11111111-1111-1111-1111-111111111111/post")]
    [InlineData("POST", "/api/purchase-bills/11111111-1111-1111-1111-111111111111/post")]
    [InlineData("GET", "/api/financial-reports/trial-balance/export")]
    [InlineData("GET", "/api/manufacturing/production-orders")]
    [InlineData("GET", "/api/administration/users")]
    [InlineData("GET", "/api/documents/Journal/11111111-1111-1111-1111-111111111111/attachments")]
    public async Task SensitiveEndpointsRejectAnonymousAndForgedCompany(string method, string path)
    {
        await using var factory = new AuthenticationApiFactory();
        await factory.SeedAdministratorAsync();
        using var client = factory.CreateClient();
        using var anonymous = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var login = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        client.DefaultRequestHeaders.Add("X-Company-Id", Guid.NewGuid().ToString());
        using var forgedCompany = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Forbidden, forgedCompany.StatusCode);
    }

    [Theory]
    [InlineData("user", HttpStatusCode.Unauthorized)]
    [InlineData("role", HttpStatusCode.Forbidden)]
    [InlineData("company", HttpStatusCode.Forbidden)]
    [InlineData("password", HttpStatusCode.Unauthorized)]
    public async Task ExistingTokensLoseRevokedAccessImmediately(string change, HttpStatusCode expected)
    {
        await using var factory = new AuthenticationApiFactory();
        await factory.SeedAdministratorAsync();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            if (change == "user") (await database.Users.SingleAsync()).SetActive(false);
            if (change == "role") (await database.Roles.SingleAsync()).SetActive(false);
            if (change == "company") (await database.Companies.SingleAsync()).SetActive(false);
            if (change == "password")
                (await database.UserCredentials.SingleAsync()).SetPasswordHash(scope.ServiceProvider.GetRequiredService<IPasswordHashingService>().Hash("Different-test-password-123"));
            await database.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var response = await client.GetAsync("/api/customers");
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SharedUserCannotBeTakenOverByOneCompanyAdministrator()
    {
        await using var factory = new AuthenticationApiFactory();
        await factory.SeedAdministratorAsync();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        Guid sharedUserId;
        Guid firstRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            firstRoleId = (await database.Roles.SingleAsync()).Id;
            var organization = new Organization("Isolated security test", "SEC");
            var company = new Company(organization.Id, "Second company", "SEC", "USD");
            var role = new Role(company.Id, "Second owner");
            var user = new User("Shared test user", "shared@example.test");
            user.AssignRole(login.User.CompanyIds.Single(), firstRoleId);
            user.AssignRole(company.Id, role.Id);
            sharedUserId = user.Id;
            database.AddRange(organization, company, role, user);
            await database.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var reset = await client.PutAsJsonAsync($"/api/administration/users/{sharedUserId}", new
        {
            displayName = "Shared test user", email = "shared@example.test", password = "Unapproved-reset-123", roleIds = new[] { firstRoleId }
        });
        Assert.Equal(HttpStatusCode.Conflict, reset.StatusCode);
        using var deactivate = await client.PutAsJsonAsync($"/api/administration/users/{sharedUserId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        using var scopedUpdate = await client.PutAsJsonAsync($"/api/administration/users/{sharedUserId}", new
        {
            displayName = "Shared test user", email = "shared@example.test", roleIds = new[] { firstRoleId }
        });
        Assert.Equal(HttpStatusCode.OK, scopedUpdate.StatusCode);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ErpDbContext>();
        Assert.Equal(2, await context.UserRoleAssignments.CountAsync(item => item.UserId == sharedUserId));
        Assert.False(await context.UserCredentials.AnyAsync(item => item.UserId == sharedUserId));
        Assert.True((await context.Users.SingleAsync(item => item.Id == sharedUserId)).IsActive);
    }

    [Fact]
    public async Task LoginLimitCoversGoogleAndPasswordButNotHealthOrNormalEndpoints()
    {
        await using var original = new AuthenticationApiFactory();
        await using var factory = original.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:LoginPermitLimit"] = "2" })));
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid" });
        await client.PostAsJsonAsync("/api/auth/google", new { idToken = "invalid" });
        using var rejected = await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers")).StatusCode);
    }

    [Fact]
    public async Task SecurityHeadersArePresentAndUntrustedCorsIsDenied()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        using var response = await client.GetAsync("/api/auth/me");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://erp.example")]
    [InlineData("https://erp.example/path")]
    [InlineData("https://localhost")]
    public void ProductionRejectsUnsafeCorsConfiguration(string origin)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = origin,
            ["Authentication:Jwt:SigningKey"] = new string('a', 64),
            ["AllowedHosts"] = "erp.example"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => ProductionSecurity.ValidateProductionConfiguration(configuration));
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = AuthenticationApiFactory.AdministratorEmail,
            Password = AuthenticationApiFactory.AdministratorPassword
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
}
