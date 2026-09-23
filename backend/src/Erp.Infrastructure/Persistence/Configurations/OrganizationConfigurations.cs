using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ConfigureEntity("organizations");
        builder.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
        builder.Property(organization => organization.Code).HasMaxLength(30).IsRequired();
        builder.Property(organization => organization.IsActive).IsRequired();
        builder.HasIndex(organization => organization.Code).IsUnique();
    }
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ConfigureEntity("companies");
        builder.Property(company => company.OrganizationId).IsRequired();
        builder.Property(company => company.Name).HasMaxLength(200).IsRequired();
        builder.Property(company => company.Code).HasMaxLength(30).IsRequired();
        builder.Property(company => company.BaseCurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(company => company.TaxIdentifier).HasMaxLength(100);
        builder.Property(company => company.IsActive).IsRequired();
        builder.HasIndex(company => new { company.OrganizationId, company.Code }).IsUnique();
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(company => company.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
