using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Application.MasterData;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.MasterData;
using Erp.Domain.Common;
using Erp.Domain.MasterData;
using Erp.Domain.Purchasing;
using Erp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests.MasterData;

public sealed class MasterDataEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task CustomerCanBeCreatedWithNameOnlyAndReceivesAUniqueGeneratedCode()
    {
        var client = await CreateAuthenticatedClientAsync();
        var request = new BusinessPartnerRequest
        {
            LegalName = "Northwind Trading"
        };

        using var createdResponse = await client.PostAsJsonAsync("/api/customers", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<BusinessPartnerDto>();
        Assert.NotNull(created);
        Assert.StartsWith("CUS-", created.Code);

        using var listResponse = await client.GetAsync("/api/customers?search=Northwind");
        Assert.True(listResponse.StatusCode == HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<BusinessPartnerDto>>();
        Assert.Contains(page!.Items, item => item.Id == created.Id);

        using var secondResponse = await client.PostAsJsonAsync("/api/customers", new BusinessPartnerRequest { LegalName = "Northwind Trading Two" });
        var second = await secondResponse.Content.ReadFromJsonAsync<BusinessPartnerDto>();
        Assert.NotEqual(created.Code, second!.Code);
    }

    [Fact]
    public async Task SupplierCanBeCreatedWithNameOnlyAndReceivesAGeneratedCode()
    {
        var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/suppliers", new BusinessPartnerRequest { LegalName = "Contoso Supplies" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var supplier = await response.Content.ReadFromJsonAsync<BusinessPartnerDto>();
        Assert.NotNull(supplier);
        Assert.StartsWith("SUP-", supplier.Code);
    }

    [Fact]
    public async Task UnusedSupplierCanBeDeletedButSupplierWithPurchasingHistoryIsProtected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var unusedSupplier = await CreateAsync<BusinessPartnerDto>(client, "/api/suppliers", new BusinessPartnerRequest { LegalName = "Unused supplier" });

        using (var deleteResponse = await client.DeleteAsync($"/api/suppliers/{unusedSupplier.Id}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        using (var getResponse = await client.GetAsync($"/api/suppliers/{unusedSupplier.Id}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        var protectedSupplier = await CreateAsync<BusinessPartnerDto>(client, "/api/suppliers", new BusinessPartnerRequest { LegalName = "Supplier with a purchase bill" });
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var companyId = await dbContext.BusinessPartners
                .Where(partner => partner.Id == protectedSupplier.Id)
                .Select(partner => partner.CompanyId)
                .SingleAsync();
            dbContext.PurchaseInvoices.Add(new PurchaseInvoice(
                companyId,
                protectedSupplier.Id,
                new DocumentNumber("PI-DELETE-GUARD"),
                new DateOnly(2026, 8, 29),
                "USD"));
            await dbContext.SaveChangesAsync();
        }

        using var protectedDeleteResponse = await client.DeleteAsync($"/api/suppliers/{protectedSupplier.Id}");
        Assert.Equal(HttpStatusCode.Conflict, protectedDeleteResponse.StatusCode);
        var problem = await protectedDeleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("commercial or financial history", problem!.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductCanBeCreatedWithNameOnlyAndClassifiedLater()
    {
        var client = await CreateAuthenticatedClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        using var response = await client.PostAsJsonAsync("/api/products", new ProductRequest
        {
            Name = "Test Product",
            ProductType = ProductType.Stock
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        Assert.StartsWith("PRD-", product!.Sku);
        Assert.Null(product.UnitOfMeasureName);
        Assert.Null(product.ProductCategoryName);

        using var secondProductResponse = await client.PostAsJsonAsync("/api/products", new ProductRequest { Name = "Test Product Two", ProductType = ProductType.Stock });
        var secondProduct = await secondProductResponse.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        Assert.NotNull(secondProduct);
        Assert.NotEqual(product.Sku, secondProduct.Sku);

        var category = await CreateAsync<ProductCategoryDto>(client, "/api/product-categories", new ProductCategoryRequest { Code = $"CAT-{suffix}", Name = "Test Category" });
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest { Code = $"U-{suffix}", Name = "Test Unit", Dimension = "Count", ConversionFactorToBase = 1, DecimalPlaces = 0 });
        using var updateResponse = await client.PutAsJsonAsync($"/api/products/{product.Id}", new ProductRequest { Name = product.Name, ProductType = ProductType.Stock, ProductCategoryId = category.Id, StockUnitOfMeasureId = unit.Id });
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        Assert.Equal(unit.Name, updated!.UnitOfMeasureName);
    }

    [Fact]
    public async Task InvalidMasterDataInputIsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/customers", new BusinessPartnerRequest { LegalName = string.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MasterDataEndpointsRequireAuthentication()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await factory.SeedAdministratorAsync();
        var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = Authentication.AuthenticationApiFactory.AdministratorEmail, Password = Authentication.AuthenticationApiFactory.AdministratorPassword });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
