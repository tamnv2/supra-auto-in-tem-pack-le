using System.Net;
using System.Text.Json;

namespace DriveSyncPortable;

internal sealed record DriveRevision(string Id, string ModifiedTime, long Size);

internal sealed partial class GoogleDriveApi
{
    // The selected root is a code-enforced boundary even though Google OAuth
    // itself grants broader access. Never use file IDs from outside that root.
    internal async Task<DriveItem> AssertFileInSelectedRootAsync(
        string fileId, string rootId, CancellationToken ct)
    {
        SyncScopeGuard.ValidateRemoteRoot(rootId);
        var file = await GetItemAsync(fileId, ct);
        if (file.Trashed || file.Folder || file.Parents.Length != 1)
            throw new IOException("File không hợp lệ hoặc đã bị chuyển vị trí.");
        string parent = file.Parents[0];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int depth = 0; depth <= 32; depth++)
        {
            if (parent == rootId) return file;
            if (!seen.Add(parent)) break;
            var folder = await GetItemAsync(parent, ct);
            if (!folder.Folder || folder.Trashed || folder.Parents.Length != 1) break;
            parent = folder.Parents[0];
        }
        throw new UnauthorizedAccessException(
            "File không thuộc thư mục Google Drive đã được thiết lập.");
    }

    internal async Task<List<DriveRevision>> ListRevisionsInScopeAsync(
        string fileId, string rootId, CancellationToken ct)
    {
        DriveItem item = await AssertFileInSelectedRootAsync(fileId, rootId, ct);
        if (item.NativeGoogleFile) throw new NotSupportedException(
            "Bản sửa Google Docs/Sheets cần xem trên website.");
        string url = "https://www.googleapis.com/drive/v3/files/"
            + Param(fileId) + "/revisions?fields="
            + Param("nextPageToken,revisions(id,modifiedTime,size)")
            + "&pageSize=100";
        using var json = await JsonAsync(HttpMethod.Get, url, null, ct);
        return json.RootElement.GetProperty("revisions").EnumerateArray()
            .Select(x => new DriveRevision(GetString(x, "id"),
                GetString(x, "modifiedTime"), GetLong(x, "size")))
            .Where(x => x.Id.Length > 0).ToList();
    }

    internal async Task DownloadRevisionCopyAsync(string fileId, string revisionId,
        string rootId, string localRoot, string target, CancellationToken ct)
    {
        SyncScopeGuard.ResolveExistingFile(localRoot, target);
        if (File.Exists(target)) throw new IOException("Tên bản sao đã tồn tại.");
        var item = await AssertFileInSelectedRootAsync(fileId, rootId, ct);
        if (item.NativeGoogleFile)
            throw new NotSupportedException("Không tải revision trực tiếp của tài liệu Google gốc.");
        string url = "https://www.googleapis.com/drive/v3/files/"
            + Param(fileId) + "/revisions/" + Param(revisionId) + "?alt=media";
        string tmp = SyncScopeGuard.ResolveExistingFile(localRoot, target + ".drivesync-partial");
        try
        {
            using var response = await SendAsync(HttpMethod.Get, url, null, ct);
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using (var output = new FileStream(tmp, FileMode.Create,
                FileAccess.Write, FileShare.None, 1024 * 1024, true))
                await input.CopyToAsync(output, ct);
            SyncScopeGuard.ResolveExistingFile(localRoot, target);
            File.Move(tmp, target, overwrite: false);
            Usage.RecordTransfer(TransferDirection.Download, new FileInfo(target).Length);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}
