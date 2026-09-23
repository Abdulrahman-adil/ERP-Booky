using Erp.Domain.Identity;
using Erp.Domain.Inventory;
using Erp.Domain.Manufacturing;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class BillOfMaterialsConfiguration : IEntityTypeConfiguration<BillOfMaterials>
{
    public void Configure(EntityTypeBuilder<BillOfMaterials> builder)
    {
        builder.ConfigureEntity("bills_of_materials");
        builder.Property(bill => bill.CompanyId).IsRequired();
        builder.Property(bill => bill.Code).HasMaxLength(30).IsRequired();
        builder.Property(bill => bill.FinishedProductId).IsRequired();
        builder.Property(bill => bill.OutputQuantity).HasPrecision(19, 6).IsRequired();
        builder.Property(bill => bill.OutputUnitOfMeasureId).IsRequired();
        builder.Property(bill => bill.EffectiveFrom).HasColumnType("date");
        builder.Property(bill => bill.EffectiveTo).HasColumnType("date");
        builder.Property(bill => bill.Notes).HasMaxLength(1000);
        builder.Property(bill => bill.IsActive).IsRequired();
        builder.Property(bill => bill.CreatedByUserId).IsRequired();
        builder.Property(bill => bill.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(bill => bill.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(bill => new { bill.CompanyId, bill.Code }).IsUnique();
        builder.HasIndex(bill => new { bill.CompanyId, bill.FinishedProductId, bill.IsActive });
        builder.HasOne<Company>().WithMany().HasForeignKey(bill => bill.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(bill => bill.FinishedProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(bill => bill.OutputUnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(bill => bill.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(bill => bill.Components).WithOne().HasForeignKey("BillOfMaterialsId").IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(bill => bill.Components).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BillOfMaterialsComponentConfiguration : IEntityTypeConfiguration<BillOfMaterialsComponent>
{
    public void Configure(EntityTypeBuilder<BillOfMaterialsComponent> builder)
    {
        builder.ConfigureEntity("bill_of_material_components");
        builder.Property(component => component.LineNumber).IsRequired();
        builder.Property(component => component.ComponentProductId).IsRequired();
        builder.Property(component => component.Quantity).HasPrecision(19, 6).IsRequired();
        builder.Property(component => component.UnitOfMeasureId).IsRequired();
        builder.HasIndex("BillOfMaterialsId", nameof(BillOfMaterialsComponent.LineNumber)).IsUnique();
        builder.HasIndex("BillOfMaterialsId", nameof(BillOfMaterialsComponent.ComponentProductId)).IsUnique();
        builder.HasOne<Product>().WithMany().HasForeignKey(component => component.ComponentProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(component => component.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ConfigureEntity("production_orders");
        builder.Property(order => order.CompanyId).IsRequired();
        builder.Property(order => order.ProductionOrderNumber).HasMaxLength(40).IsRequired();
        builder.Property(order => order.FinishedProductId).IsRequired();
        builder.Property(order => order.BillOfMaterialsId).IsRequired();
        builder.Property(order => order.PlannedQuantity).HasPrecision(19, 6).IsRequired();
        builder.Property(order => order.ActualProducedQuantity).HasPrecision(19, 6);
        builder.Property(order => order.OutputUnitOfMeasureId).IsRequired();
        builder.Property(order => order.SourceWarehouseId).IsRequired();
        builder.Property(order => order.DestinationWarehouseId).IsRequired();
        builder.Property(order => order.ProductionDate).HasColumnType("date").IsRequired();
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(order => order.Notes).HasMaxLength(1000);
        builder.Property(order => order.CreatedByUserId).IsRequired();
        builder.Property(order => order.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(order => order.ReleasedAt).HasColumnType("timestamp with time zone");
        builder.Property(order => order.PostedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(order => new { order.CompanyId, order.ProductionOrderNumber }).IsUnique();
        builder.HasIndex(order => new { order.CompanyId, order.Status, order.ProductionDate });
        builder.HasOne<Company>().WithMany().HasForeignKey(order => order.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(order => order.FinishedProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BillOfMaterials>().WithMany().HasForeignKey(order => order.BillOfMaterialsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(order => order.OutputUnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(order => order.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(order => order.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.PostedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Materials).WithOne().HasForeignKey("ProductionOrderId").IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(order => order.Materials).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProductionOrderMaterialConfiguration : IEntityTypeConfiguration<ProductionOrderMaterial>
{
    public void Configure(EntityTypeBuilder<ProductionOrderMaterial> builder)
    {
        builder.ConfigureEntity("production_order_materials");
        builder.Property(material => material.LineNumber).IsRequired();
        builder.Property(material => material.ComponentProductId).IsRequired();
        builder.Property(material => material.QuantityPerBomOutput).HasPrecision(19, 6).IsRequired();
        builder.Property(material => material.BomOutputQuantity).HasPrecision(19, 6).IsRequired();
        builder.Property(material => material.UnitOfMeasureId).IsRequired();
        builder.HasIndex("ProductionOrderId", nameof(ProductionOrderMaterial.LineNumber)).IsUnique();
        builder.HasIndex("ProductionOrderId", nameof(ProductionOrderMaterial.ComponentProductId)).IsUnique();
        builder.HasOne<Product>().WithMany().HasForeignKey(material => material.ComponentProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(material => material.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
    }
}
