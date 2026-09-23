using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erp.Api.Contracts.Authentication;
using Erp.Application.Authentication;
using Erp.Domain.Identity;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests.Authentication;

public sealed class CompanyIsolationEndpointsTests(AuthenticationApiFactory factory) : IClassFixture<AuthenticationApiFactory>
{
    [Fact]
    public async Task MultiCompanyUsersCanUseOnlyTheirSelectedCompanyPermissions()
    {
        await factory.SeedAdministratorAsync();
        Guid firstCompanyId;
        Guid secondCompanyId;
        Guid secondCompanyCustomerId;
        Guid secondCompanySupplierId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var administrator = await dbContext.Users.SingleAsync(user => user.Email == AuthenticationApiFactory.AdministratorEmail);
            firstCompanyId = await dbContext.UserRoleAssignments
                .Where(assignment => assignment.UserId == administrator.Id)
                .Select(assignment => assignment.CompanyId)
                .SingleAsync();
            var organization = new Organization("Second Test Organization", "SECOND");
            var company = new Company(organization.Id, "Second Test Company", "SECOND", "USD");
            var customerViewPermission = await dbContext.Permissions.SingleAsync(permission => permission.Key == ErpPermissions.CustomersView);
            var customerViewerRole = new Role(company.Id, "Customer Viewer");
            customerViewerRole.GrantPermission(customerViewPermission.Id);
            administrator.AssignRole(company.Id, customerViewerRole.Id);

            var customer = new BusinessPartner(company.Id, "CUST-SECOND", "Second Company Customer");
            customer.EnableCustomer();
            var supplier = new BusinessPartner(company.Id, "SUP-SECOND", "Second Company Supplier");
            supplier.EnableSupplier();
            dbContext.AddRange(organization, company, customerViewerRole, customer, supplier);
            await dbContext.SaveChangesAsync();

            secondCompanyId = company.Id;
            secondCompanyCustomerId = customer.Id;
            secondCompanySupplierId = supplier.Id;
        }

        using var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = AuthenticationApiFactory.AdministratorEmail,
            Password = AuthenticationApiFactory.AdministratorPassword
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var firstCompanyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{secondCompanyCustomerId}");
        firstCompanyRequest.Headers.Add("X-Company-Id", firstCompanyId.ToString());
        using var firstCompanyLookup = await client.SendAsync(firstCompanyRequest);
        Assert.Equal(HttpStatusCode.NotFound, firstCompanyLookup.StatusCode);

        using var supplierReadRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/suppliers/{secondCompanySupplierId}");
        supplierReadRequest.Headers.Add("X-Company-Id", firstCompanyId.ToString());
        using var supplierReadResponse = await client.SendAsync(supplierReadRequest);
        Assert.Equal(HttpStatusCode.NotFound, supplierReadResponse.StatusCode);

        using var supplierDeleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/suppliers/{secondCompanySupplierId}");
        supplierDeleteRequest.Headers.Add("X-Company-Id", firstCompanyId.ToString());
        using var supplierDeleteResponse = await client.SendAsync(supplierDeleteRequest);
        Assert.Equal(HttpStatusCode.NotFound, supplierDeleteResponse.StatusCode);

        using (var verificationScope = factory.Services.CreateScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            Assert.True(await dbContext.BusinessPartners.AnyAsync(partner => partner.CompanyId == secondCompanyId && partner.Id == secondCompanySupplierId));
        }

        using var authorizedCompanyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{secondCompanyCustomerId}");
        authorizedCompanyRequest.Headers.Add("X-Company-Id", secondCompanyId.ToString());
        using var authorizedCompanyLookup = await client.SendAsync(authorizedCompanyRequest);
        Assert.Equal(HttpStatusCode.OK, authorizedCompanyLookup.StatusCode);

        using var writeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers");
        writeRequest.Headers.Add("X-Company-Id", secondCompanyId.ToString());
        using var writeResponse = await client.SendAsync(writeRequest);
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);

        using var reportsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/financial-reports/trial-balance");
        reportsRequest.Headers.Add("X-Company-Id", secondCompanyId.ToString());
        using var reportsResponse = await client.SendAsync(reportsRequest);
        Assert.Equal(HttpStatusCode.Forbidden, reportsResponse.StatusCode);

        using var attachmentsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/documents/Journal/{Guid.NewGuid()}/attachments");
        attachmentsRequest.Headers.Add("X-Company-Id", secondCompanyId.ToString());
        using var attachmentsResponse = await client.SendAsync(attachmentsRequest);
        Assert.Equal(HttpStatusCode.Forbidden, attachmentsResponse.StatusCode);

        using var unassignedCompanyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        unassignedCompanyRequest.Headers.Add("X-Company-Id", Guid.NewGuid().ToString());
        using var unassignedCompanyResponse = await client.SendAsync(unassignedCompanyRequest);
        Assert.Equal(HttpStatusCode.Forbidden, unassignedCompanyResponse.StatusCode);
    }
}
