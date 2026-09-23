using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Erp.Domain.Sales;
using Erp.Domain.Identity;
using Erp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ConfigureEntity("sales_invoices");
        builder.Property(invoice => invoice.CompanyId).IsRequired();
        builder.Property(invoice => invoice.CustomerId).IsRequired();
        builder.Property(invoice => invoice.WarehouseId);
        builder.Property(invoice => invoice.InvoiceDate).HasColumnType("date").IsRequired();
        builder.Property(invoice => invoice.DueDate).HasColumnType("date");
        builder.Property(invoice => invoice.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(invoice => invoice.Reference).HasMaxLength(100);
        builder.Property(invoice => invoice.Notes).HasMaxLength(1000);
        builder.Property(invoice => invoice.CreatedByUserId);
        builder.Property(invoice => invoice.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(invoice => invoice.PostedByUserId);
        builder.Property(invoice => invoice.PostedAt).HasColumnType("timestamp with time zone");
        builder.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.DocumentNumber)
            .HasConversion(number => number.Value, value => new Erp.Domain.Common.DocumentNumber(value))
            .HasColumnName("document_number")
            .HasMaxLength(40)
            .IsRequired();
        builder.HasIndex(invoice => new { invoice.CompanyId, invoice.DocumentNumber }).IsUnique();
        builder.HasIndex(invoice => new { invoice.CompanyId, invoice.CustomerId, invoice.InvoiceDate });
        builder.HasIndex(invoice => new { invoice.CompanyId, invoice.Status, invoice.InvoiceDate });
        builder.HasIndex(invoice => invoice.WarehouseId);
        builder.HasIndex(invoice => invoice.CreatedByUserId);
        builder.HasIndex(invoice => invoice.PostedByUserId);
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(invoice => invoice.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(invoice => invoice.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(invoice => invoice.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invoice => invoice.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invoice => invoice.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(invoice => invoice.SourceReference, reference => reference.ConfigureSourceReference(false));
        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey("SalesInvoiceId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(invoice => invoice.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.ConfigureEntity("sales_invoice_lines");
        builder.Property(line => line.LineNumber).IsRequired();
        builder.Property(line => line.Description).HasMaxLength(500).IsRequired();
        builder.Property(line => line.ProductId);
        builder.Property(line => line.DiscountPercentage).HasPrecision(5, 2).IsRequired();
        builder.HasIndex("SalesInvoiceId", nameof(SalesInvoiceLine.LineNumber)).IsUnique();
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(line => line.Quantity, quantity => quantity.ConfigureQuantity("quantity"));
        builder.Navigation(line => line.Quantity).IsRequired();
        builder.OwnsOne(line => line.UnitPrice, money => money.ConfigureMoney("unit_price"));
        builder.Navigation(line => line.UnitPrice).IsRequired();
        builder.OwnsOne(line => line.Tax, tax =>
        {
            tax.ToTable("sales_invoice_line_taxes");
            tax.WithOwner().HasForeignKey("SalesInvoiceLineId");
            tax.ConfigureTaxSnapshot("tax");
        });
    }
}
