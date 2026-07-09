using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace RenewalOps.Infrastructure.Services.Google;

public sealed class GoogleDriveClient : IGoogleDriveClient
{
    private const string FolderName = "RenewalOps";
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string ApplicationName = "RenewalOps";
    private const string DocumentIdProperty = "renewalOpsDocumentId";

    private readonly GoogleCredentialFactory _credentialFactory;
    private readonly ILogger<GoogleDriveClient> _logger;

    public GoogleDriveClient(GoogleCredentialFactory credentialFactory, ILogger<GoogleDriveClient> logger)
    {
        _credentialFactory = credentialFactory;
        _logger = logger;
    }

    public async Task<string> UploadToRenewalOpsFolderAsync(
        Guid userId, Guid documentId, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var credential = await _credentialFactory.CreateAsync(userId, ct)
            ?? throw new InvalidOperationException($"No active Google connection for user {userId}.");

        using var drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        // Idempotency: if a file already carries this document's marker, reuse it. This covers
        // the case where a prior run uploaded the file but crashed before persisting the id.
        var existingId = await FindExistingFileAsync(drive, documentId, ct);
        if (existingId is not null)
        {
            _logger.LogInformation("Drive file for document {DocId} already exists ({FileId}); reusing", documentId, existingId);
            return existingId;
        }

        var folderId = await EnsureFolderAsync(drive, ct);

        var metadata = new DriveFile
        {
            Name = fileName,
            Parents = [folderId],
            AppProperties = new Dictionary<string, string> { [DocumentIdProperty] = documentId.ToString("N") }
        };
        var request = drive.Files.Create(metadata, content, contentType);
        request.Fields = "id";

        var progress = await request.UploadAsync(ct);
        if (progress.Status != UploadStatus.Completed)
            throw new InvalidOperationException($"Drive upload failed: {progress.Exception?.Message}", progress.Exception);

        var fileId = request.ResponseBody.Id;
        _logger.LogInformation("Uploaded '{FileName}' to Drive folder {FolderId} as {FileId}", fileName, folderId, fileId);
        return fileId;
    }

    private static async Task<string?> FindExistingFileAsync(DriveService drive, Guid documentId, CancellationToken ct)
    {
        var list = drive.Files.List();
        list.Q = $"appProperties has {{ key='{DocumentIdProperty}' and value='{documentId:N}' }} and trashed=false";
        list.Fields = "files(id)";
        list.Spaces = "drive";
        var result = await list.ExecuteAsync(ct);
        return result.Files is { Count: > 0 } ? result.Files[0].Id : null;
    }

    private static async Task<string> EnsureFolderAsync(DriveService drive, CancellationToken ct)
    {
        var list = drive.Files.List();
        list.Q = $"mimeType='{FolderMimeType}' and name='{FolderName}' and trashed=false";
        list.Fields = "files(id)";
        list.Spaces = "drive";
        var existing = await list.ExecuteAsync(ct);
        if (existing.Files is { Count: > 0 })
            return existing.Files[0].Id;

        var folder = new DriveFile { Name = FolderName, MimeType = FolderMimeType };
        var create = drive.Files.Create(folder);
        create.Fields = "id";
        var created = await create.ExecuteAsync(ct);
        return created.Id;
    }
}
