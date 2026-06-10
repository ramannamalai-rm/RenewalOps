using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using Tesseract;

namespace RenewalOps.Infrastructure.Services;

public sealed partial class TesseractOcrService : IOcrService, IDisposable
{
    private readonly TesseractEngine _engine;
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd",
        "dd-MM-yyyy", "MM-dd-yyyy", "yyyy-MM-dd",
        "dd.MM.yyyy", "MM.dd.yyyy", "yyyy.MM.dd",
        "dd/MM/yy",   "MM/dd/yy",
        "MMMM d, yyyy", "MMMM dd, yyyy",
        "MMM d, yyyy",  "MMM dd, yyyy",
        "d MMMM yyyy",  "dd MMMM yyyy"
    ];

    public TesseractOcrService(IConfiguration config, ILogger<TesseractOcrService> logger)
    {
        var tessdataPath = config["Ocr:TessdataPath"] ?? "./tessdata";
        _engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
        _logger = logger;
    }

    public async Task<OcrResult> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken ct = default)
    {
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("PDF OCR not supported in MVP; returning empty result");
            return new OcrResult(string.Empty, null, null);
        }

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        var imageBytes = ms.ToArray();

        var text = await Task.Run(() =>
        {
            _semaphore.Wait(ct);
            try
            {
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = _engine.Process(pix);
                return page.GetText();
            }
            finally
            {
                _semaphore.Release();
            }
        }, ct);

        var expiryDate = ExtractDate(text, ExpiryPattern());
        var issueDate = ExtractDate(text, IssueDatePattern());

        _logger.LogDebug("OCR extracted {Length} chars, expiry={Expiry}, issue={Issue}",
            text.Length, expiryDate, issueDate);

        return new OcrResult(text, expiryDate, issueDate);
    }

    private static DateTime? ExtractDate(string text, Regex pattern)
    {
        var match = pattern.Match(text);
        if (!match.Success) return null;

        var raw = match.Groups[1].Value.Trim();

        if (DateTime.TryParseExact(raw, DateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return parsed;

        return null;
    }

    [GeneratedRegex(
        @"(?:expir(?:y|es|ation)(?:\s+(?:date|on))?|valid\s+(?:till|until|thru|through)|exp\.?\s*date|best\s+before)\s*[:\-]?\s*(\d{1,2}[\/.\-]\d{1,2}[\/.\-]\d{2,4}|\d{4}[\/.\-]\d{1,2}[\/.\-]\d{1,2}|\w+\s+\d{1,2},?\s+\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ExpiryPattern();

    [GeneratedRegex(
        @"(?:issue\s*date|issued\s+on|date\s+of\s+issue)\s*[:\-]?\s*(\d{1,2}[\/.\-]\d{1,2}[\/.\-]\d{2,4}|\d{4}[\/.\-]\d{1,2}[\/.\-]\d{1,2}|\w+\s+\d{1,2},?\s+\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex IssueDatePattern();

    public void Dispose()
    {
        _semaphore.Dispose();
        _engine.Dispose();
    }
}
