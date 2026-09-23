using Erp.Api.IntegrationTests.Infrastructure;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests;

public sealed class ApiStartupTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task HealthEndpointReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public void DbContextUsesPostgreSqlProvider()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public async Task SwaggerDocumentIsAvailableOutsideProduction()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.True(response.IsSuccessStatusCode);
    }
}
