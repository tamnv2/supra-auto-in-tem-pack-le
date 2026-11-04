namespace DriveSyncPortable;

// Fail closed: all user files belong to exactly one selected local folder.
internal static class SyncScopeGuard
{
    public static void ValidateRemoteRoot(string id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id.Equals("root", StringComparison.OrdinalIgnoreCase)
            || id.Equals("appDataFolder", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Phải chọn một thư mục riêng, không được chọn toàn bộ My Drive.");
    }

    public static string ValidateLocalRoot(string selected)
    {
        if (string.IsNullOrWhiteSpace(selected))
            throw new IOException("Chưa chọn thư mục máy tính.");
        string full = Path.GetFullPath(selected)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string driveRoot = (Path.GetPathRoot(Path.GetFullPath(selected)) ?? "")
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Equals(driveRoot, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Không được chọn toàn bộ ổ đĩa.");
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException("Thư mục đồng bộ không tồn tại.");
        RejectReparsePoints(full);
        if (IsSameOrChild(full, Path.GetFullPath(SettingsStore.BaseFolder)))
            throw new IOException("Không thể đồng bộ thư mục chứa cấu hình, token của DriveSync.");
        return full;
    }

    public static string ResolveLocalChild(string selected, string relative)
    {
        string root = ValidateLocalRoot(selected);
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new IOException("Đường dẫn file không hợp lệ.");
        string[] parts = relative.Replace('\\', '/').Split('/');
        if (parts.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."
            || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains(':')))
            throw new IOException("Đường dẫn chứa ký tự không hợp lệ hoặc thoát khỏi thư mục.");
        string full = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase) || !IsSameOrChild(root, full))
            throw new IOException("Từ chối truy cập file ngoài thư mục đã chọn.");
        RejectReparsePoints(full);
        return full;
    }

    public static string ResolveExistingFile(string selected, string fullPath)
    {
        string root = ValidateLocalRoot(selected);
        string full = Path.GetFullPath(fullPath);
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase) || !IsSameOrChild(root, full))
            throw new IOException("Từ chối truy cập file ngoài thư mục đã chọn.");
        RejectReparsePoints(full);
        return full;
    }

    private static bool IsSameOrChild(string parent, string path)
    {
        string prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectReparsePoints(string path)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full) ?? "";
        string current = root;
        foreach (string segment in full.Substring(root.Length).Split(
            Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Phát hiện symlink/junction: từ chối truy cập ra ngoài.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
