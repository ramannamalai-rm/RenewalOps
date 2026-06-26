using RenewalOps.Domain.Entities;

namespace RenewalOps.Domain.Interfaces;

public interface IReminderRunRepository
{
    Task AddRangeAsync(IEnumerable<ReminderRun> reminders, CancellationToken ct = default);
    Task<ReminderRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ReminderRun>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
    Task UpdateAsync(ReminderRun reminder, CancellationToken ct = default);
}
