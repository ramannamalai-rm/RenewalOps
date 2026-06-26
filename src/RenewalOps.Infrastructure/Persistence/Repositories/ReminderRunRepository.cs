using Microsoft.EntityFrameworkCore;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Persistence.Repositories;

public class ReminderRunRepository : IReminderRunRepository
{
    private readonly AppDbContext _db;

    public ReminderRunRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<ReminderRun> reminders, CancellationToken ct = default)
    {
        _db.ReminderRuns.AddRange(reminders);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ReminderRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ReminderRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<ReminderRun>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        return await _db.ReminderRuns
            .Where(r => r.DocumentId == documentId)
            .OrderBy(r => r.ScheduledForUtc)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(ReminderRun reminder, CancellationToken ct = default)
    {
        _db.ReminderRuns.Update(reminder);
        await _db.SaveChangesAsync(ct);
    }
}
