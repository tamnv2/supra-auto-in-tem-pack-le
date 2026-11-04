using System.Text.Json;

namespace DriveSyncPortable;

// Local estimates only: not Google Cloud Project usage or billing data.
internal sealed class ApiUsageDay
{
    public string UtcDate { get; set; } = "";
    public long Requests { get; set; }
    public long EstimatedUnits { get; set; }
    public long UploadedBytes { get; set; }
    public long DownloadedBytes { get; set; }
}

internal sealed class UsageTracker
{
    private readonly object _gate = new();
    private readonly string _path;
    private ApiUsageDay _day;

    public UsageTracker(string storageDirectory)
    {
        Directory.CreateDirectory(storageDirectory);
        _path = Path.Combine(storageDirectory, "api-usage-local.json");
        try
        {
            _day = File.Exists(_path)
                ? JsonSerializer.Deserialize<ApiUsageDay>(File.ReadAllText(_path)) ?? new ApiUsageDay()
                : new ApiUsageDay();
        }
        catch { _day = new ApiUsageDay(); }
        lock (_gate) RotateIfNeeded();
    }

    private void RotateIfNeeded()
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (_day.UtcDate == today) return;
        _day = new ApiUsageDay { UtcDate = today };
        SaveLocked();
    }

    private void SaveLocked()
    {
        try
        {
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_day));
            File.Move(tmp, _path, true);
        }
        catch { /* Metering failures must not block sync. */ }
    }

    public ApiUsageDay Snapshot()
    {
        lock (_gate)
        {
            RotateIfNeeded();
            return new ApiUsageDay {
                UtcDate = _day.UtcDate,
                Requests = _day.Requests, EstimatedUnits = _day.EstimatedUnits,
                UploadedBytes = _day.UploadedBytes, DownloadedBytes = _day.DownloadedBytes
            };
        }
    }

    public void RecordRequest(HttpMethod method, string url, bool sessionChunk = false)
    {
        lock (_gate)
        {
            RotateIfNeeded();
            _day.Requests++;
            _day.EstimatedUnits += EstimateUnits(method, url, sessionChunk);
            SaveLocked();
        }
    }

    public void RecordTransfer(TransferDirection direction, long bytes)
    {
        if (bytes <= 0) return;
        lock (_gate)
        {
            RotateIfNeeded();
            if (direction == TransferDirection.Upload) _day.UploadedBytes += bytes;
            else _day.DownloadedBytes += bytes;
            SaveLocked();
        }
    }

    // Google Drive API v3 method categories (May 2026). Session chunk cost is
    // not estimated separately because Google does not publish per-chunk units.
    public static int EstimateUnits(HttpMethod method, string url, bool sessionChunk = false)
    {
        if (sessionChunk) return 0;
        var uri = new Uri(url);
        string path = uri.AbsolutePath;
        if (path.Contains("/changes/startPageToken", StringComparison.Ordinal)) return 5;
        if (path.Contains("/changes", StringComparison.Ordinal)) return 100;
        if (path.EndsWith("/about", StringComparison.Ordinal)) return 5;
        if (path.Contains("/files", StringComparison.Ordinal))
        {
            if (method == HttpMethod.Get)
            {
                if (uri.Query.Contains("alt=media", StringComparison.OrdinalIgnoreCase)) return 200;
                if (path.EndsWith("/files", StringComparison.Ordinal)) return 100;
                return 5;
            }
            return 50;
        }
        if (method == HttpMethod.Get && path.Contains("/permissions", StringComparison.Ordinal)) return 100;
        if (method == HttpMethod.Get && path.Contains("/revisions", StringComparison.Ordinal)) return 100;
        return method == HttpMethod.Get ? 5 : 50;
    }
}
