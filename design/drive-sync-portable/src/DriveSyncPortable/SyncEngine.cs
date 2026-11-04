using System.Collections.Concurrent;

namespace DriveSyncPortable;

internal sealed class SyncEngine : IDisposable
{
    private readonly SettingsStore _store;
    private readonly ICloudDriveApi _drive;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private CancellationTokenSource? _cancel;
    private Task? _loop;
    private FileSystemWatcher? _watcher;
    private int _dirty = 1;
    private Dictionary<string, DriveItem>? _remoteFiles;
    private Dictionary<string, string>? _remoteFolders;
    private DateTimeOffset _lastReconcile = DateTimeOffset.MinValue;
    private bool _conflictNeedsReview;

    public event Action<string>? Status;
    public event Action<SyncStatus>? StateChanged;
    public event Action<TransferProgress>? Progress;
    public event Action<string>? Activity;
    public bool IsRunning => _loop is { IsCompleted: false };
    public bool Paused { get; private set; }

    public SyncEngine(SettingsStore settings, ICloudDriveApi api)
    {
        _store = settings;
        _drive = api;
    }

    private void Report(SyncState state, string title, string detail)
    {
        Status?.Invoke(title);
        StateChanged?.Invoke(new SyncStatus(state, title, detail));
    }
    private void Log(string message) => Activity?.Invoke(message);

