namespace RenewalOps.Application.Interfaces;

public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken ct = default);
}

public record OcrResult(string RawText, DateTime? DetectedExpiryDate, DateTime? DetectedIssueDate);
