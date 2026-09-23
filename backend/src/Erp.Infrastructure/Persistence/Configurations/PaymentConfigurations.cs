using Erp.Domain.Accounting;
using Erp.Domain.MasterData;
using Erp.Domain.Payments;
using Erp.Domain.Organizations;
using Erp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ConfigureEntity("payments");
        builder.Property(payment => payment.CompanyId).IsRequired();
        builder.Property(payment => payment.BusinessPartnerId).IsRequired();
        builder.Property(payment => payment.CashBankAccountId).IsRequired();
        builder.Property(payment => payment.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.DocumentNumber)
            .HasConversion(number => number.Value, value => new Erp.Domain.Common.DocumentNumber(value))
            .HasColumnName("document_number")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(payment => payment.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.ExternalReference).HasMaxLength(100);
        builder.Property(payment => payment.Notes).HasMaxLength(1000);
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.CreatedByUserId);
        builder.Property(payment => payment.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(payment => payment.PostedByUserId);
        builder.Property(payment => payment.PostedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(payment => new { payment.CompanyId, payment.BusinessPartnerId, payment.PaymentDate });
        builder.HasIndex(payment => new { payment.CompanyId, payment.DocumentNumber }).IsUnique();
        builder.HasIndex(payment => new { payment.CompanyId, payment.Status, payment.PaymentDate });
        builder.HasIndex(payment => payment.CashBankAccountId);
        builder.HasIndex(payment => payment.CreatedByUserId);
        builder.HasIndex(payment => payment.PostedByUserId);
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(payment => payment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(payment => payment.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>()
            .WithMany()
            .HasForeignKey(payment => payment.CashBankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(payment => payment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(payment => payment.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(payment => payment.Amount, money => money.ConfigureMoney("payment"));
        builder.Navigation(payment => payment.Amount).IsRequired();
        builder.OwnsOne(payment => payment.SourceReference, reference => reference.ConfigureSourceReference(true));
        builder.Navigation(payment => payment.SourceReference).IsRequired();
        builder.HasMany(payment => payment.Allocations)
            .WithOne()
            .HasForeignKey(allocation => allocation.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(payment => payment.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ConfigureEntity("payment_allocations");
        builder.Property(allocation => allocation.PaymentId).IsRequired();
        builder.Property(allocation => allocation.OpenItemId).IsRequired();
        builder.HasIndex(allocation => new { allocation.PaymentId, allocation.OpenItemId }).IsUnique();
        builder.HasIndex(allocation => allocation.OpenItemId);
        builder.HasOne<OpenItem>()
            .WithMany()
            .HasForeignKey(allocation => allocation.OpenItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(allocation => allocation.Amount, money => money.ConfigureMoney("allocated"));
        builder.Navigation(allocation => allocation.Amount).IsRequired();
    }
}
