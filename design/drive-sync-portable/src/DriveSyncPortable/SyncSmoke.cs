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
    }

    private void Touch(Node node)
    {
        _version++;
        node.Timestamp = DateTimeOffset.UtcNow.AddSeconds(_version).ToString("O");
    }

    private static string Md5(byte[] data) => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
    private static DriveItem ToItem(Node n)
        => new(n.Id, n.Name, n.Folder ? "application/vnd.google-apps.folder" : "application/octet-stream",
               n.Timestamp, n.Folder ? "" : Md5(n.Bytes), n.Bytes.LongLength, new[] { n.Parent });

    private Node Find(string name, string parent)
        => _nodes.Values.Single(n => !n.Trashed && n.Parent == parent && n.Name == name);

    public byte[] FileBytes(string name, string parent = "root")
        => Find(name, parent).Bytes.ToArray();

    public void ChangeRemoteFile(string name, byte[] bytes, string parent = "root")
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

    public void DeleteRemoteFile(string name, string parent = "root")
    {
        var node = Find(name, parent);
        node.Trashed = true;
        Touch(node);
    }

    public bool Exists(string name, string parent = "root")
        => _nodes.Values.Any(n => !n.Trashed && n.Parent == parent && n.Name == name);

    public Task<string> GetStartPageTokenAsync(CancellationToken ct)
        => Task.FromResult(_version.ToString());

    public Task<(bool changed, string nextToken)> ReadChangesAsync(string pageToken, CancellationToken ct)
    {
        _ = int.TryParse(pageToken, out int original);
        return Task.FromResult((original < _version, _version.ToString()));
    }

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
            var settings = new SettingsStore(privateSettings);
            settings.Data.LocalFolder = local;
            settings.Data.RemoteFolderId = "root";
            settings.Data.RemoteFolderName = "Test Only";
            settings.Data.MirrorDeletes = true;
            settings.Data.MappingApproved = true;
            settings.Save();
            var fake = new FakeDrive();
            using var engine = new SyncEngine(settings, fake);
            var activity = new List<string>();
            engine.Activity += activity.Add;

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

            // 5. Local delete -> Drive trash
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
            Console.Error.WriteLine(e);
            return 3;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
