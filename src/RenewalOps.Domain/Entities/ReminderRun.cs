using RenewalOps.Domain.Enums;

namespace RenewalOps.Domain.Entities;

public class ReminderRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public DateTime ScheduledForUtc { get; set; }
    public DateTime? ExecutedUtc { get; set; }

    public ReminderChannel Channel { get; set; }
    public ReminderStatus Status { get; set; }
}
