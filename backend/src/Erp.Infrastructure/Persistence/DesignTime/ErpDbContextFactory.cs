using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Erp.Infrastructure.Persistence.DesignTime;

public sealed class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set the ConnectionStrings__PostgreSQL environment variable before running Entity Framework Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ErpDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", "erp"));

        return new ErpDbContext(optionsBuilder.Options);
    }
}
