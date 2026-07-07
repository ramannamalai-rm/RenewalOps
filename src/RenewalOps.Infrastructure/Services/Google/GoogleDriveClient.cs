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

    private readonly GoogleCredentialFactory _credentialFactory;
    private readonly ILogger<GoogleDriveClient> _logger;

    public GoogleDriveClient(GoogleCredentialFactory credentialFactory, ILogger<GoogleDriveClient> logger)
    {
        _credentialFactory = credentialFactory;
        _logger = logger;
    }

    public async Task<string> UploadToRenewalOpsFolderAsync(
        Guid userId, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var credential = await _credentialFactory.CreateAsync(userId, ct)
            ?? throw new InvalidOperationException($"No active Google connection for user {userId}.");

        using var drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        var folderId = await EnsureFolderAsync(drive, ct);

        var metadata = new DriveFile { Name = fileName, Parents = [folderId] };
        var request = drive.Files.Create(metadata, content, contentType);
        request.Fields = "id";

        var progress = await request.UploadAsync(ct);
        if (progress.Status != UploadStatus.Completed)
            throw new InvalidOperationException($"Drive upload failed: {progress.Exception?.Message}", progress.Exception);

        var fileId = request.ResponseBody.Id;
        _logger.LogInformation("Uploaded '{FileName}' to Drive folder {FolderId} as {FileId}", fileName, folderId, fileId);
        return fileId;
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
