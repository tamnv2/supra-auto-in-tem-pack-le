namespace DriveSyncPortable;

internal sealed record DriveStorageQuota(long? Limit, long Usage, long UsageInDrive);

public sealed partial class MainForm
{
    private readonly RoundedPanel _quotaCard = new() { FillColor = Theme.White };
    private readonly Label _quotaStorage = Theme.Text("Dung lượng Drive: chưa tải", 18, 34, 550, 23, 10, true);
    private readonly Label _quotaApi = Theme.Text("API hôm nay: chưa có dữ liệu", 18, 59, 580, 20, 9, false, Theme.Ink);
    private readonly Label _quotaNote = Theme.Text(
        "API Units là ước tính riêng EXE, không phải số liệu toàn dự án Google Cloud.",
        18, 82, 900, 19, 8, false, Theme.Muted);
    private readonly RoundedButton _quotaRefresh = new("Làm mới quota")
        { Size = new Size(142, 32) };
    private System.Windows.Forms.Timer? _quotaTimer;
    private DateTimeOffset _lastQuotaFetch = DateTimeOffset.MinValue;
    private bool _quotaFetching;

    private void BuildQuotaCard()
    {
        _quotaCard.Controls.Add(Theme.Text("QUOTA GOOGLE DRIVE", 18, 8, 260, 22, 9, true, Theme.Muted));
        _quotaCard.Controls.AddRange(new Control[] {
            _quotaStorage, _quotaApi, _quotaNote, _quotaRefresh
        });
        _quotaRefresh.Click += async (_, _) => await RefreshQuotaAsync(true);
    }

    private void StartQuotaMonitor()
    {
        _quotaTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _quotaTimer.Tick += async (_, _) => await RefreshQuotaAsync(false);
        _quotaTimer.Start();
        RefreshQuotaLocal();
    }

    private void RefreshQuotaLocal()
    {
        var day = _api?.Usage.Snapshot();
        if (day is null)
        {
            _quotaApi.Text = "API hôm nay: chưa có dữ liệu";
            return;
        }
        _quotaApi.Text = "API EXE hôm nay (UTC): " + day.Requests.ToString("N0")
            + " request · ~" + day.EstimatedUnits.ToString("N0") + " units"
            + " · ↑" + TransferFormat.Size(day.UploadedBytes)
            + " ↓" + TransferFormat.Size(day.DownloadedBytes);
        _quotaApi.AccessibleDescription =
            "Đơn vị quota chỉ ước tính từ các lệnh do ứng dụng này phát ra; không bao gồm phần mềm khác hay quota dự án.";
    }

    private async Task RefreshQuotaAsync(bool force)
    {
        RefreshQuotaLocal();
        if (_quotaFetching || _api is null || !_api.IsSignedIn) return;
        if (!force && DateTimeOffset.UtcNow - _lastQuotaFetch < TimeSpan.FromMinutes(30)) return;
        _quotaFetching = true;
        _quotaRefresh.Enabled = false;
        try
        {
            var storage = await _api.ReadStorageQuotaAsync(CancellationToken.None);
            string pct = storage.Limit is > 0
                ? " (" + (100.0 * storage.Usage / storage.Limit.Value).ToString("0.0") + "%)"
                : "";
            string capacity = storage.Limit is > 0
                ? TransferFormat.Size(storage.Limit.Value)
                : "không được cung cấp";
            _quotaStorage.Text = "Dung lượng thực tế: " + TransferFormat.Size(storage.Usage)
                + " / " + capacity + pct
                + " · Drive: " + TransferFormat.Size(storage.UsageInDrive);
            _lastQuotaFetch = DateTimeOffset.UtcNow;
        }
        catch (Exception)
        {
            if (_lastQuotaFetch == DateTimeOffset.MinValue)
                _quotaStorage.Text = "Dung lượng Drive: chưa đọc được (kiểm tra kết nối/quyền Google)";
        }
        finally
        {
            _quotaFetching = false;
            _quotaRefresh.Enabled = true;
            RefreshQuotaLocal();
        }
    }
}
