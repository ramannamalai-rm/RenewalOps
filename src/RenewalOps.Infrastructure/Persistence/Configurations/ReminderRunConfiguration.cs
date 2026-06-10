using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenewalOps.Domain.Entities;

namespace RenewalOps.Infrastructure.Persistence.Configurations;

public class ReminderRunConfiguration : IEntityTypeConfiguration<ReminderRun>
{
    public void Configure(EntityTypeBuilder<ReminderRun> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.DocumentId);

        builder.HasOne(r => r.Document)
            .WithMany()
            .HasForeignKey(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
