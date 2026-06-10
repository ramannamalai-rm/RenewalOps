using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenewalOps.Domain.Entities;

namespace RenewalOps.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.OwnerId);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.ExpiryDate);

        builder.HasQueryFilter(d => !d.IsDeleted);

        builder.Property(d => d.Title).HasMaxLength(200);
        builder.Property(d => d.StorageKey).HasMaxLength(500);
        builder.Property(d => d.OriginalFileName).HasMaxLength(500);

        builder.HasOne(d => d.Owner)
            .WithMany()
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
