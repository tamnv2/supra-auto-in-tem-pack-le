using System.Security.Cryptography;

namespace DriveSyncPortable;

// In-memory Google Drive emulator: tests the REAL SyncEngine with zero network requests.
internal sealed class FakeDrive : ICloudDriveApi
{
    public bool IsSignedIn => true;
    public Action<TransferProgress>? TransferChanged { get; set; }
    private sealed class Node
    {
        public string Id = "";
        public string Name = "";
        public string Parent = "";
        public bool Folder;
        public bool Trashed;
        public byte[] Bytes = Array.Empty<byte>();
        public string Timestamp = "";
    }

    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private int _version;
    public FakeDrive()
    {
        _nodes["root"] = new Node { Id = "root", Folder = true, Name = "My Drive" };
        _nodes["scope"] = new Node { Id = "scope", Folder = true, Parent = "root", Name = "Selected" };
        _nodes["outside"] = new Node { Id = "outside", Folder = true, Parent = "root", Name = "Other" };
    }

    private void Touch(Node node)
    {
        _version++;
        node.Timestamp = DateTimeOffset.UtcNow.AddSeconds(_version).ToString("O");
    }

    private static string Md5(byte[] data) => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
    private static DriveItem ToItem(Node n)
        => new(n.Id, n.Name, n.Folder ? "application/vnd.google-apps.folder" : "application/octet-stream",
               n.Timestamp, n.Folder ? "" : Md5(n.Bytes), n.Bytes.LongLength, new[] { n.Parent }, n.Trashed);

    private Node Find(string name, string parent)
        => _nodes.Values.Single(n => !n.Trashed && n.Parent == parent && n.Name == name);

    public byte[] FileBytes(string name, string parent = "scope")
        => Find(name, parent).Bytes.ToArray();

    public void ChangeRemoteFile(string name, byte[] bytes, string parent = "scope")
    {
        var node = _nodes.Values.FirstOrDefault(n => !n.Trashed && n.Parent == parent && n.Name == name);
        if (node is null)
        {
            node = new Node {
                Id = Guid.NewGuid().ToString("N"), Name = name, Parent = parent, Folder = false
            };
            _nodes[node.Id] = node;
        }
        node.Bytes = bytes.ToArray();
        Touch(node);
    }

    public void MoveWithoutChangeToken(string name, string fromParent, string toParent)
    {
        Find(name, fromParent).Parent = toParent;
        // Simulate stale cached path before a change-feed event.
    }

    public void DeleteRemoteFile(string name, string parent = "scope")
    {
        var node = Find(name, parent);
        node.Trashed = true;
        Touch(node);
    }

    public bool Exists(string name, string parent = "scope")
        => _nodes.Values.Any(n => !n.Trashed && n.Parent == parent && n.Name == name);

    public Task<string> GetStartPageTokenAsync(CancellationToken ct)
        => Task.FromResult(_version.ToString());

    public Task<(bool changed, string nextToken)> ReadChangesAsync(
        string pageToken, IReadOnlyCollection<string> trackedIds, CancellationToken ct)
    {
        _ = int.TryParse(pageToken, out int original);
        return Task.FromResult((original < _version, _version.ToString()));
    }

    public Task<DriveItem> GetItemAsync(string id, CancellationToken ct)
        => Task.FromResult(ToItem(_nodes[id]));

    public Task<List<DriveItem>> ListChildrenAsync(string parentId, CancellationToken ct)
    {
        return Task.FromResult(_nodes.Values
            .Where(n => !n.Trashed && n.Parent == parentId)
            .Select(ToItem).ToList());
    }

    public Task<DriveItem> CreateFolderAsync(string name, string parent, CancellationToken ct)
    {
        var folder = new Node {
            Id = Guid.NewGuid().ToString("N"), Name = name,
            Parent = parent, Folder = true
        };
        _nodes[folder.Id] = folder;
        Touch(folder);
        return Task.FromResult(ToItem(folder));
    }

