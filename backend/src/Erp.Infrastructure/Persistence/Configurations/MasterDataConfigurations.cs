using Erp.Domain.Accounting;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ConfigureEntity("currencies");
        builder.Property(currency => currency.Code).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(currency => currency.Name).HasMaxLength(100).IsRequired();
        builder.Property(currency => currency.DecimalPlaces).IsRequired();
        builder.HasIndex(currency => currency.Code).IsUnique();
    }
}

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ConfigureEntity("units_of_measure");
        builder.Property(unit => unit.CompanyId);
        builder.Property(unit => unit.Code).HasMaxLength(20).IsRequired();
        builder.Property(unit => unit.Name).HasMaxLength(100).IsRequired();
        builder.Property(unit => unit.Dimension).HasMaxLength(50).IsRequired();
        builder.Property(unit => unit.ConversionFactorToBase).HasPrecision(19, 6).IsRequired();
        builder.Property(unit => unit.DecimalPlaces).IsRequired();
        builder.HasIndex(unit => new { unit.CompanyId, unit.Code }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(unit => unit.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ConfigureEntity("product_categories");
        builder.Property(category => category.CompanyId).IsRequired();
        builder.Property(category => category.Code).HasMaxLength(30).IsRequired();
        builder.Property(category => category.Name).HasMaxLength(150).IsRequired();
        builder.Property(category => category.IsActive).IsRequired();
        builder.HasIndex(category => new { category.CompanyId, category.Code }).IsUnique();
        builder.HasIndex(category => category.ParentCategoryId);
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(category => category.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(category => category.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TaxCodeConfiguration : IEntityTypeConfiguration<TaxCode>
{
    public void Configure(EntityTypeBuilder<TaxCode> builder)
    {
        builder.ConfigureEntity("tax_codes");
        builder.Property(taxCode => taxCode.CompanyId).IsRequired();
        builder.Property(taxCode => taxCode.Code).HasMaxLength(30).IsRequired();
        builder.Property(taxCode => taxCode.Rate).HasPrecision(5, 2).IsRequired();
        builder.Property(taxCode => taxCode.IsRecoverable).IsRequired();
        builder.Property(taxCode => taxCode.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(taxCode => taxCode.EffectiveTo).HasColumnType("date");
        builder.Property(taxCode => taxCode.IsActive).IsRequired();
        builder.HasIndex(taxCode => new { taxCode.CompanyId, taxCode.Code }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(taxCode => taxCode.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BusinessPartnerConfiguration : IEntityTypeConfiguration<BusinessPartner>
{
    public void Configure(EntityTypeBuilder<BusinessPartner> builder)
    {
        builder.ConfigureEntity("business_partners");
        builder.Property(partner => partner.CompanyId).IsRequired();
        builder.Property(partner => partner.Code).HasMaxLength(30).IsRequired();
        builder.Property(partner => partner.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(partner => partner.TaxIdentifier).HasMaxLength(100);
        builder.Property(partner => partner.PaymentTermsDays).IsRequired();
        builder.Property(partner => partner.IsActive).IsRequired();
        builder.HasIndex(partner => new { partner.CompanyId, partner.Code }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(partner => partner.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(partner => partner.Address, address => address.ConfigureAddress("address", required: false));
        builder.OwnsOne(partner => partner.ContactDetails, contact => contact.ConfigureContactDetails("contact"));
        builder.HasOne(partner => partner.CustomerProfile)
            .WithOne()
            .HasForeignKey<CustomerProfile>(profile => profile.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(partner => partner.SupplierProfile)
            .WithOne()
            .HasForeignKey<SupplierProfile>(profile => profile.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ConfigureEntity("customer_profiles");
        builder.Property(profile => profile.BusinessPartnerId).IsRequired();
        builder.Property(profile => profile.ReceivableAccountId);
        builder.HasIndex(profile => profile.BusinessPartnerId).IsUnique();
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(profile => profile.ReceivableAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(profile => profile.CreditLimit, money => money.ConfigureMoney("credit_limit"));
    }
}

internal sealed class SupplierProfileConfiguration : IEntityTypeConfiguration<SupplierProfile>
{
    public void Configure(EntityTypeBuilder<SupplierProfile> builder)
    {
        builder.ConfigureEntity("supplier_profiles");
        builder.Property(profile => profile.BusinessPartnerId).IsRequired();
        builder.Property(profile => profile.SupplierReference).HasMaxLength(100);
        builder.Property(profile => profile.PayableAccountId);
        builder.HasIndex(profile => profile.BusinessPartnerId).IsUnique();
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(profile => profile.PayableAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ConfigureEntity("products");
        builder.Property(product => product.CompanyId).IsRequired();
        builder.Property(product => product.Sku).HasMaxLength(60).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.ProductType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(product => product.InventoryPurpose).HasConversion<string>().HasMaxLength(30);
        builder.Property(product => product.StockUnitOfMeasureId);
        builder.Property(product => product.IsActive).IsRequired();
        builder.Ignore(product => product.IsStockTracked);
        builder.HasIndex(product => new { product.CompanyId, product.Sku }).IsUnique();
        builder.HasIndex(product => product.ProductCategoryId);
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(product => product.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(product => product.StockUnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(product => product.ProductCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PostingProfile>()
            .WithMany()
            .HasForeignKey(product => product.PostingProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
