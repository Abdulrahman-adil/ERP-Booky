using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erp.Api.IntegrationTests.Authentication;
using Erp.Api.Contracts.Authentication;
using Erp.Application.Administration;
using Erp.Application.Authentication;
using Erp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests.Administration;

public sealed class AdministrationEndpointsTests(AuthenticationApiFactory factory) : IClassFixture<AuthenticationApiFactory>
{
    [Fact]
    public async Task AdministratorCanCreateAccessRoleAndUser()
    {
        await factory.SeedAdministratorAsync();
        using var client = await CreateAdministratorClientAsync();

        using var roleResponse = await client.PostAsJsonAsync("/api/administration/roles", new
        {
            name = "Sales viewer",
            description = "Read-only sales access.",
            permissionIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.Created, roleResponse.StatusCode);
        var role = await roleResponse.Content.ReadFromJsonAsync<AdministrationRoleDto>();
        Assert.NotNull(role);

        using var userResponse = await client.PostAsJsonAsync("/api/administration/users", new
        {
            displayName = "Sales Viewer",
            email = "sales.viewer@example.test",
            roleIds = new[] { role!.Id },
            password = "A-strong-user-password-2026"
        });
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        var user = await userResponse.Content.ReadFromJsonAsync<AdministrationUserDto>();
        Assert.NotNull(user);
        Assert.Equal(["Sales viewer"], user!.RoleNames);
    }

    [Fact]
    public async Task UserWithoutAdministrationPermissionReceivesForbidden()
    {
        await factory.SeedAdministratorAsync();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var administrator = dbContext.Users.Single(user => user.Email == AuthenticationApiFactory.AdministratorEmail);
        var companyId = dbContext.UserRoleAssignments.Single(assignment => assignment.UserId == administrator.Id).CompanyId;
        var accessTokens = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        var token = accessTokens.Create(new AuthenticatedUser(
            administrator.Id,
            administrator.DisplayName,
            administrator.Email,
            ["Viewer"],
            [ErpPermissions.CustomersView],
            [companyId],
            [new CompanyPermission(companyId, ErpPermissions.CustomersView)]));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        using var response = await client.GetAsync("/api/administration/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentResetEndpointIsHiddenOutsideDevelopment()
    {
        await factory.SeedAdministratorAsync();
        using var client = await CreateAdministratorClientAsync();

        using var response = await client.GetAsync("/api/administration/development-reset");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpClient> CreateAdministratorClientAsync()
    {
        var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = AuthenticationApiFactory.AdministratorEmail,
            Password = AuthenticationApiFactory.AdministratorPassword
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
}
