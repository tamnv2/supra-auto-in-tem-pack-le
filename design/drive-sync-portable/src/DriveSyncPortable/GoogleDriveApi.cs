using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriveSyncPortable;

internal sealed record DriveItem(
    string Id, string Name, string MimeType, string ModifiedTime, string Md5, long Size, string[] Parents, bool Trashed = false)
{
    public bool Folder => MimeType == "application/vnd.google-apps.folder";
    public bool NativeGoogleFile => MimeType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal)
                                    && !Folder;
    public string Version => string.IsNullOrEmpty(Md5)
        ? "time:" + ModifiedTime + "|" + Size : "md5:" + Md5 + "|" + Size;
}

internal sealed partial class GoogleDriveApi : ICloudDriveApi, IDisposable
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(30) };
    private readonly SettingsStore _store;
    public UsageTracker Usage { get; }
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private readonly BandwidthLimiter _uploadLimiter;
    private readonly BandwidthLimiter _downloadLimiter;
    public GoogleDriveApi(SettingsStore store)
    {
        _store = store;
        Usage = new UsageTracker(store.StorageFolder);
        _uploadLimiter = new BandwidthLimiter(() => _store.Data.UploadLimitKiBps);
        _downloadLimiter = new BandwidthLimiter(() => _store.Data.DownloadLimitKiBps);
    }

    public bool IsSignedIn => _store.GetToken()?.RefreshToken?.Length > 0;
    public Action<TransferProgress>? TransferChanged { get; set; }

    private static string Param(string value) => Uri.EscapeDataString(value);
    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";

    private static long GetLong(JsonElement element, string property)
        => element.TryGetProperty(property, out var field) && long.TryParse(field.ToString(), out var number) ? number : 0;

    private static DriveItem ReadItem(JsonElement e)
    {
        var parents = e.TryGetProperty("parents", out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : Array.Empty<string>();
        return new DriveItem(GetString(e, "id"), GetString(e, "name"), GetString(e, "mimeType"),
            GetString(e, "modifiedTime"), GetString(e, "md5Checksum"), GetLong(e, "size"), parents,
            e.TryGetProperty("trashed", out var trashed) && trashed.ValueKind == JsonValueKind.True);
    }

    private static string ItemFields = "id,name,mimeType,modifiedTime,md5Checksum,size,parents,trashed";

    public async Task SignInAsync(CancellationToken cancel)
    {
        string clientId = _store.Data.ClientId.Trim();
        if (!clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cần Client ID loại Desktop app của Google.");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string redirect = $"http://127.0.0.1:{port}/callback/";
        byte[] verifierBytes = RandomNumberGenerator.GetBytes(32);
        string verifier = Base64Url(verifierBytes);
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = Base64Url(RandomNumberGenerator.GetBytes(24));
        string auth = "https://accounts.google.com/o/oauth2/v2/auth"
            + "?response_type=code&client_id=" + Param(clientId)
            + "&redirect_uri=" + Param(redirect)
            + "&scope=" + Param("https://www.googleapis.com/auth/drive")
            + "&access_type=offline&prompt=consent"
            + "&code_challenge_method=S256&code_challenge=" + Param(challenge)
            + "&state=" + Param(state);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(TimeSpan.FromMinutes(5));
        _ = Process.Start(new ProcessStartInfo(auth) { UseShellExecute = true })
            ?? throw new InvalidOperationException("Không mở được trình duyệt để xác thực Google.");

        using var connection = await listener.AcceptTcpClientAsync(limit.Token);
        using var network = connection.GetStream();
        using var reader = new StreamReader(network, Encoding.ASCII, false, 1024, leaveOpen: true);
        string line = await reader.ReadLineAsync(limit.Token) ?? "";
        var segments = line.Split(' ');
        if (segments.Length < 2 || !segments[1].StartsWith("/callback/", StringComparison.Ordinal))
            throw new InvalidOperationException("OAuth callback không hợp lệ.");
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        string rawQuery = new Uri("http://localhost" + segments[1]).Query.TrimStart('?');
        foreach (var part in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            parameters[Uri.UnescapeDataString(kv[0])] = kv.Length == 2 ? Uri.UnescapeDataString(kv[1].Replace("+", " ")) : "";
        }
        bool validState = parameters.TryGetValue("state", out var actualState)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(actualState));
        bool hasCode = parameters.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code);
        var responseHtml = validState && hasCode
            ? "<h2>Đã nhận xác thực Google</h2><p>Quay lại ứng dụng DriveSync Portable để hoàn tất.</p>"
            : "<h2>Đăng nhập chưa hoàn tất</h2><p>Quay lại DriveSync Portable và thử lại.</p>";
        byte[] htmlBytes = Encoding.UTF8.GetBytes("<!doctype html><html><meta charset='utf-8'><body style='font:16px Segoe UI;padding:24px'>" + responseHtml + "</body></html>");
        byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + htmlBytes.Length + "\r\nConnection: close\r\n\r\n");
        await network.WriteAsync(headers, limit.Token);
        await network.WriteAsync(htmlBytes, limit.Token);
        await network.FlushAsync(limit.Token);
        if (!validState || !hasCode)
            throw new InvalidOperationException("Không xác minh được trạng thái OAuth hoặc Google chưa cấp quyền.");

        var fields = new Dictionary<string, string> {
            ["client_id"] = clientId,
            ["code"] = code!,
            ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier,
            ["grant_type"] = "authorization_code"
        };
        string secret = _store.GetClientSecret();
        if (!string.IsNullOrWhiteSpace(secret)) fields["client_secret"] = secret;
        OAuthToken fresh = await RequestTokenAsync(fields, limit.Token);
        if (string.IsNullOrEmpty(fresh.RefreshToken))
            throw new InvalidOperationException("Google không trả refresh token. Cần cấp lại quyền truy cập offline.");
        _store.SetToken(fresh);
        _store.Data.GoogleEmail = await ReadEmailAsync(limit.Token);
        _store.Data.ChangeToken = "";
        _store.Save();
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<OAuthToken> RequestTokenAsync(Dictionary<string, string> fields, CancellationToken ct)
    {
        using var response = await Client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(fields), ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google OAuth từ chối mã xác thực hoặc token. HTTP " + (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        return new OAuthToken {
            AccessToken = GetString(root, "access_token"),
            RefreshToken = GetString(root, "refresh_token"),
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, GetLong(root, "expires_in") - 90))
        };
    }

    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        await _tokenGate.WaitAsync(ct);
        try
        {
            var token = _store.GetToken() ?? throw new InvalidOperationException("Chưa đăng nhập Google.");
            if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1) && !string.IsNullOrEmpty(token.AccessToken))
                return token.AccessToken;
            var fields = new Dictionary<string, string> {
                ["client_id"] = _store.Data.ClientId,
                ["refresh_token"] = token.RefreshToken,
                ["grant_type"] = "refresh_token"
            };
            string secret = _store.GetClientSecret();
            if (!string.IsNullOrWhiteSpace(secret)) fields["client_secret"] = secret;
            var renewed = await RequestTokenAsync(fields, ct);
            renewed.RefreshToken = token.RefreshToken;
            _store.SetToken(renewed);
            return renewed.AccessToken;
        }
        finally { _tokenGate.Release(); }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct, long? rangeStart = null)
    {
        // Only idempotent reads are retried here; never blindly retry a write.
        int maxAttempts = method == HttpMethod.Get ? 3 : 1;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var token = await AccessTokenAsync(ct);
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (rangeStart is > 0) request.Headers.Range = new RangeHeaderValue(rangeStart.Value, null);
            if (content is not null) request.Content = content;
            Usage.RecordRequest(method, url,
                method == HttpMethod.Put && url.Contains("upload_id=", StringComparison.OrdinalIgnoreCase));
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode) return response;
            bool retry = method == HttpMethod.Get && attempt + 1 < maxAttempts
                && (response.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500);
            int code = (int)response.StatusCode;
            if (!retry)
            {
                response.Dispose();
                throw new HttpRequestException("Google Drive API HTTP " + code, null, (HttpStatusCode)code);
            }
            response.Dispose();
            int delay = Math.Min(8_000, 1_000 * (1 << attempt)) + Random.Shared.Next(0, 600);
            await Task.Delay(delay, ct);
        }
        throw new HttpRequestException("Google Drive API hết số lần thử lại.");
    }


    private async Task<JsonDocument> JsonAsync(HttpMethod method, string url, object? data, CancellationToken ct)
    {
        HttpContent? payload = data is null ? null : new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
        using var response = await SendAsync(method, url, payload, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public static (string Extension, string MimeType)? NativeExportFormat(DriveItem item)
        => item.MimeType switch {
            "application/vnd.google-apps.document" =>
                (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            "application/vnd.google-apps.spreadsheet" =>
                (".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            "application/vnd.google-apps.presentation" =>
                (".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
            "application/vnd.google-apps.drawing" => (".pdf", "application/pdf"),
            _ => null
        };

    // Manual export only. Native Docs are not silently converted during sync.
    public async Task ExportNativeAsync(DriveItem item, string destination, CancellationToken ct)
    {
        var format = NativeExportFormat(item)
            ?? throw new NotSupportedException("Loại tài liệu Google không hỗ trợ xuất Office/PDF.");
        string url = "https://www.googleapis.com/drive/v3/files/" + Param(item.Id)
            + "/export?mimeType=" + Param(format.MimeType);
        using var response = await SendAsync(HttpMethod.Get, url, null, ct);
        string temp = destination + ".drivesync-partial";
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write,
                FileShare.None, 1024 * 1024, true))
                await input.CopyToAsync(output, ct);
            File.Move(temp, destination, true);
            Usage.RecordTransfer(TransferDirection.Download, new FileInfo(destination).Length);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public async Task<DriveStorageQuota> ReadStorageQuotaAsync(CancellationToken ct)
    {
        using var json = await JsonAsync(HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/about?fields="
            + Param("storageQuota(limit,usage,usageInDrive)"), null, ct);
        var quota = json.RootElement.GetProperty("storageQuota");
        string limitText = GetString(quota, "limit");
        return new DriveStorageQuota(
            long.TryParse(limitText, out long limit) ? limit : null,
            GetLong(quota, "usage"), GetLong(quota, "usageInDrive"));
    }

    public async Task<string> ReadEmailAsync(CancellationToken ct)
    {
        using var json = await JsonAsync(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)", null, ct);
        var user = json.RootElement.GetProperty("user");
        return GetString(user, "emailAddress");
    }

    // User-triggered search only under the selected remote folder.
    public async Task<List<DriveItem>> SearchByNameAsync(string term,
        string selectedRootId, CancellationToken ct)
    {
        SyncScopeGuard.ValidateRemoteRoot(selectedRootId);
        string query = term.Trim();
        if (query.Length < 2 || query.Length > 100)
            throw new ArgumentException("Từ khóa phải có 2-100 ký tự.");
        var hits = new List<DriveItem>();
        var pending = new Queue<(string Id, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue((selectedRootId, 0));
        while (pending.Count > 0 && hits.Count < 100)
        {
            ct.ThrowIfCancellationRequested();
            var (id, depth) = pending.Dequeue();
            if (!visited.Add(id)) continue;
            if (visited.Count > 250)
                throw new InvalidOperationException("Tìm kiếm vượt 250 thư mục; dừng để bảo vệ quota.");
            foreach (var item in await ListChildrenAsync(id, ct))
            {
                if (item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    hits.Add(item);
                if (hits.Count >= 100) break;
                if (item.Folder && depth < 32) pending.Enqueue((item.Id, depth + 1));
            }
        }
        return hits;
    }

    public async Task<DriveItem> GetItemAsync(string id, CancellationToken ct)
    {
        using var json = await JsonAsync(HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/files/" + Param(id)
                + "?fields=" + Param(ItemFields), null, ct);
        return ReadItem(json.RootElement);
    }

    public async Task<List<DriveItem>> ListChildrenAsync(string parentId, CancellationToken ct)
    {
        var list = new List<DriveItem>();
        string next = "";
        do
        {
            string q = "'" + parentId.Replace("\\", "\\\\").Replace("'", "\\'") + "' in parents and trashed = false";
            string uri = "https://www.googleapis.com/drive/v3/files?q=" + Param(q)
                + "&fields=" + Param("nextPageToken,files(" + ItemFields + ")")
                + "&pageSize=1000"
                + (string.IsNullOrEmpty(next) ? "" : "&pageToken=" + Param(next));
            using var json = await JsonAsync(HttpMethod.Get, uri, null, ct);
            foreach (var file in json.RootElement.GetProperty("files").EnumerateArray())
                list.Add(ReadItem(file));
            next = GetString(json.RootElement, "nextPageToken");
        } while (!string.IsNullOrEmpty(next));
        return list;
    }

    public async Task<DriveItem> CreateFolderAsync(string name, string parent, CancellationToken ct)
    {
        using var json = await JsonAsync(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=" + Param(ItemFields),
            new { name, mimeType = "application/vnd.google-apps.folder", parents = new[] { parent } }, ct);
        return ReadItem(json.RootElement);
    }

    public async Task<string> GetStartPageTokenAsync(CancellationToken ct)
    {
        using var json = await JsonAsync(HttpMethod.Get, "https://www.googleapis.com/drive/v3/changes/startPageToken?fields=startPageToken", null, ct);
        return GetString(json.RootElement, "startPageToken");
    }

    public async Task<(bool changed, string nextToken)> ReadChangesAsync(
        string pageToken, IReadOnlyCollection<string> trackedIds, CancellationToken ct)
    {
        bool changes = false;
        var tracked = new HashSet<string>(trackedIds, StringComparer.Ordinal);
        string token = pageToken;
        while (true)
        {
            string url = "https://www.googleapis.com/drive/v3/changes?pageToken=" + Param(token)
                + "&fields=" + Param("nextPageToken,newStartPageToken,changes(fileId,removed,file(id,parents,trashed))")
                + "&pageSize=1000&includeRemoved=true";
            using var json = await JsonAsync(HttpMethod.Get, url, null, ct);
            if (json.RootElement.TryGetProperty("changes", out var items))
            {
                foreach (var change in items.EnumerateArray())
                {
                    string id = GetString(change, "fileId");
                    if (tracked.Contains(id))
                    {
                        changes = true;
                        continue;
                    }
                    // Newly created/moved files inside a tracked folder must trigger reconciliation.
                    if (change.TryGetProperty("file", out var file)
                        && file.ValueKind == JsonValueKind.Object
                        && file.TryGetProperty("parents", out var parents)
                        && parents.ValueKind == JsonValueKind.Array
                        && parents.EnumerateArray().Any(p => tracked.Contains(p.GetString() ?? "")))
                        changes = true;
                }
            }
            string page = GetString(json.RootElement, "nextPageToken");
            if (!string.IsNullOrEmpty(page))
            {
                token = page;
                continue;
            }
            return (changes, GetString(json.RootElement, "newStartPageToken") is { Length: > 0 } next ? next : token);
        }
    }

    public async Task<DriveItem> UploadAsync(string localPath, string name, string parentId, string? existingId, CancellationToken ct)
    {
        var info = new FileInfo(localPath);
        if (!info.Exists) throw new IOException("Tệp nguồn không tồn tại: " + info.Name);
        if (info.Length > 0)
            return await UploadResumableAsync(localPath, name, parentId, existingId, ct);
        string endpoint = "https://www.googleapis.com/upload/drive/v3/files";
        bool replacing = !string.IsNullOrEmpty(existingId);
        if (replacing) endpoint += "/" + Param(existingId!);
        endpoint += "?uploadType=resumable&fields=" + Param(ItemFields);
        HttpContent body = new StringContent(
            JsonSerializer.Serialize(replacing
                ? new { name } as object
                : new { name, parents = new[] { parentId } }),
            Encoding.UTF8, "application/json");
        using var response = await StartResumableAsync(endpoint, replacing ? HttpMethod.Patch : HttpMethod.Post,
            body, info.Length, ct);
        Uri location = response.Headers.Location
            ?? throw new InvalidOperationException("Google không cung cấp URL upload.");
        await using var file = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        var meter = new TransferMeter(TransferChanged, TransferDirection.Upload, name, info.Length);
        try
        {
            using var meteredFile = new MeteredReadStream(file, meter, _uploadLimiter);
            using var payload = new StreamContent(meteredFile, 1024 * 1024);
            payload.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            payload.Headers.ContentLength = info.Length;
            using var upload = await SendAsync(HttpMethod.Put, location.ToString(), payload, ct);
            await using var stream = await upload.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            Usage.RecordTransfer(TransferDirection.Upload, info.Length);
            return ReadItem(json.RootElement);
        }
        finally { meter.Finish(); }
    }

    private async Task<HttpResponseMessage> StartResumableAsync(string endpoint, HttpMethod method, HttpContent body, long length, CancellationToken ct)
    {
        var token = await AccessTokenAsync(ct);
        using var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Upload-Content-Type", "application/octet-stream");
        request.Headers.TryAddWithoutValidation("X-Upload-Content-Length", length.ToString());
        request.Content = body;
        Usage.RecordRequest(method, endpoint);
        var response = await Client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return response;
        var code = (int)response.StatusCode;
        response.Dispose();
        throw new HttpRequestException("Không khởi tạo được upload Google Drive: HTTP " + code);
    }

    public async Task DownloadAsync(string remoteId, string destination, string expectedLocalState,
                                    string remoteMd5, CancellationToken ct)
    {
        string temp = destination + ".drivesync-partial";
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        long resumeOffset = File.Exists(temp) ? new FileInfo(temp).Length : 0;
        string endpoint = "https://www.googleapis.com/drive/v3/files/" + Param(remoteId) + "?alt=media";
        HttpResponseMessage result;
        try
        {
            result = await SendAsync(HttpMethod.Get, endpoint,
                null, ct, resumeOffset > 0 ? resumeOffset : null);
        }
        catch (HttpRequestException ex) when (
            resumeOffset > 0 && ex.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // A completed or stale partial may be longer than the current remote file.
            File.Delete(temp);
            resumeOffset = 0;
            result = await SendAsync(HttpMethod.Get, endpoint, null, ct);
        }
        using var response = result;

        // Some servers ignore Range. Start clean in that case, never append a full response.
        bool append = resumeOffset > 0 && response.StatusCode == HttpStatusCode.PartialContent
            && response.Content.Headers.ContentRange?.From == resumeOffset;
        long incoming = response.Content.Headers.ContentLength ?? 0;
        try
        {
            string diskRoot = Path.GetPathRoot(Path.GetFullPath(destination)) ?? "";
            if (!string.IsNullOrWhiteSpace(diskRoot) && incoming > 0
                && new DriveInfo(diskRoot).AvailableFreeSpace < incoming)
                throw new IOException("Ổ đĩa không đủ chỗ để tải file.");
        }
        catch (ArgumentException) { /* Unknown drive; let FileStream report an error. */ }

        var meter = new TransferMeter(TransferChanged, TransferDirection.Download,
            Path.GetFileName(destination), incoming);
        try
        {
            await using var remote = await response.Content.ReadAsStreamAsync(ct);
            await using var local = new FileStream(temp, append ? FileMode.Append : FileMode.Create,
                FileAccess.Write, FileShare.None, 1024 * 1024, true);
            byte[] buffer = new byte[256 * 1024];
            int count;
            while ((count = await remote.ReadAsync(buffer, ct)) > 0)
            {
                await _downloadLimiter.ThrottleAsync(count, ct);
                await local.WriteAsync(buffer.AsMemory(0, count), ct);
                meter.Add(count);
            }
            await local.FlushAsync(ct);
        }
        finally { meter.Finish(); }

        if (!string.IsNullOrEmpty(remoteMd5))
        {
            await using var verifyStream = new FileStream(temp, FileMode.Open, FileAccess.Read,
                FileShare.Read, 1024 * 1024, true);
            string receivedMd5 = Convert.ToHexString(
                await MD5.HashDataAsync(verifyStream, ct)).ToLowerInvariant();
            if (!receivedMd5.Equals(remoteMd5, StringComparison.OrdinalIgnoreCase))
            {
                verifyStream.Close();
                File.Delete(temp);
                throw new IOException("Checksum tải về sai; đã xóa file tạm để tải lại từ đầu.");
            }
        }

        string actualLocalState = "";
        if (File.Exists(destination))
        {
            var current = new FileInfo(destination);
            actualLocalState = current.Length + "|" + current.LastWriteTimeUtc.Ticks;
        }
        if (expectedLocalState != actualLocalState)
            throw new IOException("File máy tính đã thay đổi khi tải; không ghi đè.");

        File.Move(temp, destination, true);
        Usage.RecordTransfer(TransferDirection.Download, new FileInfo(destination).Length);
    }

    public async Task<DriveItem> RenameOrMoveAsync(string remoteId, string newName,
        string oldParentId, string newParentId, CancellationToken ct)
    {
        string query = "fields=" + Param(ItemFields);
        if (!string.Equals(oldParentId, newParentId, StringComparison.Ordinal))
            query += "&addParents=" + Param(newParentId)
                  + "&removeParents=" + Param(oldParentId);
        using var json = await JsonAsync(HttpMethod.Patch,
            "https://www.googleapis.com/drive/v3/files/" + Param(remoteId) + "?" + query,
            new { name = newName }, ct);
        return ReadItem(json.RootElement);
    }

    public async Task TrashAsync(string remoteId, CancellationToken ct)
    {
        using var json = await JsonAsync(HttpMethod.Patch, "https://www.googleapis.com/drive/v3/files/" + Param(remoteId) + "?fields=id",
            new { trashed = true }, ct);
    }

    public void Dispose() => _tokenGate.Dispose();
}
