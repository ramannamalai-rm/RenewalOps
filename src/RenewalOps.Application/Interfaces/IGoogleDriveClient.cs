namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Thin wrapper over Google Drive operations used by the sync job. Implemented in
/// Infrastructure with the Drive v3 SDK; faked in tests.
/// </summary>
public interface IGoogleDriveClient
{
    /// <summary>
    /// Uploads a file into the user's "RenewalOps" Drive folder (creating the folder if
    /// needed) and returns the created Drive file id.
    /// </summary>
    Task<string> UploadToRenewalOpsFolderAsync(
        Guid userId, string fileName, string contentType, Stream content, CancellationToken ct = default);
}