    public void Start()
    {
        if (IsRunning) return;
        SyncScopeGuard.ValidateLocalRoot(_store.Data.LocalFolder);
        SyncScopeGuard.ValidateRemoteRoot(_store.Data.RemoteFolderId);
        if (!_drive.IsSignedIn)
            throw new InvalidOperationException("Chưa đăng nhập tài khoản Google.");

        _cancel = new CancellationTokenSource();
        _drive.TransferChanged = progress =>
        {
            Progress?.Invoke(progress);
            if (!progress.Completed)
                Report(progress.Direction == TransferDirection.Upload ? SyncState.Uploading : SyncState.Downloading,
                    progress.Direction == TransferDirection.Upload ? "Đang đồng bộ · Tải lên" : "Đang đồng bộ · Tải xuống",
                    progress.FileName + " · " + TransferFormat.Size(progress.BytesTransferred)
                    + " / " + TransferFormat.Size(progress.TotalBytes));
        };
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
        Report(pause ? SyncState.Paused : SyncState.Checking,
            pause ? "Đã tạm dừng" : "Đang kiểm tra",
            pause ? "Không chuyển tệp; các thay đổi sẽ được xử lý khi tiếp tục"
                  : "Đang kiểm tra thay đổi mới");
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
        _drive.TransferChanged = null;
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
                    SyncState state = ex switch {
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized }
                            => SyncState.AuthenticationError,
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden }
                            => SyncState.NeedsAttention,
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                            => SyncState.WaitingNetwork,
                        HttpRequestException { StatusCode: not null }
                            => SyncState.NeedsAttention,
                        HttpRequestException => SyncState.WaitingNetwork,
                        System.Net.Sockets.SocketException => SyncState.WaitingNetwork,
                        TaskCanceledException => SyncState.WaitingNetwork,
                        _ => SyncState.NeedsAttention
                    };
                    string title = ex switch {
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                            => "Google tạm giới hạn yêu cầu",
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden }
                            => "Cần kiểm tra quyền Google",
                        _ when state == SyncState.AuthenticationError => "Cần đăng nhập lại",
                        _ when state == SyncState.WaitingNetwork => "Chờ kết nối mạng",
                        _ => "Có lỗi cần kiểm tra"
                    };
                    Report(state, title, "Chưa hoàn tất đồng bộ · Thử lại sau 60 giây");
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

    public Task SyncNowAsync(CancellationToken ct)
    {
        Interlocked.Exchange(ref _dirty, 1);
        return RunOnceAsync(ct);
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (Paused) return;
        if (!Directory.Exists(_store.Data.LocalFolder))
        {
            Report(SyncState.FolderUnavailable, "Không thấy thư mục laptop",
                "Kiểm tra ổ đĩa hoặc thư mục đã chọn · Không tự xóa file Drive");
            return;
        }
        await _syncLock.WaitAsync(ct);
        try
        {
            SyncScopeGuard.ValidateLocalRoot(_store.Data.LocalFolder);
            SyncScopeGuard.ValidateRemoteRoot(_store.Data.RemoteFolderId);
            await AssertRemoteFolderWithinScopeAsync(_store.Data.RemoteFolderId, ct);
            Report(SyncState.Checking, "Đang kiểm tra thay đổi",
                "Đối chiếu Google Drive và thư mục trên máy");
            bool localDirty = Interlocked.Exchange(ref _dirty, 0) == 1;
            bool refresh = _remoteFiles is null
                        || _remoteFolders is null
                        || DateTimeOffset.UtcNow - _lastReconcile >= TimeSpan.FromHours(4);
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
                    var tracked = new HashSet<string>(StringComparer.Ordinal);
                    if (_remoteFiles is not null)
                        foreach (var item in _remoteFiles.Values) tracked.Add(item.Id);
                    if (_remoteFolders is not null)
                        foreach (var id in _remoteFolders.Values) tracked.Add(id);
                    tracked.Add(_store.Data.RemoteFolderId);
                    var changes = await _drive.ReadChangesAsync(_store.Data.ChangeToken, tracked, ct);
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
                bool dirtyDuringCheck = Volatile.Read(ref _dirty) == 1;
                Report(dirtyDuringCheck ? SyncState.Checking
                    : _conflictNeedsReview ? SyncState.NeedsAttention : SyncState.Synced,
                    dirtyDuringCheck ? "Còn thay đổi đang chờ"
                    : _conflictNeedsReview ? "Có file xung đột" : "Đã đồng bộ",
                    dirtyDuringCheck ? "Đang nhận thay đổi mới; sắp kiểm tra lại"
                    : _conflictNeedsReview ? "Đã giữ hai phiên bản; xem lịch sử để xử lý"
                    : "Không có file cần chuyển · Lần kiểm tra " + DateTime.Now.ToString("HH:mm:ss"));
                return;
            }
            bool allSucceeded = await ReconcileAsync(ct);
            bool pending = Volatile.Read(ref _dirty) == 1;
            Report(!allSucceeded || _conflictNeedsReview ? SyncState.NeedsAttention
                    : pending ? SyncState.Checking : SyncState.Synced,
                !allSucceeded ? "Có file chưa đồng bộ"
                : _conflictNeedsReview ? "Có file xung đột"
                : pending ? "Còn thay đổi đang chờ" : "Đã đồng bộ",
                !allSucceeded ? "Kiểm tra lịch sử để xem lỗi · Tự thử lại sau 60 giây"
                : _conflictNeedsReview ? "Đã giữ hai phiên bản; xem lịch sử để xử lý"
                : pending ? "Đang nhận thay đổi mới; sắp đồng bộ tiếp"
                : "Hoàn tất · Lần kiểm tra " + DateTime.Now.ToString("HH:mm:ss"));
        }
        finally { _syncLock.Release(); }
    }

    // Verify current Drive ancestry before each write; never trust cached paths alone.
    private async Task AssertRemoteFolderWithinScopeAsync(string folderId, CancellationToken ct)
    {
        SyncScopeGuard.ValidateRemoteRoot(_store.Data.RemoteFolderId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string current = folderId;
        for (int depth = 0; depth <= 32; depth++)
        {
            if (!seen.Add(current)) throw new IOException("Drive folder parent cycle.");
            var item = await _drive.GetItemAsync(current, ct);
            if (!item.Folder || item.Trashed)
                throw new IOException("Drive folder is no longer available.");
            if (current == _store.Data.RemoteFolderId) return;
            if (item.Parents.Length != 1)
                throw new IOException("Drive folder is outside selected root.");
            current = item.Parents[0];
        }
        throw new IOException("Drive folder depth exceeds allowed scope.");
    }

    private async Task AssertRemoteFileWithinScopeAsync(string fileId, CancellationToken ct)
    {
        var item = await _drive.GetItemAsync(fileId, ct);
        if (item.Folder || item.Trashed || item.Parents.Length != 1)
            throw new IOException("Drive file moved or unavailable; update blocked.");
        await AssertRemoteFolderWithinScopeAsync(item.Parents[0], ct);
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
                {
                    // Keep a Windows InternetShortcut, not an editable Office mirror.
                    // The reserved suffix is excluded from uploads/deletes.
                    string shortcut = LocalPath(path + ".gdrive.url");
                    string link = "[InternetShortcut]" + Environment.NewLine
                        + "URL=https://drive.google.com/open?id=" + Uri.EscapeDataString(item.Id)
                        + Environment.NewLine + "DriveSyncNativeFile=1" + Environment.NewLine;
                    Directory.CreateDirectory(Path.GetDirectoryName(shortcut)!);
                    if (!File.Exists(shortcut)
                        || File.ReadAllText(shortcut).Contains("DriveSyncNativeFile=1",
                            StringComparison.Ordinal))
                    {
                        if (!File.Exists(shortcut) || File.ReadAllText(shortcut) != link)
                            File.WriteAllText(shortcut, link);
                    }
                    else Log("Bỏ qua shortcut trùng với file do người dùng tạo: " + shortcut);
                }
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

    private bool IsIgnored(string relative)
    {
        string name = relative.Split('/').Last();
        if (name.StartsWith("~$", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".drivesync-partial", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".gdrive.url", StringComparison.OrdinalIgnoreCase)
            || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
            || name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase))
            return true;
        string ext = Path.GetExtension(name);
        return ext.Length > 0 && _store.Data.IgnoredExtensions
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private string LocalPath(string relative)
        => SyncScopeGuard.ResolveLocalChild(_store.Data.LocalFolder, relative);

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

    private IEnumerable<string> LocalFolders()
    {
        var pending = new Stack<string>();
        pending.Push(_store.Data.LocalFolder);
        while (pending.Count > 0)
        {
            string root = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                var attrs = File.GetAttributes(directory);
                if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                string relative = Path.GetRelativePath(_store.Data.LocalFolder, directory).Replace('\\', '/');
                if (!IsSafeRelative(relative)) continue;
                yield return relative;
                pending.Push(directory);
            }
        }
    }

    private async Task EnsureRemoteFolderAsync(string relative, CancellationToken ct)
    {
        string current = "";
        foreach (string segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string next = current.Length == 0 ? segment : current + "/" + segment;
            if (!_remoteFolders!.ContainsKey(next))
            {
                await AssertRemoteFolderWithinScopeAsync(_remoteFolders[current], ct);
                var newFolder = await _drive.CreateFolderAsync(segment,
                    _remoteFolders[current], ct);
                _remoteFolders[next] = newFolder.Id;
                Log("Đã tạo thư mục Drive: " + next);
            }
            current = next;
        }
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
                await AssertRemoteFolderWithinScopeAsync(_remoteFolders[current], ct);
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
        _conflictNeedsReview = local.Keys.Any(p =>
            p.Contains("(xung-dot-laptop-", StringComparison.OrdinalIgnoreCase));
        var remote = _remoteFiles!;
        // Remote folders are created even if empty, without blindly removing local folders.
        foreach (var folder in _remoteFolders!.Keys.OrderBy(p => p.Count(c => c == '/')))
        {
            if (folder.Length == 0) continue;
            Directory.CreateDirectory(LocalPath(folder));
        }
        foreach (var folder in LocalFolders().OrderBy(x => x.Count(c => c == '/')))
            await EnsureRemoteFolderAsync(folder, ct);
        foreach (var pair in local)
            await EnsureRemoteParentAsync(pair.Key, ct);

        // Remote rename/move detection: the same immutable Drive File ID now has a
        // new relative path. Only move a local file if its contents are unchanged.
        foreach (var current in remote.ToArray())
        {
            if (_store.Data.Records.ContainsKey(current.Key) || local.ContainsKey(current.Key))
                continue;
            var previous = _store.Data.Records.FirstOrDefault(p =>
                p.Key != current.Key && p.Value.RemoteId == current.Value.Id);
            if (string.IsNullOrWhiteSpace(previous.Key)
                || !local.TryGetValue(previous.Key, out string? oldFile)
                || !File.Exists(oldFile)
                || previous.Value.LocalVersion != LocalVersion(oldFile)
                || previous.Value.RemoteVersion != current.Value.Version
                || string.IsNullOrEmpty(current.Value.Md5))
                continue;
            if (!string.Equals(await FileMd5Async(oldFile, ct),
                current.Value.Md5, StringComparison.OrdinalIgnoreCase))
                continue;
            string newFile = LocalPath(current.Key);
            if (File.Exists(newFile)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(newFile)!);
            SyncScopeGuard.ResolveExistingFile(_store.Data.LocalFolder, oldFile);
            File.Move(oldFile, newFile);
            local.Remove(previous.Key);
            local[current.Key] = newFile;
            _store.Data.Records.Remove(previous.Key);
            SaveRecord(current.Key, current.Value, newFile);
            Log("Đổi tên/di chuyển từ Drive: " + previous.Key + " → " + current.Key);
        }

        // Local rename/move detection: stable local fingerprint + matching remote MD5.
        // Never treat modified contents as a pure rename; ambiguous matches fall back
        // to conflict-preserving behavior.
        var missing = _store.Data.Records.Keys
            .Where(k => !local.ContainsKey(k) && remote.ContainsKey(k)).ToList();
        foreach (var candidate in local.Where(p => !_store.Data.Records.ContainsKey(p.Key)
                                                && !remote.ContainsKey(p.Key)))
        {
            SyncScopeGuard.ResolveExistingFile(_store.Data.LocalFolder, candidate.Value);
            string signature = LocalVersion(candidate.Value);
            var matches = missing.Where(old =>
                _store.Data.Records[old].LocalVersion == signature
                && remote[old].Size == new FileInfo(candidate.Value).Length
                && !string.IsNullOrEmpty(remote[old].Md5)).ToArray();
            if (matches.Length != 1) continue;
            string oldPath = matches[0];
            var sourceItem = remote[oldPath];
            if (!string.Equals(await FileMd5Async(candidate.Value, ct),
                sourceItem.Md5, StringComparison.OrdinalIgnoreCase)) continue;
            string newParent = await EnsureRemoteParentAsync(candidate.Key, ct);
            string oldParent = sourceItem.Parents.FirstOrDefault() ?? _store.Data.RemoteFolderId;
            await AssertRemoteFileWithinScopeAsync(sourceItem.Id, ct);
            await AssertRemoteFolderWithinScopeAsync(newParent, ct);
            var moved = await _drive.RenameOrMoveAsync(sourceItem.Id,
                Path.GetFileName(candidate.Key), oldParent, newParent, ct);
            remote.Remove(oldPath);
            remote[candidate.Key] = moved;
            _store.Data.Records.Remove(oldPath);
            SaveRecord(candidate.Key, moved, candidate.Value);
            missing.Remove(oldPath);
            Log("Đã đổi tên/di chuyển, giữ File ID: " + oldPath + " → " + candidate.Key);
        }

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

    private bool IsSafeRelative(string path)
        => path.Split('/').Length <= 33 && path.Split('/').All(IsSafeName) && !IsIgnored(path);

    private async Task SyncFileAsync(string relative, Dictionary<string, string> local,
                                      Dictionary<string, DriveItem> remote, CancellationToken ct)
    {
        bool hasLocal = local.TryGetValue(relative, out var localFile) && File.Exists(localFile);
        if (hasLocal)
            SyncScopeGuard.ResolveExistingFile(_store.Data.LocalFolder, localFile!);
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
                SyncScopeGuard.ResolveExistingFile(_store.Data.LocalFolder, localFile!);
                File.Copy(localFile!, conflict, false);
                _conflictNeedsReview = true;
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
                await AssertRemoteFileWithinScopeAsync(remoteFile!.Id, ct);
                await _drive.TrashAsync(remoteFile.Id, ct);
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
                string backup = Path.Combine(_store.StorageFolder, "Recovery", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
                    relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                SyncScopeGuard.ResolveExistingFile(_store.Data.LocalFolder, localFile!);
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
            _conflictNeedsReview = true;
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
        await AssertRemoteFolderWithinScopeAsync(parent, ct);
        if (!string.IsNullOrWhiteSpace(remoteId))
            await AssertRemoteFileWithinScopeAsync(remoteId, ct);
        SyncScopeGuard.ResolveExistingFile(_store.Data.LocalFolder, source);
        Report(SyncState.Uploading, "Đang đồng bộ · Tải lên", relative);
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
        await AssertRemoteFileWithinScopeAsync(item.Id, ct);
        Report(SyncState.Downloading, "Đang đồng bộ · Tải xuống", relative);
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
