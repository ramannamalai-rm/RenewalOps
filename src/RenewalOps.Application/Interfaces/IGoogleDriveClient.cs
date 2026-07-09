namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Thin wrapper over Google Drive operations used by the sync job. Implemented in
/// Infrastructure with the Drive v3 SDK; faked in tests.
/// </summary>
public interface IGoogleDriveClient
{
    /// <summary>
    /// Uploads a file into the user's "RenewalOps" Drive folder (creating the folder if
    /// needed) and returns the Drive file id. Idempotent: the file is stamped with the
    /// document id, and a re-run reuses the existing file (found by that marker) instead of
    /// creating a duplicate — even if the id was never persisted locally.
    /// </summary>
    Task<string> UploadToRenewalOpsFolderAsync(
        Guid userId, Guid documentId, string fileName, string contentType, Stream content, CancellationToken ct = default);
}
