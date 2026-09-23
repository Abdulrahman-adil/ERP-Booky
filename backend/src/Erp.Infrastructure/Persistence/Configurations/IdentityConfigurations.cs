using Erp.Domain.Identity;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ConfigureEntity("permissions");
        builder.Property(permission => permission.Key).HasMaxLength(150).IsRequired();
        builder.Property(permission => permission.Name).HasMaxLength(150).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(500);
        builder.Property(permission => permission.IsActive).IsRequired();
        builder.HasIndex(permission => permission.Key).IsUnique();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ConfigureEntity("roles");
        builder.Property(role => role.CompanyId).IsRequired();
        builder.Property(role => role.Name).HasMaxLength(100).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(500);
        builder.Property(role => role.IsActive).IsRequired();
        builder.HasIndex(role => new { role.CompanyId, role.Name }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(role => role.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(role => role.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ConfigureEntity("role_permissions");
        builder.Property(permission => permission.RoleId).IsRequired();
        builder.Property(permission => permission.PermissionId).IsRequired();
        builder.HasIndex(permission => new { permission.RoleId, permission.PermissionId }).IsUnique();
        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(permission => permission.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ConfigureEntity("users");
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasMany(user => user.RoleAssignments)
            .WithOne()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(user => user.RoleAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ConfigureEntity("user_role_assignments");
        builder.Property(assignment => assignment.UserId).IsRequired();
        builder.Property(assignment => assignment.CompanyId).IsRequired();
        builder.Property(assignment => assignment.RoleId).IsRequired();
        builder.HasIndex(assignment => new { assignment.UserId, assignment.CompanyId, assignment.RoleId }).IsUnique();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(assignment => assignment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ConfigureEntity("external_identities");
        builder.Property(identity => identity.UserId).IsRequired();
        builder.Property(identity => identity.Provider).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(identity => identity.ProviderSubject).HasMaxLength(255).IsRequired();
        builder.Property(identity => identity.LinkedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(identity => new { identity.Provider, identity.ProviderSubject }).IsUnique();
        builder.HasIndex(identity => new { identity.UserId, identity.Provider }).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(identity => identity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
