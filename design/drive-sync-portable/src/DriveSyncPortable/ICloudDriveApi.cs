namespace DriveSyncPortable;

internal interface ICloudDriveApi
{
    bool IsSignedIn { get; }
    Action<TransferProgress>? TransferChanged { get; set; }
    Task<string> GetStartPageTokenAsync(CancellationToken ct);
    Task<(bool changed, string nextToken)> ReadChangesAsync(string pageToken, CancellationToken ct);
    Task<List<DriveItem>> ListChildrenAsync(string parentId, CancellationToken ct);
    Task<DriveItem> CreateFolderAsync(string name, string parent, CancellationToken ct);
    Task<DriveItem> UploadAsync(string localPath, string name, string parentId,
                               string? existingId, CancellationToken ct);
    Task DownloadAsync(string remoteId, string destination, string expectedLocalState,
                       string remoteMd5, CancellationToken ct);
    Task TrashAsync(string remoteId, CancellationToken ct);
}
