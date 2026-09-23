using Erp.Domain.Common;
using Erp.Domain.Identity;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ConfigureEntity("warehouses");
        builder.Property(warehouse => warehouse.CompanyId).IsRequired();
        builder.Property(warehouse => warehouse.Code).HasMaxLength(30).IsRequired();
        builder.Property(warehouse => warehouse.Name).HasMaxLength(150).IsRequired();
        builder.Property(warehouse => warehouse.Description).HasMaxLength(500);
        builder.Property(warehouse => warehouse.IsActive).IsRequired();
        builder.HasIndex(warehouse => new { warehouse.CompanyId, warehouse.Code }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(warehouse => warehouse.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(warehouse => warehouse.Address, address => address.ConfigureAddress("address", required: false));
    }
}

internal sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ConfigureEntity("inventory_transactions");
        builder.Property(transaction => transaction.CompanyId).IsRequired();
        builder.Property(transaction => transaction.ProductId).IsRequired();
        builder.Property(transaction => transaction.WarehouseId).IsRequired();
        builder.Property(transaction => transaction.MovementNumber).HasMaxLength(30);
        builder.Property(transaction => transaction.MovementType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(transaction => transaction.OccurredAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(transaction => transaction.CreatedByUserId);
        builder.Property(transaction => transaction.ExternalReference).HasMaxLength(100);
        builder.Property(transaction => transaction.Notes).HasMaxLength(1000);
        builder.Property(transaction => transaction.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(transaction => new { transaction.CompanyId, transaction.ProductId, transaction.WarehouseId, transaction.OccurredAt });
        builder.HasIndex(transaction => new { transaction.CompanyId, transaction.MovementNumber }).IsUnique();
        builder.HasIndex(transaction => new { transaction.CompanyId, transaction.ProductId, transaction.WarehouseId })
            .IsUnique()
            .HasFilter("\"MovementType\" = 'OpeningBalance'");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(transaction => transaction.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(transaction => transaction.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(transaction => transaction.Quantity, quantity => quantity.ConfigureQuantity("quantity"));
        builder.Navigation(transaction => transaction.Quantity).IsRequired();
        builder.OwnsOne(transaction => transaction.BalanceAfterQuantity, quantity => quantity.ConfigureQuantity("balance_after_quantity"));
        builder.OwnsOne(transaction => transaction.CostAmount, money => money.ConfigureMoney("cost"));
        builder.OwnsOne(transaction => transaction.SourceReference, reference => reference.ConfigureSourceReference(false));
        builder.Navigation(transaction => transaction.SourceReference).IsRequired();
    }
}

internal sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ConfigureEntity("inventory_balances");
        builder.Property(balance => balance.CompanyId).IsRequired();
        builder.Property(balance => balance.ProductId).IsRequired();
        builder.Property(balance => balance.WarehouseId).IsRequired();
        builder.Property(balance => balance.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(balance => new { balance.CompanyId, balance.ProductId, balance.WarehouseId }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(balance => balance.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(balance => balance.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(balance => balance.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(balance => balance.OnHandQuantity, quantity => quantity.ConfigureQuantity("on_hand_quantity"));
        builder.Navigation(balance => balance.OnHandQuantity).IsRequired();
        builder.OwnsOne(balance => balance.InventoryValue, money => money.ConfigureMoney("inventory_value"));
    }
}
