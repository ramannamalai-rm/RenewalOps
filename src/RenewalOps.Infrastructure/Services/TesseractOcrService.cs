using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using Tesseract;

namespace RenewalOps.Infrastructure.Services;

public sealed partial class TesseractOcrService : IOcrService, IDisposable
{
    private readonly Lazy<TesseractEngine> _engine;
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
        // Construct lazily: the native engine is expensive and can fail if native libs are
        // missing. Deferring avoids throwing from the DI container / job activator and lets
        // ExtractTextAsync degrade gracefully to an empty result.
        _engine = new Lazy<TesseractEngine>(
            () => new TesseractEngine(tessdataPath, "eng", EngineMode.Default),
            LazyThreadSafetyMode.ExecutionAndPublication);
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

        string text;
        try
        {
            text = await Task.Run(() =>
            {
                _semaphore.Wait(ct);
                try
                {
                    using var pix = Pix.LoadFromMemory(imageBytes);
                    using var page = _engine.Value.Process(pix);
                    return page.GetText();
                }
                finally
                {
                    _semaphore.Release();
                }
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bad image or a native-interop failure must not poison the job queue.
            _logger.LogError(ex, "OCR failed for content type {ContentType}; returning empty result", contentType);
            return new OcrResult(string.Empty, null, null);
        }

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

        // Return UTC-kind dates: these persist to a Postgres 'timestamp with time zone'
        // column, and Npgsql rejects DateTimes with Kind=Unspecified. Detected values are
        // date-only, so treating them as UTC midnight is correct.
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (DateTime.TryParseExact(raw, DateFormats,
                CultureInfo.InvariantCulture, styles, out var parsed))
            return parsed;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, styles, out parsed))
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
        if (_engine.IsValueCreated)
            _engine.Value.Dispose();
    }
}