    public async Task<DriveItem> UploadAsync(string localPath, string name, string parentId,
                                              string? existingId, CancellationToken ct)
    {
        Node node;
        if (existingId is null)
        {
            node = new Node { Id = Guid.NewGuid().ToString("N"),
                Name = name, Parent = parentId, Folder = false };
            _nodes[node.Id] = node;
        }
        else node = _nodes[existingId];
        node.Name = name;
        node.Bytes = await File.ReadAllBytesAsync(localPath, ct);
        Touch(node);
        return ToItem(node);
    }

    public async Task DownloadAsync(string remoteId, string destination, string expectedLocalState,
                                     string remoteMd5, CancellationToken ct)
    {
        var node = _nodes[remoteId];
        string temp = destination + ".drivesync-partial";
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            await File.WriteAllBytesAsync(temp, node.Bytes, ct);
            if (!string.Equals(remoteMd5, Md5(node.Bytes), StringComparison.OrdinalIgnoreCase))
                throw new IOException("FAKE_CHECKSUM_FAILURE");
            string actual = "";
            if (File.Exists(destination))
            {
                var info = new FileInfo(destination);
                actual = info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            }
            if (expectedLocalState != actual)
                throw new IOException("FAKE_LOCAL_RACE_DETECTED");
            File.Move(temp, destination, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public Task<DriveItem> RenameOrMoveAsync(string remoteId, string newName,
        string oldParentId, string newParentId, CancellationToken ct)
    {
        var node = _nodes[remoteId];
        if (node.Parent != oldParentId) throw new InvalidOperationException("FAKE_BAD_PARENT");
        node.Name = newName; node.Parent = newParentId;
        Touch(node);
        return Task.FromResult(ToItem(node));
    }

    public Task TrashAsync(string remoteId, CancellationToken ct)
    {
        var node = _nodes[remoteId];
        node.Trashed = true;
        Touch(node);
        return Task.CompletedTask;
    }
}

internal static class SyncSmoke
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("SYNC_SMOKE_FAILED: " + name);
    }

