using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erp.Api.Contracts.Authentication;
using Erp.Application.Authentication;
using Erp.Domain.Accounting;
using Erp.Domain.Identity;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests.Authentication;

public sealed class AuthenticationEndpointsTests(AuthenticationApiFactory factory) : IClassFixture<AuthenticationApiFactory>
{
    [Fact]
    public async Task ValidCredentialsReturnATokenAndCurrentUser()
    {
        await factory.SeedAdministratorAsync();
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = AuthenticationApiFactory.AdministratorEmail,
                Password = AuthenticationApiFactory.AdministratorPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.NotEmpty(login.AccessToken);
        Assert.Contains("Administrator", login.User.Roles);
        Assert.Contains("users.manage", login.User.Permissions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var currentUserResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(AuthenticationApiFactory.AdministratorEmail, currentUser!.Email);
    }

    [Fact]
    public async Task InvalidCredentialsAreRejected()
    {
        await factory.SeedAdministratorAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = AuthenticationApiFactory.AdministratorEmail,
                Password = "incorrect-password"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUserEndpointRequiresAuthentication()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUserReportsThePermissionSnapshotFromTheAccessToken()
    {
        await factory.SeedAdministratorAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var administrator = await dbContext.Users.SingleAsync(user => user.Email == AuthenticationApiFactory.AdministratorEmail);
        var companyId = await dbContext.UserRoleAssignments
            .Where(assignment => assignment.UserId == administrator.Id)
            .Select(assignment => assignment.CompanyId)
            .SingleAsync();
        var accessTokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        var accessToken = accessTokenService.Create(new AuthenticatedUser(
            administrator.Id,
            administrator.DisplayName,
            administrator.Email,
            ["Administrator"],
            ["users.manage"],
            [companyId],
            [new CompanyPermission(companyId, "users.manage")]));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Value);

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(["users.manage"], currentUser!.Permissions);
        Assert.DoesNotContain("payables.view", currentUser.Permissions);
    }

    [Fact]
    public async Task ValidGoogleIdentityCreatesAnIndependentWorkspaceOnlyOnce()
    {
        const string idToken = "google-id-token-created-for-external-workspace-test";
        const string subject = "google-subject-001";
        factory.GoogleIdTokenValidator.Register(idToken, new VerifiedGoogleIdentity(subject, "google.owner@example.test", "Google Workspace Owner"));
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest { IdToken = idToken });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstLogin = await firstResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(firstLogin);
        Assert.Equal(["Owner"], firstLogin.User.Roles);
        Assert.Single(firstLogin.User.CompanyIds);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var externalIdentity = await dbContext.ExternalIdentities.SingleAsync(identity => identity.Provider == ExternalIdentityProvider.Google && identity.ProviderSubject == subject);
        var companyId = Assert.Single(await dbContext.UserRoleAssignments
            .Where(assignment => assignment.UserId == externalIdentity.UserId)
            .Select(assignment => assignment.CompanyId)
            .ToArrayAsync());

        Assert.Null(await dbContext.UserCredentials.SingleOrDefaultAsync(credential => credential.UserId == externalIdentity.UserId));
        Assert.True(await dbContext.ChartsOfAccounts.AnyAsync(chart => chart.CompanyId == companyId));
        Assert.True(await dbContext.AccountingPeriods.AnyAsync(period => period.CompanyId == companyId && period.Status == AccountingPeriodStatus.Open));
        Assert.True(await dbContext.CashBankAccounts.AnyAsync(account => account.CompanyId == companyId && account.IsActive));
        Assert.True(await dbContext.PostingProfiles.AnyAsync(profile => profile.CompanyId == companyId && profile.IsActive));
        Assert.Equal(0, await dbContext.SalesInvoices.CountAsync(invoice => invoice.CompanyId == companyId));
        Assert.Equal(0, await dbContext.PurchaseInvoices.CountAsync(invoice => invoice.CompanyId == companyId));
        Assert.Equal(0, await dbContext.Payments.CountAsync(payment => payment.CompanyId == companyId));
        Assert.Equal(0, await dbContext.JournalEntries.CountAsync(journal => journal.CompanyId == companyId));

        using var returningResponse = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest { IdToken = idToken });
        Assert.Equal(HttpStatusCode.OK, returningResponse.StatusCode);
        var returningLogin = await returningResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(returningLogin);
        Assert.Equal(firstLogin.User.Id, returningLogin.User.Id);
        Assert.Equal(firstLogin.User.CompanyIds, returningLogin.User.CompanyIds);
        Assert.Equal(1, await dbContext.ExternalIdentities.CountAsync(identity => identity.Provider == ExternalIdentityProvider.Google && identity.ProviderSubject == subject));
    }

    [Fact]
    public async Task GoogleIdentityWithAnExistingUnlinkedEmailRequiresAnExplicitLinkingFlow()
    {
        await factory.SeedAdministratorAsync();
        const string idToken = "google-id-token-for-an-existing-password-account";
        factory.GoogleIdTokenValidator.Register(idToken, new VerifiedGoogleIdentity("google-subject-existing-email", AuthenticationApiFactory.AdministratorEmail, "Existing Account"));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest { IdToken = idToken });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        Assert.False(await dbContext.ExternalIdentities.AnyAsync(identity => identity.ProviderSubject == "google-subject-existing-email"));
    }
}
