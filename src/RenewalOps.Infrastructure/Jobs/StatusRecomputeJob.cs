using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Recurring job (nightly) that recomputes each document's lifecycle status from its expiry
/// date: Expired if past, ExpiringSoon if within the configured window, otherwise Active.
/// Renewed documents are left untouched. Only documents whose status actually changes are
/// written, and each change is audited.
/// </summary>
public class StatusRecomputeJob
{
    private const int DefaultExpiringSoonWindowDays = 30;

    private readonly IDocumentRepository _documentRepo;
    private readonly IAuditEventRepository _auditRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<StatusRecomputeJob> _logger;

    public StatusRecomputeJob(
        IDocumentRepository documentRepo,
        IAuditEventRepository auditRepo,
        IConfiguration config,
        ILogger<StatusRecomputeJob> logger)
    {
        _documentRepo = documentRepo;
        _auditRepo = auditRepo;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var windowDays = _config.GetValue("BackgroundJobs:ExpiringSoonWindowDays", DefaultExpiringSoonWindowDays);
        var now = DateTime.UtcNow;

        var documents = await _documentRepo.GetWithExpiryForRecomputeAsync(ct);
        var changed = new List<Document>();

        foreach (var document in documents)
        {
            var newStatus = ComputeStatus(document.ExpiryDate!.Value, now, windowDays);
            if (newStatus == document.Status)
                continue;

            document.Status = newStatus;
            changed.Add(document);
        }

        if (changed.Count == 0)
        {
            _logger.LogInformation("Status recompute: no documents changed ({Total} evaluated)", documents.Count);
            return;
        }

        await _documentRepo.UpdateRangeAsync(changed, ct);

        foreach (var document in changed)
        {
            await _auditRepo.AddAsync(new AuditEvent
            {
                ActorUserId = document.OwnerId,
                DocumentId = document.Id,
                Action = nameof(AuditAction.DocumentUpdated),
                PayloadJson = $"{{\"statusRecompute\":true,\"status\":\"{document.Status}\"}}"
            }, ct);
        }

        _logger.LogInformation(
            "Status recompute: updated {Changed} of {Total} documents", changed.Count, documents.Count);
    }

    private static DocumentStatus ComputeStatus(DateTime expiryUtc, DateTime now, int windowDays)
    {
        var expiry = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc);

        if (expiry < now)
            return DocumentStatus.Expired;

        if (expiry <= now.AddDays(windowDays))
            return DocumentStatus.ExpiringSoon;

        return DocumentStatus.Active;
    }
}
