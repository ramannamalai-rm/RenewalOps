using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenewalOps.Domain.Entities;

namespace RenewalOps.Infrastructure.Persistence.Configurations;

public class GoogleConnectionConfiguration : IEntityTypeConfiguration<GoogleConnection>
{
    public void Configure(EntityTypeBuilder<GoogleConnection> builder)
    {
        builder.HasKey(c => c.Id);

        // One Google connection per user.
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.Property(c => c.EncryptedRefreshToken).IsRequired();
        builder.Property(c => c.Scopes).HasMaxLength(1000);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
