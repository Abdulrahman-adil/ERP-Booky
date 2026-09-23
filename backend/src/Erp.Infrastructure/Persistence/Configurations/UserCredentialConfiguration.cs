using Erp.Domain.Identity;
using Erp.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).ValueGeneratedNever();
        builder.Property(credential => credential.UserId).IsRequired();
        builder.Property(credential => credential.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(credential => credential.PasswordChangedAt).IsRequired();
        builder.HasIndex(credential => credential.UserId).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
