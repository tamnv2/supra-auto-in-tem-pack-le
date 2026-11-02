using System.Collections.Concurrent;

namespace DriveSyncPortable;

internal sealed class SyncEngine : IDisposable
{
    private readonly SettingsStore _store;
    private readonly GoogleDriveApi _drive;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private CancellationTokenSource? _cancel;
    private Task? _loop;
    private FileSystemWatcher? _watcher;
    private int _dirty = 1;
    private Dictionary<string, DriveItem>? _remoteFiles;
    private Dictionary<string, string>? _remoteFolders;
    private DateTimeOffset _lastReconcile = DateTimeOffset.MinValue;

    public event Action<string>? Status;
    public event Action<string>? Activity;
    public bool IsRunning => _loop is { IsCompleted: false };
    public bool Paused { get; private set; }

    public SyncEngine(SettingsStore settings, GoogleDriveApi api)
    {
        _store = settings;
        _drive = api;
    }

    private void Report(string message) => Status?.Invoke(message);
    private void Log(string message) => Activity?.Invoke(message);

    public void Start()
    {
        if (IsRunning) return;
        if (!Directory.Exists(_store.Data.LocalFolder))
            throw new DirectoryNotFoundException("Thư mục laptop không tồn tại.");
        if (string.IsNullOrWhiteSpace(_store.Data.RemoteFolderId))
            throw new InvalidOperationException("Chưa chọn thư mục Google Drive.");
        if (!_drive.IsSignedIn)
            throw new InvalidOperationException("Chưa đăng nhập tài khoản Google.");

        _cancel = new CancellationTokenSource();
        Paused = false;
        Interlocked.Exchange(ref _dirty, 1);
        _watcher = new FileSystemWatcher(_store.Data.LocalFolder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += (_, _) => Signal();
        _loop = LoopAsync(_cancel.Token);
        Signal();
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        if (args.Name is not null && IsIgnored(args.Name.Replace('\\', '/'))) return;
        Signal();
    }
    private void OnRenamed(object sender, RenamedEventArgs args) => Signal();

    public void Pause(bool pause)
    {
        Paused = pause;
        if (!pause) Signal();
        Report(pause ? "Đã tạm dừng" : "Đang theo dõi file");
    }

    private void Signal()
    {
        Interlocked.Exchange(ref _dirty, 1);
        try { _wake.Release(); } catch (SemaphoreFullException) { }
    }

    public async Task StopAsync()
    {
        _cancel?.Cancel();
        if (_loop is not null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
        _watcher?.Dispose();
        _watcher = null;
        _cancel?.Dispose();
        _cancel = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!Paused)
            {
                try
                {
                    await RunOnceAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Report("Lỗi đồng bộ — đang chờ thử lại");
                    Log("Lỗi: " + SafeError(ex));
                }
            }
            using var tick = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var wake = _wake.WaitAsync(tick.Token);
            var delay = Task.Delay(TimeSpan.FromSeconds(60), tick.Token);
            try { await Task.WhenAny(wake, delay); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            tick.Cancel();
            try { await wake; } catch (OperationCanceledException) { }
            if (!Paused && Interlocked.CompareExchange(ref _dirty, 0, 0) == 1)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (Paused) return;
        if (!Directory.Exists(_store.Data.LocalFolder))
        {
            Report("Thư mục laptop không tồn tại — đồng bộ tạm dừng");
            return;
        }
        await _syncLock.WaitAsync(ct);
        try
        {
            Report("Đang kiểm tra thay đổi...");
            bool localDirty = Interlocked.Exchange(ref _dirty, 0) == 1;
            bool refresh = _remoteFiles is null
                        || _remoteFolders is null
                        || DateTimeOffset.UtcNow - _lastReconcile >= TimeSpan.FromHours(1);
            if (string.IsNullOrEmpty(_store.Data.ChangeToken))
            {
                _store.Data.ChangeToken = await _drive.GetStartPageTokenAsync(ct);
                _store.Save();
                refresh = true;
            }
            else
            {
                try
                {
                    var changes = await _drive.ReadChangesAsync(_store.Data.ChangeToken, ct);
                    // Commit change token only after successful reconciliation.
                    refresh |= changes.changed;
                    if (refresh)
                    {
                        await RefreshRemoteAsync(ct);
                        _store.Data.ChangeToken = changes.nextToken;
                        _store.Save();
                    }
                    else
                    {
                        _store.Data.ChangeToken = changes.nextToken;
                        _store.Save();
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    _store.Data.ChangeToken = await _drive.GetStartPageTokenAsync(ct);
                    refresh = true;
                }
            }

            if (refresh && _lastReconcile == DateTimeOffset.MinValue
                || refresh && _remoteFiles is null)
                await RefreshRemoteAsync(ct);
            if (_remoteFiles is null || _remoteFolders is null)
                await RefreshRemoteAsync(ct);

            if (!localDirty && !refresh)
            {
                Report("Đã đồng bộ · Không có thay đổi");
                return;
            }
            bool allSucceeded = await ReconcileAsync(ct);
            Report(allSucceeded ? "Đã đồng bộ · Đang theo dõi thay đổi"
                                : "Còn file chưa đồng bộ · Tự thử lại sau 60 giây");
        }
        finally { _syncLock.Release(); }
    }

    private async Task RefreshRemoteAsync(CancellationToken ct)
    {
        var files = new Dictionary<string, DriveItem>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { [""] = _store.Data.RemoteFolderId };
        var stack = new Stack<(string relative, string id, int depth)>();
        stack.Push(("", _store.Data.RemoteFolderId, 0));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (relative, id, depth) = stack.Pop();
            if (depth > 32) throw new InvalidOperationException("Cấu trúc thư mục quá sâu (trên 32 cấp).");
            if (!visited.Add(id)) continue;
            foreach (var item in await _drive.ListChildrenAsync(id, ct))
            {
                if (!IsSafeName(item.Name))
                {
                    Log("Bỏ qua tên file không phù hợp Windows: " + item.Name);
                    continue;
                }
                string path = string.IsNullOrEmpty(relative) ? item.Name : relative + "/" + item.Name;
                if (item.Folder)
                {
                    if (folders.ContainsKey(path) || files.ContainsKey(path))
                    {
                        Log("Phát hiện tên thư mục trùng nhau: " + path);
                        continue;
                    }
                    folders[path] = item.Id;
                    stack.Push((path, item.Id, depth + 1));
                }
                else if (item.NativeGoogleFile)
                    Log("Tài liệu Google trực tuyến (chưa hỗ trợ dạng file gốc): " + item.Name);
                else if (!files.TryAdd(path, item))
                    Log("Tên file trùng trong cùng thư mục: " + path);
            }
        }
        _remoteFiles = files;
        _remoteFolders = folders;
        _lastReconcile = DateTimeOffset.UtcNow;
    }

    private static bool IsSafeName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name != "." && name != ".."
            && name.TrimEnd(' ', '.') == name
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3",
                        "LPT1", "LPT2", "LPT3" }.Contains(
                Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsIgnored(string relative)
    {
        string name = relative.Split('/').Last();
        return name.StartsWith("~$", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".drivesync-partial", StringComparison.OrdinalIgnoreCase)
            || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
            || name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private string LocalPath(string relative)
    {
        var root = Path.GetFullPath(_store.Data.LocalFolder).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Đường dẫn vượt ngoài thư mục đồng bộ.");
        return path;
    }

    private static string LocalVersion(string path)
    {
        var info = new FileInfo(path);
        return info.Length + "|" + info.LastWriteTimeUtc.Ticks;
    }

    private Dictionary<string, string> ReadLocalFiles()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Stack<string>();
        directories.Push(_store.Data.LocalFolder);
        while (directories.Count > 0)
        {
            string folder = directories.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                string relative = Path.GetRelativePath(_store.Data.LocalFolder, entry).Replace('\\', '/');
                if (IsIgnored(relative)) continue;
                if ((attributes & FileAttributes.Directory) != 0) directories.Push(entry);
                else files[relative] = entry;
            }
        }
        return files;
    }

    private async Task<string> EnsureRemoteParentAsync(string relative, CancellationToken ct)
    {
        string[] segments = relative.Split('/');
        string current = "";
        for (int i = 0; i < segments.Length - 1; i++)
        {
            string child = string.IsNullOrEmpty(current) ? segments[i] : current + "/" + segments[i];
            if (!_remoteFolders!.ContainsKey(child))
            {
                var created = await _drive.CreateFolderAsync(segments[i],
                    _remoteFolders[current], ct);
                _remoteFolders[child] = created.Id;
            }
            current = child;
        }
        return _remoteFolders![current];
    }

    private async Task<bool> ReconcileAsync(CancellationToken ct)
    {
        bool allSucceeded = true;
        var local = ReadLocalFiles();
        var remote = _remoteFiles!;
        // Remote folders are created even if empty, without blindly removing local folders.
        foreach (var folder in _remoteFolders!.Keys.OrderBy(p => p.Count(c => c == '/')))
        {
            if (folder.Length == 0) continue;
            Directory.CreateDirectory(LocalPath(folder));
        }
        foreach (var pair in local)
            await EnsureRemoteParentAsync(pair.Key, ct);

        var paths = new HashSet<string>(local.Keys, StringComparer.OrdinalIgnoreCase);
        paths.UnionWith(remote.Keys);
        paths.UnionWith(_store.Data.Records.Keys);

        foreach (string relative in paths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsSafeRelative(relative)) continue;
            try { await SyncFileAsync(relative, local, remote, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                allSucceeded = false;
                Log("Chưa đồng bộ được: " + relative + " · " + SafeError(ex));
                Interlocked.Exchange(ref _dirty, 1); // Retry on next 60s cycle; avoid request bursts.
            }
        }
        return allSucceeded;
    }

    private static bool IsSafeRelative(string path)
        => path.Split('/').Length <= 33 && path.Split('/').All(IsSafeName) && !IsIgnored(path);

    private async Task SyncFileAsync(string relative, Dictionary<string, string> local,
                                      Dictionary<string, DriveItem> remote, CancellationToken ct)
    {
        bool hasLocal = local.TryGetValue(relative, out var localFile) && File.Exists(localFile);
        bool hasRemote = remote.TryGetValue(relative, out var remoteFile);
        bool tracked = _store.Data.Records.TryGetValue(relative, out var prior);
        if (!hasLocal && !hasRemote) { _store.Data.Records.Remove(relative); _store.Save(); return; }

        if (!tracked)
        {
            if (hasLocal && hasRemote)
            {
                // First run: equal names but potentially different contents. Preserve BOTH.
                string oldLocal = await FileMd5Async(localFile!, ct);
                if (!string.IsNullOrEmpty(remoteFile!.Md5) && string.Equals(oldLocal, remoteFile.Md5, StringComparison.OrdinalIgnoreCase))
                {
                    SaveRecord(relative, remoteFile, localFile!);
                    return;
                }
                string conflict = MakeConflictPath(localFile!);
                File.Copy(localFile!, conflict, false);
                Log("Giữ bản laptop xung đột: " + Path.GetFileName(conflict));
                string expected = LocalVersion(localFile!);
                await _drive.DownloadAsync(remoteFile!.Id, localFile!, expected, remoteFile.Md5, ct);
                SaveRecord(relative, remoteFile, localFile!);
            }
            else if (hasLocal)
                await UploadFileAsync(relative, localFile!, null, ct);
            else if (hasRemote)
                await DownloadFileAsync(relative, remoteFile!, ct);
            return;
        }

        string localVersion = hasLocal ? LocalVersion(localFile!) : "";
        bool localChanged = hasLocal && localVersion != prior!.LocalVersion;
        bool remoteChanged = hasRemote && remoteFile!.Version != prior!.RemoteVersion;

        if (!hasLocal && hasRemote)
        {
            if (_store.Data.MirrorDeletes && !remoteChanged)
            {
                await _drive.TrashAsync(remoteFile!.Id, ct);
                remote.Remove(relative);
                _store.Data.Records.Remove(relative);
                _store.Save();
                Log("Đã chuyển vào thùng rác Drive: " + relative);
            }
            else if (remoteChanged || !_store.Data.MirrorDeletes)
                await DownloadFileAsync(relative, remoteFile!, ct);
            return;
        }
        if (hasLocal && !hasRemote)
        {
            if (_store.Data.MirrorDeletes && !localChanged)
            {
                string backup = Path.Combine(SettingsStore.BaseFolder, "Recovery", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
                    relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Move(localFile!, backup);
                _store.Data.Records.Remove(relative);
                _store.Save();
                Log("Drive đã xóa; file laptop được chuyển vào Recovery: " + relative);
            }
            else
                await UploadFileAsync(relative, localFile!, null, ct);
            return;
        }

        if (localChanged && remoteChanged)
        {
            string conflict = MakeConflictPath(localFile!);
            File.Copy(localFile!, conflict, false);
            Log("Hai phía đều sửa; giữ bản xung đột: " + Path.GetFileName(conflict));
            await DownloadFileAsync(relative, remoteFile!, ct);
        }
        else if (localChanged)
            await UploadFileAsync(relative, localFile!, remoteFile!.Id, ct);
        else if (remoteChanged)
            await DownloadFileAsync(relative, remoteFile!, ct);
    }

    private async Task UploadFileAsync(string relative, string source, string? remoteId, CancellationToken ct)
    {
        // Prevent uploading an actively-written file.
        long sizeBefore = new FileInfo(source).Length;
        long ticksBefore = File.GetLastWriteTimeUtc(source).Ticks;
        await Task.Delay(1200, ct);
        if (!File.Exists(source) || new FileInfo(source).Length != sizeBefore
            || File.GetLastWriteTimeUtc(source).Ticks != ticksBefore)
            throw new IOException("File vẫn đang được ghi; sẽ thử sau.");

        string parent = await EnsureRemoteParentAsync(relative, ct);
        var item = await _drive.UploadAsync(source, relative.Split('/').Last(), parent, remoteId, ct);
        // Commit server ID even if the source changed during upload: never create a duplicate
        // file next time; retain the old source fingerprint so the new edit uploads again.
        _remoteFiles![relative] = item;
        bool sourceStillMatches = File.Exists(source)
            && new FileInfo(source).Length == sizeBefore
            && File.GetLastWriteTimeUtc(source).Ticks == ticksBefore;
        _store.Data.Records[relative] = new SyncRecord {
            RemoteId = item.Id,
            RemoteVersion = item.Version,
            LocalVersion = sizeBefore + "|" + ticksBefore
        };
        _store.Save();
        if (!sourceStillMatches)
        {
            Interlocked.Exchange(ref _dirty, 1);
            Log("File thay đổi trong khi tải; sẽ đồng bộ phiên bản mới: " + relative);
        }
        else Log("↑ Đã tải lên: " + relative);
    }

    private async Task DownloadFileAsync(string relative, DriveItem item, CancellationToken ct)
    {
        string target = LocalPath(relative);
        string expected = File.Exists(target) ? LocalVersion(target) : "";
        await _drive.DownloadAsync(item.Id, target, expected, item.Md5, ct);
        SaveRecord(relative, item, target);
        Log("↓ Đã tải về: " + relative);
    }

    private void SaveRecord(string relative, DriveItem remote, string localPath)
    {
        _store.Data.Records[relative] = new SyncRecord {
            RemoteId = remote.Id, RemoteVersion = remote.Version, LocalVersion = LocalVersion(localPath)
        };
        _store.Save();
    }

    private static async Task<string> FileMd5Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, true);
        return Convert.ToHexString(await System.Security.Cryptography.MD5.HashDataAsync(stream, ct)).ToLowerInvariant();
    }

    private static string MakeConflictPath(string path)
    {
        string dir = Path.GetDirectoryName(path)!;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int i = 0; i < 100; i++)
        {
            string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + (i == 0 ? "" : "-" + i);
            string result = Path.Combine(dir, stem + " (xung-dot-laptop-" + suffix + ")" + extension);
            if (!File.Exists(result)) return result;
        }
        throw new IOException("Không tạo được tên file xung đột duy nhất.");
    }

    private static string SafeError(Exception ex)
    {
        return ex switch {
            HttpRequestException h when h.StatusCode is not null => "Google HTTP " + (int)h.StatusCode.Value,
            UnauthorizedAccessException => "Không có quyền đọc/ghi file",
            IOException => "File đang bận hoặc lỗi ổ đĩa",
            _ => ex.GetType().Name
        };
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _cancel?.Cancel();
        _cancel?.Dispose();
        _wake.Dispose();
        _syncLock.Dispose();
    }
}
