using Erp.Domain.Common;
using Erp.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static void ConfigureEntity<TEntity>(this EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : Entity
    {
        builder.ToTable(tableName);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
    }

    public static void ConfigureMoney<TEntity>(this OwnedNavigationBuilder<TEntity, Money> builder, string prefix)
        where TEntity : class
    {
        builder.Property(money => money.Amount)
            .HasColumnName($"{prefix}_amount")
            .HasPrecision(19, 4)
            .IsRequired();
        builder.Property(money => money.CurrencyCode)
            .HasColumnName($"{prefix}_currency_code")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();
    }

    public static void ConfigureQuantity<TEntity>(this OwnedNavigationBuilder<TEntity, Quantity> builder, string prefix)
        where TEntity : class
    {
        builder.Property(quantity => quantity.Amount)
            .HasColumnName($"{prefix}_amount")
            .HasPrecision(19, 6)
            .IsRequired();
        builder.Property(quantity => quantity.UnitOfMeasureId)
            .HasColumnName($"{prefix}_unit_of_measure_id")
            .IsRequired();
        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(quantity => quantity.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public static void ConfigureSourceReference<TEntity>(this OwnedNavigationBuilder<TEntity, SourceReference> builder, bool isUnique)
        where TEntity : class
    {
        builder.Property(reference => reference.Module)
            .HasColumnName("source_module")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(reference => reference.AggregateId)
            .HasColumnName("source_aggregate_id")
            .IsRequired();
        builder.Property(reference => reference.EventType)
            .HasColumnName("source_event_type")
            .HasMaxLength(100)
            .IsRequired();

        var index = builder.HasIndex(reference => new { reference.Module, reference.AggregateId, reference.EventType });

        if (isUnique)
        {
            index.IsUnique();
        }
    }

    public static void ConfigureAddress<TEntity>(this OwnedNavigationBuilder<TEntity, Address> builder, string prefix, bool required = true)
        where TEntity : class
    {
        builder.Property(address => address.Line1).HasColumnName($"{prefix}_line_1").HasMaxLength(200).IsRequired(required);
        builder.Property(address => address.Line2).HasColumnName($"{prefix}_line_2").HasMaxLength(200);
        builder.Property(address => address.City).HasColumnName($"{prefix}_city").HasMaxLength(100);
        builder.Property(address => address.PostalCode).HasColumnName($"{prefix}_postal_code").HasMaxLength(30);
        builder.Property(address => address.CountryCode).HasColumnName($"{prefix}_country_code").HasMaxLength(2).IsFixedLength().IsRequired(required);
    }

    public static void ConfigureContactDetails<TEntity>(this OwnedNavigationBuilder<TEntity, ContactDetails> builder, string prefix)
        where TEntity : class
    {
        builder.Property(contact => contact.Email).HasColumnName($"{prefix}_email").HasMaxLength(254);
        builder.Property(contact => contact.Phone).HasColumnName($"{prefix}_phone").HasMaxLength(50);
    }

    public static void ConfigureTaxSnapshot<TEntity>(this OwnedNavigationBuilder<TEntity, TaxSnapshot> builder, string prefix)
        where TEntity : class
    {
        builder.Property(tax => tax.TaxCodeId).HasColumnName($"{prefix}_tax_code_id").IsRequired();
        builder.Property(tax => tax.Rate).HasColumnName($"{prefix}_rate").HasPrecision(5, 2).IsRequired();
        builder.HasOne<TaxCode>()
            .WithMany()
            .HasForeignKey(tax => tax.TaxCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public static void ConfigureDateRange<TEntity>(this OwnedNavigationBuilder<TEntity, DateRange> builder)
        where TEntity : class
    {
        builder.Property(range => range.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(range => range.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
    }
}