    public static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "DriveSync-OfflineSmoke-" + Guid.NewGuid().ToString("N"));
        string local = Path.Combine(root, "local");
        string privateSettings = Path.Combine(root, "private");
        Directory.CreateDirectory(local);
        try
        {
            string outsideLocal = Path.Combine(root, "outside-local");
            Directory.CreateDirectory(outsideLocal);
            string protectedFile = Path.Combine(outsideLocal, "protected.txt");
            await File.WriteAllTextAsync(protectedFile, "PROTECTED");

            bool traversalRejected = false;
            try { SyncScopeGuard.ResolveLocalChild(local, "../outside-local/protected.txt"); }
            catch (IOException) { traversalRejected = true; }
            Check(traversalRejected, "LOCAL_TRAVERSAL_REJECTED");
            bool driveRootRejected = false;
            try { SyncScopeGuard.ValidateRemoteRoot("root"); }
            catch (InvalidOperationException) { driveRootRejected = true; }
            Check(driveRootRejected, "WHOLE_MY_DRIVE_REJECTED");

            var settings = new SettingsStore(privateSettings);
            settings.Data.LocalFolder = local;
            settings.Data.RemoteFolderId = "scope";
            settings.Data.RemoteFolderName = "Test Only";
            settings.Data.MirrorDeletes = true;
            settings.Data.MappingApproved = true;
            settings.Save();
            var fake = new FakeDrive();
            using var engine = new SyncEngine(settings, fake);
            var activity = new List<string>();
            engine.Activity += activity.Add;

            fake.ChangeRemoteFile("outside.txt",
                System.Text.Encoding.UTF8.GetBytes("OUTSIDE"), "outside");

            // 1. Local create -> Google upload
            string workbook = Path.Combine(local, "BaoCao.xlsx");
            await File.WriteAllTextAsync(workbook, "first");
            await engine.SyncNowAsync(CancellationToken.None);
            Check(fake.Exists("BaoCao.xlsx"), "LOCAL_CREATE_UPLOADED");
            Check(System.Text.Encoding.UTF8.GetString(fake.FileBytes("BaoCao.xlsx")) == "first",
                "UPLOADED_CONTENT_MATCH");

            // 2. Google edit -> laptop download
            fake.ChangeRemoteFile("BaoCao.xlsx", System.Text.Encoding.UTF8.GetBytes("remote-update"));
            await engine.SyncNowAsync(CancellationToken.None);
            Check((await File.ReadAllTextAsync(workbook)) == "remote-update", "REMOTE_EDIT_DOWNLOADED");

            // 3. Simultaneous edits preserve both versions
            await File.WriteAllTextAsync(workbook, "my-local-changes");
            File.SetLastWriteTimeUtc(workbook, DateTime.UtcNow.AddSeconds(4));
            fake.ChangeRemoteFile("BaoCao.xlsx", System.Text.Encoding.UTF8.GetBytes("cloud-changes"));
            await engine.SyncNowAsync(CancellationToken.None);
            Check((await File.ReadAllTextAsync(workbook)) == "cloud-changes", "CLOUD_VERSION_RETAINED");
            string[] conflict = Directory.GetFiles(local, "*xung-dot-laptop*");
            Check(conflict.Length == 1, "LOCAL_CONFLICT_BACKUP");
            Check((await File.ReadAllTextAsync(conflict[0])) == "my-local-changes", "LOCAL_CONFLICT_CONTENT");
            await engine.SyncNowAsync(CancellationToken.None);
            Check(fake.Exists(Path.GetFileName(conflict[0])), "CONFLICT_COPY_UPLOADED");

            // 4. Remote file creation -> laptop download
            fake.ChangeRemoteFile("KeHoach.pdf", System.Text.Encoding.UTF8.GetBytes("PDF DATA"));
            await engine.SyncNowAsync(CancellationToken.None);
            string remoteCreated = Path.Combine(local, "KeHoach.pdf");
            Check(File.Exists(remoteCreated), "REMOTE_CREATE_DOWNLOADED");
            Check((await File.ReadAllTextAsync(remoteCreated)) == "PDF DATA", "REMOTE_CREATE_CONTENT");

            // 5. Local rename -> Drive metadata update (no duplicate file).
            string renameBefore = Path.Combine(local, "RenameMe.txt");
            string renameAfter = Path.Combine(local, "Renamed.txt");
            await File.WriteAllTextAsync(renameBefore, "rename-contents");
            await engine.SyncNowAsync(CancellationToken.None);
            Check(fake.Exists("RenameMe.txt"), "RENAME_SOURCE_UPLOADED");
            File.Move(renameBefore, renameAfter);
            await engine.SyncNowAsync(CancellationToken.None);
            Check(fake.Exists("Renamed.txt") && !fake.Exists("RenameMe.txt"),
                "LOCAL_RENAME_PRESERVES_REMOTE_FILE");

            // 6. Local empty folder -> remote folder (no sentinel file).
            Directory.CreateDirectory(Path.Combine(local, "EmptyFolder"));
            await engine.SyncNowAsync(CancellationToken.None);
            Check((await fake.ListChildrenAsync("scope", CancellationToken.None))
                .Any(x => x.Name == "EmptyFolder" && x.Folder), "LOCAL_EMPTY_FOLDER_CREATED");

            // Folder rename must keep the same Drive folder ID and child contents.
            string oldDir = Path.Combine(local, "OldGroup");
            Directory.CreateDirectory(oldDir);
            await File.WriteAllTextAsync(Path.Combine(oldDir, "inside.txt"), "SAME");
            await engine.SyncNowAsync(CancellationToken.None);
            var oldFolder = (await fake.ListChildrenAsync("scope", CancellationToken.None))
                .Single(x => x.Name == "OldGroup");
            string newDir = Path.Combine(local, "NewGroup");
            Directory.Move(oldDir, newDir);
            engine.QueueFolderRename("OldGroup", "NewGroup");
            await engine.SyncNowAsync(CancellationToken.None);
            var renamed = (await fake.ListChildrenAsync("scope", CancellationToken.None))
                .Single(x => x.Name == "NewGroup");
            Check(renamed.Id == oldFolder.Id, "FOLDER_RENAME_KEEPS_ID");
            Check(!Directory.Exists(oldDir), "NO_OLD_FOLDER_RECREATED");
            Check(fake.Exists("inside.txt", renamed.Id), "FOLDER_CHILD_PRESERVED");

            // 7. Remote rename -> local move (no double copy).
            fake.ChangeRemoteFile("CloudRename.txt", System.Text.Encoding.UTF8.GetBytes("cloud-rename"));
            await engine.SyncNowAsync(CancellationToken.None);
            var cloudItem = (await fake.ListChildrenAsync("scope", CancellationToken.None))
                .Single(x => x.Name == "CloudRename.txt");
            await fake.RenameOrMoveAsync(cloudItem.Id, "CloudRenamed.txt", "scope", "scope",
                CancellationToken.None);
            await engine.SyncNowAsync(CancellationToken.None);
            Check(File.Exists(Path.Combine(local, "CloudRenamed.txt"))
                && !File.Exists(Path.Combine(local, "CloudRename.txt")),
                "REMOTE_RENAME_MOVED_LOCAL");

            // No Drive update/trash after the file is moved outside the selected folder.
            string leaving = Path.Combine(local, "Leaving.txt");
            await File.WriteAllTextAsync(leaving, "leaving");
            await engine.SyncNowAsync(CancellationToken.None);
            Check(fake.Exists("Leaving.txt"), "SCOPE_SENTINEL_UPLOADED");
            fake.MoveWithoutChangeToken("Leaving.txt", "scope", "outside");
            File.Delete(leaving);
            await engine.SyncNowAsync(CancellationToken.None);
            Check(fake.Exists("Leaving.txt", "outside"), "STALE_CACHE_CANNOT_TRASH_OUTSIDE");
            Check(!File.Exists(Path.Combine(local, "outside.txt")), "OUTSIDE_NOT_DOWNLOADED");
            Check(await File.ReadAllTextAsync(protectedFile) == "PROTECTED",
                "OUTSIDE_LOCAL_UNCHANGED");

            // 8. Local delete -> Drive trash
            File.Delete(workbook);
            await engine.SyncNowAsync(CancellationToken.None);
            Check(!fake.Exists("BaoCao.xlsx"), "LOCAL_DELETE_TRASHED_REMOTELY");

            // 6. Remote delete -> local Recovery (never permanent delete)
            fake.DeleteRemoteFile("KeHoach.pdf");
            await engine.SyncNowAsync(CancellationToken.None);
            Check(!File.Exists(remoteCreated), "REMOTE_DELETE_LOCAL_MOVED");
            string recovery = Path.Combine(privateSettings, "Recovery");
            Check(Directory.Exists(recovery) &&
                  Directory.EnumerateFiles(recovery, "KeHoach.pdf", SearchOption.AllDirectories).Any(),
                "LOCAL_RECOVERY_AVAILABLE");

            // 7. Stable state + restart can reconcile without recreating deleted files
            using var restarted = new SyncEngine(new SettingsStore(privateSettings), fake);
            await restarted.SyncNowAsync(CancellationToken.None);
            Check(!fake.Exists("BaoCao.xlsx") && !File.Exists(remoteCreated),
                "RESTART_DOES_NOT_RESTORE_DELETED_FILES");

            Console.WriteLine("SYNC_ENGINE_OFFLINE_SMOKE_PASS: upload/download/conflict/delete/recovery/restart");
            return 0;
        }
        catch (Exception e)
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(),
                "drivesync-sync-smoke-error.txt"), e.ToString()); } catch { }
            Console.Error.WriteLine(e);
            return 3;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
