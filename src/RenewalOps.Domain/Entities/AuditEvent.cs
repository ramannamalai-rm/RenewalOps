namespace RenewalOps.Domain.Entities;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;

    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    public required string Action { get; set; }
    public string? PayloadJson { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
