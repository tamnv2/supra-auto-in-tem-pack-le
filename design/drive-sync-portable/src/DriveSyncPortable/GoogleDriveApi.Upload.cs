using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DriveSyncPortable;

internal sealed partial class GoogleDriveApi
{
    private const int UploadChunkSize = 8 * 1024 * 1024; // 32 × 256 KiB.

    private static Uri VerifiedSessionUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || (uri.Host != "www.googleapis.com" && uri.Host != "upload.googleapis.com"))
            throw new IOException("Google trả về địa chỉ phiên tải lên không hợp lệ.");
        return uri;
    }

    private async Task<HttpResponseMessage> SendUploadSessionAsync(
        Uri sessionUri, HttpContent content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, sessionUri)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", await AccessTokenAsync(ct));
        Usage.RecordRequest(HttpMethod.Put, sessionUri.ToString(), sessionChunk: true);
        return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static long NextOffset(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Range", out var values))
            return 0;
        string text = values.FirstOrDefault() ?? "";
        int dash = text.LastIndexOf('-');
        return dash >= 0 && long.TryParse(text[(dash + 1)..], out long last)
            ? last + 1 : 0;
    }

    private async Task<DriveItem> ReadFinishedUploadAsync(
        HttpResponseMessage response, long bytes, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var completed = ReadItem(json.RootElement);
        _store.SetPendingUploadSession(null);
        Usage.RecordTransfer(TransferDirection.Upload, bytes);
        return completed;
    }

    private async Task<DriveItem> UploadResumableAsync(
        string localPath, string name, string parentId,
        string? existingId, CancellationToken ct)
    {
        var info = new FileInfo(localPath);
        if (!info.Exists || info.Length <= 0)
            throw new IOException("Không tìm thấy file có nội dung để tải.");
        long length = info.Length;
        string fingerprint = length + "|" + info.LastWriteTimeUtc.Ticks;
        string absolute = Path.GetFullPath(localPath);
        string remote = existingId ?? "";
        PendingUploadSession? checkpoint = _store.GetPendingUploadSession();

        bool same = checkpoint is not null
            && checkpoint.LocalPath.Equals(absolute, StringComparison.OrdinalIgnoreCase)
            && checkpoint.Fingerprint == fingerprint
            && checkpoint.ParentId == parentId
            && checkpoint.FileName == name
            && checkpoint.RemoteId == remote;
        if (!same && checkpoint is not null)
        {
            _store.SetPendingUploadSession(null);
            checkpoint = null;
        }

        Uri? sessionUri = same ? VerifiedSessionUri(checkpoint!.SessionUrl) : null;
        long offset = 0;
        if (sessionUri is not null)
        {
            using var statusPayload = new ByteArrayContent(Array.Empty<byte>());
            statusPayload.Headers.ContentRange = new ContentRangeHeaderValue(length);
            using var status = await SendUploadSessionAsync(sessionUri, statusPayload, ct);
            if (status.IsSuccessStatusCode)
                return await ReadFinishedUploadAsync(status, length, ct);
            if (status.StatusCode == HttpStatusCode.NotFound)
            {
                _store.SetPendingUploadSession(null);
                sessionUri = null;
            }
            else if ((int)status.StatusCode == 308)
                offset = NextOffset(status);
            else
                throw new HttpRequestException(
                    "Google không thể tiếp tục Upload: HTTP " + (int)status.StatusCode,
                    null, status.StatusCode);
        }

        if (sessionUri is null)
        {
            string endpoint = "https://www.googleapis.com/upload/drive/v3/files"
                + (string.IsNullOrEmpty(existingId) ? "" : "/" + Param(existingId))
                + "?uploadType=resumable&fields=" + Param(ItemFields);
            using HttpContent initial = new StringContent(
                JsonSerializer.Serialize(existingId is null
                    ? (object)new { name, parents = new[] { parentId } }
                    : new { name }),
                System.Text.Encoding.UTF8, "application/json");
            using var started = await StartResumableAsync(endpoint,
                existingId is null ? HttpMethod.Post : HttpMethod.Patch,
                initial, length, ct);
            sessionUri = VerifiedSessionUri(started.Headers.Location?.ToString() ?? "");
            _store.SetPendingUploadSession(new PendingUploadSession(
                absolute, fingerprint, parentId, name, remote, sessionUri.ToString()));
        }

        var meter = new TransferMeter(TransferChanged, TransferDirection.Upload, name, length);
        try
        {
            await using var file = new FileStream(absolute, FileMode.Open, FileAccess.Read,
                FileShare.Read, 1024 * 1024, true);
            while (offset < length)
            {
                ct.ThrowIfCancellationRequested();
                if (new FileInfo(absolute).Length != length
                    || File.GetLastWriteTimeUtc(absolute).Ticks != info.LastWriteTimeUtc.Ticks)
                    throw new IOException("File đã thay đổi khi Upload; cần đồng bộ lại phiên bản mới.");

                file.Position = offset;
                int size = checked((int)Math.Min(UploadChunkSize, length - offset));
                byte[] chunk = new byte[size];
                await file.ReadExactlyAsync(chunk.AsMemory(), ct);
                using var chunkStream = new MemoryStream(chunk, writable: false);
                using var metered = new MeteredReadStream(chunkStream, meter, _uploadLimiter);
                using var content = new StreamContent(metered);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentLength = size;
                content.Headers.ContentRange = new ContentRangeHeaderValue(
                    offset, offset + size - 1, length);
                using var response = await SendUploadSessionAsync(sessionUri, content, ct);
                if (response.IsSuccessStatusCode)
                    return await ReadFinishedUploadAsync(response, length, ct);
                if ((int)response.StatusCode == 308)
                {
                    long next = NextOffset(response);
                    if (next < offset || next > length)
                        throw new IOException("Google trả sai vị trí tiếp tục Upload.");
                    offset = next;
                    if (response.Headers.Location is not null)
                    {
                        sessionUri = VerifiedSessionUri(response.Headers.Location.ToString());
                        _store.SetPendingUploadSession(new PendingUploadSession(
                            absolute, fingerprint, parentId, name, remote, sessionUri.ToString()));
                    }
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.NotFound)
                    _store.SetPendingUploadSession(null); // Expired; next run starts safely.
                throw new HttpRequestException(
                    "Upload tạm dừng: Google HTTP " + (int)response.StatusCode,
                    null, response.StatusCode);
            }
            throw new IOException("Upload chưa trả metadata xác nhận hoàn thành.");
        }
        finally { meter.Finish(); }
    }
}
