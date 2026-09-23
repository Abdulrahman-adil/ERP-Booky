using Erp.Application.Authentication;
using Erp.Application.Accounting;
using Erp.Application.MasterData;
using Erp.Application.Inventory;
using Erp.Application.Sales;
using Erp.Application.Payments;
using Erp.Application.Purchasing;
using Erp.Application.Documents;
using Erp.Application.Manufacturing;
using Erp.Application.Administration;
using Erp.Infrastructure.Authentication;
using Erp.Infrastructure.Reporting;
using Erp.Infrastructure.Documents;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The PostgreSQL connection string is not configured.");
        }

        services.AddDbContext<ErpDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)
                    .MigrationsHistoryTable("__EFMigrationsHistory", "erp")));
        services.AddScoped<IUserAuthenticationStore, EfUserAuthenticationStore>();
        services.AddScoped<IExternalWorkspaceProvisioner, EfExternalWorkspaceProvisioner>();
        services.AddScoped<IMasterDataService, EfMasterDataService>();
        services.AddScoped<IInventoryService, EfInventoryService>();
        services.AddScoped<IManufacturingService, EfManufacturingService>();
        services.AddScoped<IAccountingService, EfAccountingService>();
        services.AddScoped<IFinancialReportingService, EfFinancialReportingService>();
        services.AddScoped<IFinancialReportExportService, FinancialReportExportService>();
        services.AddScoped<IDocumentAttachmentService, EfDocumentAttachmentService>();
        services.Configure<AttachmentStorageOptions>(configuration.GetSection(AttachmentStorageOptions.SectionName));
        services.AddSingleton<IAttachmentStorage, LocalAttachmentStorage>();
        services.AddScoped<ISalesService, EfSalesService>();
        services.AddScoped<IPaymentService, EfPaymentService>();
        services.AddScoped<IPurchaseService, EfPurchaseService>();
        services.AddScoped<IAdministrationService, EfAdministrationService>();
        services.AddScoped<IDevelopmentDataResetService, EfDevelopmentDataResetService>();
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.Configure<ExternalWorkspaceOptions>(configuration.GetSection(ExternalWorkspaceOptions.SectionName));
        services.Configure<DevelopmentAdminOptions>(configuration.GetSection(DevelopmentAdminOptions.SectionName));
        services.AddScoped<DevelopmentIdentitySeeder>();

        return services;
    }
}
