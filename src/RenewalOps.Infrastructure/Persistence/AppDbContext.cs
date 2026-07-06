using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RenewalOps.Domain.Entities;

namespace RenewalOps.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ReminderRun> ReminderRuns => Set<ReminderRun>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType.IsEnum ||
                    (Nullable.GetUnderlyingType(property.ClrType)?.IsEnum ?? false))
                {
                    var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    var converterType = typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.EnumToStringConverter<>)
                        .MakeGenericType(enumType);
                    var converter = (Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)
                        Activator.CreateInstance(converterType)!;
                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
