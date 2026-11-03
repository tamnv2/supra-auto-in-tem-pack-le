using Microsoft.Win32;

namespace DriveSyncPortable;

public sealed partial class MainForm : Form
{
    private readonly Panel _viewport = new() { Dock = DockStyle.Fill, BackColor = Theme.Page, AutoScroll = true };
    private readonly RoundedPanel _hero = new() { FillColor = Theme.PaleGreen, StrokeColor = Theme.Line };
    private readonly RoundedPanel _upCard = new() { FillColor = Theme.White };
    private readonly RoundedPanel _downCard = new() { FillColor = Theme.White };
    private readonly RoundedPanel _account = new();
    private readonly RoundedPanel _local = new();
    private readonly RoundedPanel _remote = new();
    private readonly RoundedPanel _settings = new();
    private readonly RoundedPanel _activity = new();

    private readonly Label _statusSymbol = Theme.Text("○", 18, 17, 42, 43, 24, true, Theme.Green);
    private readonly Label _statusTitle = Theme.Text("Chưa kết nối Google", 69, 11, 550, 30, 15, true);
    private readonly Label _statusSubtitle = Theme.Text("Đăng nhập và chọn thư mục để bắt đầu", 69, 39, 550, 24, 9, false, Theme.Muted);
    private readonly Label _upSpeed = Theme.Text("0 B/s", 20, 23, 290, 30, 16, true, Theme.Blue);
    private readonly Label _downSpeed = Theme.Text("0 B/s", 20, 23, 290, 30, 16, true, Theme.Green);
    private readonly Label _upInfo = Theme.Text("Chưa có tệp đang tải lên", 175, 28, 270, 24, 8, false, Theme.Muted);
    private readonly Label _downInfo = Theme.Text("Chưa có tệp đang tải xuống", 175, 28, 270, 24, 8, false, Theme.Muted);

    private readonly Label _accountName = Theme.Text("Chưa đăng nhập Google", 68, 30, 510, 24, 10, true);
    private readonly Label _accountDesc = Theme.Text("Đăng nhập Google qua trình duyệt mặc định", 68, 50, 510, 19, 8, false, Theme.Muted);
    private readonly Label _localPath = Theme.Text("Chưa chọn thư mục", 16, 26, 350, 26, 9, true);
    private readonly Label _remotePath = Theme.Text("Chưa chọn thư mục", 16, 26, 350, 26, 9, true);

    private readonly RoundedButton _pause = new("⏸  Tạm dừng") { Size = new Size(120, 36) };
    private readonly RoundedButton _login = new("Đăng nhập Google")
        { Size = new Size(149, 34), FillColor = Theme.PaleBlue, TextColor = Theme.Blue, Outlined = false };
    private readonly RoundedButton _localChoose = new("Chọn thư mục") { Size = new Size(125, 33) };
    private readonly RoundedButton _remoteChoose = new("Chọn thư mục") { Size = new Size(125, 33) };
    private readonly RoundedButton _background = new("▣  Chạy ngầm")
        { FillColor = Theme.Blue, TextColor = Theme.White, Outlined = false, Size = new Size(138, 36) };
    private readonly RoundedButton _togglePause = new("⏸  Tạm dừng") { Size = new Size(125, 36) };
    private readonly RoundedButton _exitButton = new("⏻  Thoát ứng dụng")
        { FillColor = Color.FromArgb(255, 243, 240), TextColor = Color.FromArgb(159, 58, 48),
          Outlined = false, Size = new Size(152, 36) };
    private readonly ToggleSwitch _startup = new();
    private readonly NotifyIcon _tray;
    private readonly Label _footer = Theme.Text("DriveSync Portable · Google OAuth · Token bảo vệ bởi Windows",
        0, 0, 820, 20, 8, false, Theme.Muted);
    private readonly Label _folderHeader = Theme.Text("Thư mục đồng bộ hai chiều", 0, 0, 400, 21, 10, true);
    private readonly Label _privacy = Theme.Text("Đóng cửa sổ bằng X → chạy ngầm. Chỉ nút Thoát mới dừng ứng dụng.",
        0, 0, 900, 22, 8, false, Theme.Muted);
    private readonly ListView _historyList = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false,
        HideSelection = false, GridLines = false, BorderStyle = BorderStyle.None,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, Font = new Font("Segoe UI", 9),
        BackColor = Theme.White, ForeColor = Theme.Ink
    };
    private readonly Label _activityCount = Theme.Text("0 hoạt động", 0, 11, 180, 22, 8, false, Theme.Muted);
    private bool _paused;
    private bool _exitRequested;
    private bool _exiting;
    private bool _trayTipShown;
    private bool _updatingStartup;
    private readonly List<string> _activityHistory = new();

    public MainForm()
    {
        Text = "DriveSync Portable — Đồng bộ Google Drive";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(970, 838);
        MinimumSize = new Size(825, 660);
        BackColor = Theme.Page;
        Font = new Font("Segoe UI", 9);
        Icon = AppIcon.Create();
        AutoScaleMode = AutoScaleMode.Dpi;

        _tray = new NotifyIcon { Icon = Icon, Text = "DriveSync Portable", Visible = false };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Mở DriveSync", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Tạm dừng / Tiếp tục", null, (_, _) => TogglePause());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Thoát ứng dụng (xác nhận 2 lần)", null, async (_, _) => await RequestExitAsync());
        _tray.ContextMenuStrip = trayMenu;
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        Controls.Add(_viewport);
        Controls.Add(MakeHeader());
        _viewport.Controls.AddRange(new Control[] {
            _hero, _upCard, _downCard, _account, _folderHeader, _local, _remote,
            _settings, _activity, _privacy, _footer
        });

        BuildHero();
        BuildSpeedCards();
        BuildAccount();
        BuildFolders();
        BuildSettings();
        BuildHistory();

        _pause.Click += (_, _) => TogglePause();
        _togglePause.Click += (_, _) => TogglePause();
        _login.Click += async (_, _) => await SignInGoogleAsync();
        _localChoose.Click += async (_, _) => await ChooseLocalFolderAsync();
        _remoteChoose.Click += async (_, _) => await ChooseRemoteFolderAsync();
        _background.Click += (_, _) => HideToTray();
        _exitButton.Click += async (_, _) => await RequestExitAsync();
        _startup.CheckedChanged += (_, _) => ChangeStartup();

        _startup.Checked = ReadStartup();
        Resize += (_, _) => LayoutCards();
        Shown += (_, _) => LayoutCards();
        LayoutCards();
        InitializeSync();
    }

    private Panel MakeHeader()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 63, BackColor = Theme.White };
        var logo = Theme.Text("↻", 18, 12, 39, 39, 23, true, Theme.Blue);
        logo.BackColor = Theme.PaleBlue;
        logo.TextAlign = ContentAlignment.MiddleCenter;
        top.Controls.Add(logo);
        top.Controls.Add(Theme.Text("DriveSync Portable", 69, 9, 360, 26, 16, true));
        top.Controls.Add(Theme.Text("Google Drive · Windows Desktop", 70, 34, 350, 17, 8, false, Theme.Muted));
        var version = Theme.Text("v0.3 · ĐỒNG BỘ HAI CHIỀU", 0, 19, 225, 26, 8, true, Theme.Blue);
        version.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        version.TextAlign = ContentAlignment.MiddleCenter;
        version.BackColor = Theme.PaleBlue;
        top.Controls.Add(version);
        top.Resize += (_, _) => version.Left = Math.Max(430, top.Width - 247);
        return top;
    }

    private void BuildHero()
    {
        _statusSymbol.BackColor = Color.FromArgb(220, 242, 231);
        _statusSymbol.TextAlign = ContentAlignment.MiddleCenter;
        _hero.Controls.AddRange(new Control[] { _statusSymbol, _statusTitle, _statusSubtitle, _pause });
    }

    private void BuildSpeedCards()
    {
        _upCard.Controls.Add(Theme.Text("↑  TỐC ĐỘ TẢI LÊN", 17, 6, 250, 18, 8, true, Theme.Muted));
        _upCard.Controls.Add(_upSpeed);
        _upCard.Controls.Add(_upInfo);
        _downCard.Controls.Add(Theme.Text("↓  TỐC ĐỘ TẢI XUỐNG", 17, 6, 250, 18, 8, true, Theme.Muted));
        _downCard.Controls.Add(_downSpeed);
        _downCard.Controls.Add(_downInfo);
    }

    private void BuildAccount()
    {
        _account.Controls.Add(Theme.Text("TÀI KHOẢN GOOGLE", 18, 5, 280, 20, 8, true, Theme.Muted));
        var circle = Theme.Text("G", 19, 30, 36, 36, 17, true, Theme.White);
        circle.BackColor = Theme.Blue;
        circle.TextAlign = ContentAlignment.MiddleCenter;
        _account.Controls.AddRange(new Control[] { circle, _accountName, _accountDesc, _login });
    }

    private void BuildFolders()
    {
        _local.Controls.Add(Theme.Text("▣  TRÊN LAPTOP", 16, 7, 280, 18, 8, true, Theme.Muted));
        _local.Controls.AddRange(new Control[] { _localPath, _localChoose });
        _remote.Controls.Add(Theme.Text("☁  TRÊN GOOGLE DRIVE", 16, 7, 280, 18, 8, true, Theme.Muted));
        _remote.Controls.AddRange(new Control[] { _remotePath, _remoteChoose });
    }

    private void BuildSettings()
    {
        _settings.Controls.Add(Theme.Text("Thiết lập nhanh", 18, 8, 195, 24, 10, true));
        _settings.Controls.Add(Theme.Text("Tự khởi động Windows", 18, 37, 240, 21, 9, true));
        _settings.Controls.Add(_startup);
        _settings.Controls.AddRange(new Control[] { _background, _togglePause, _exitButton });
    }

    private void BuildHistory()
    {
        _activity.Controls.Add(Theme.Text("Lịch sử đồng bộ", 18, 10, 300, 25, 11, true));
        _activityCount.TextAlign = ContentAlignment.MiddleRight;
        _activity.Controls.Add(_activityCount);

        _historyList.Columns.Add("Thời gian", 100);
        _historyList.Columns.Add("Thao tác", 155);
        _historyList.Columns.Add("Tệp / nội dung", 495);
        _historyList.Columns.Add("Kết quả", 100);
        _activity.Controls.Add(_historyList);
    }

    private void LayoutCards()
    {
        if (_viewport.IsDisposed) return;
        const int x = 15;
        int w = Math.Max(775, _viewport.ClientSize.Width - 30);
        int half = (w - 10) / 2;
        int y = 12;

        _hero.SetBounds(x, y, w, 77);
        _pause.SetBounds(w - 140, 22, 125, 34);
        _statusTitle.Width = Math.Max(320, w - 270);
        _statusSubtitle.Width = Math.Max(320, w - 270);

        y += 85;
        _upCard.SetBounds(x, y, half, 63);
        _downCard.SetBounds(x + half + 10, y, w - half - 10, 63);
        _upInfo.SetBounds(174, 26, Math.Max(100, half - 190), 26);
        _downInfo.SetBounds(174, 26, Math.Max(100, w - half - 200), 26);

        y += 71;
        _account.SetBounds(x, y, w, 78);
        _accountName.Width = Math.Max(340, w - 270);
        _accountDesc.Width = Math.Max(340, w - 270);
        _login.SetBounds(w - 163, 30, 149, 34);

        y += 86;
        _folderHeader.SetBounds(x, y, w, 22);
        y += 25;
        _local.SetBounds(x, y, half, 68);
        _remote.SetBounds(x + half + 10, y, w - half - 10, 68);
        _localPath.Width = Math.Max(130, half - 162);
        _remotePath.Width = Math.Max(130, w - half - 170);
        _localChoose.SetBounds(half - 138, 28, 126, 31);
        _remoteChoose.SetBounds(w - half - 148, 28, 126, 31);

        y += 76;
        _settings.SetBounds(x, y, w, 72);
        _startup.Location = new Point(228, 36);
        _background.SetBounds(w - 452, 29, 136, 34);
        _togglePause.SetBounds(w - 309, 29, 127, 34);
        _exitButton.SetBounds(w - 174, 29, 158, 34);

        y += 80;
        int historyHeight = Math.Max(240, _viewport.ClientSize.Height - y - 68);
        _activity.SetBounds(x, y, w, historyHeight);
        _activityCount.SetBounds(w - 195, 11, 175, 22);
        _historyList.SetBounds(12, 41, w - 24, historyHeight - 53);
        int available = Math.Max(600, _historyList.Width - SystemInformation.VerticalScrollBarWidth - 6);
        _historyList.Columns[0].Width = 95;
        _historyList.Columns[1].Width = 144;
        _historyList.Columns[3].Width = 98;
        _historyList.Columns[2].Width = Math.Max(220, available - 337);

        y += historyHeight + 7;
        _privacy.SetBounds(x + 4, y, w - 8, 19);
        _footer.SetBounds(x + 4, y + 20, w - 8, 19);
        _viewport.AutoScrollMinSize = new Size(w + 30, y + 45);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _sync?.Pause(_paused);
        _pause.Text = _paused ? "▶  Tiếp tục" : "⏸  Tạm dừng";
        _togglePause.Text = _pause.Text;
        ApplySyncStatus(new SyncStatus(_paused ? SyncState.Paused : SyncState.Checking,
            _paused ? "Đã tạm dừng" : "Đang kiểm tra",
            _paused ? "Lượt đang tải có thể hoàn tất; tệp mới sẽ chờ tiếp tục"
                    : "Đang kiểm tra các thay đổi còn chờ"));
    }

    private void ApplySyncStatus(SyncStatus status)
    {
        if (_paused && status.State != SyncState.Paused && status.State != SyncState.Stopping) return;
        Color accent;
        Color background;
        string symbol;
        switch (status.State)
        {
            case SyncState.Synced:
                accent = Theme.Green; background = Theme.PaleGreen; symbol = "✓"; break;
            case SyncState.Uploading:
                accent = Theme.Blue; background = Theme.PaleBlue; symbol = "↑"; break;
            case SyncState.Downloading:
                accent = Theme.Blue; background = Theme.PaleBlue; symbol = "↓"; break;
            case SyncState.Paused:
                accent = Color.FromArgb(162, 109, 23);
                background = Color.FromArgb(255, 247, 230); symbol = "Ⅱ"; break;
            case SyncState.WaitingNetwork:
                accent = Color.FromArgb(162, 109, 23);
                background = Color.FromArgb(255, 247, 230); symbol = "↻"; break;
            case SyncState.NeedsAttention:
            case SyncState.AuthenticationError:
            case SyncState.FolderUnavailable:
                accent = Color.FromArgb(179, 66, 54);
                background = Color.FromArgb(255, 239, 236); symbol = "!"; break;
            default:
                accent = Theme.Blue; background = Theme.PaleBlue; symbol = "○"; break;
        }
        _hero.FillColor = background;
        _hero.Invalidate();
        _statusSymbol.Text = symbol;
        _statusSymbol.ForeColor = accent;
        _statusSymbol.BackColor = background;
        _statusTitle.Text = status.Title;
        _statusSubtitle.Text = status.Detail;
        string trayText = "DriveSync: " + status.Title;
        _tray.Text = trayText.Length <= 63 ? trayText : trayText[..63];
    }

    private void ApplyTransfer(TransferProgress value)
    {
        Label speed = value.Direction == TransferDirection.Upload ? _upSpeed : _downSpeed;
        Label info = value.Direction == TransferDirection.Upload ? _upInfo : _downInfo;
        speed.Text = value.Completed ? "0 B/s" : TransferFormat.Speed(value.BytesPerSecond);
        if (value.Completed)
        {
            info.Text = "Đã kết thúc lượt truyền";
            return;
        }
        string progress = value.TotalBytes > 0
            ? Math.Min(100, Math.Round(value.BytesTransferred * 100.0 / value.TotalBytes, 0)).ToString("0") + "%"
            : TransferFormat.Size(value.BytesTransferred);
        info.Text = value.FileName + " · " + progress;
    }

    private void HideToTray()
    {
        _tray.Visible = true;
        Hide();
        if (!_trayTipShown)
        {
            _tray.ShowBalloonTip(1600, "DriveSync đang chạy ngầm",
                "Nhấn đúp biểu tượng để mở lại. Thoát hẳn tại nút Thoát ứng dụng.", ToolTipIcon.Info);
            _trayTipShown = true;
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private async Task RequestExitAsync()
    {
        if (_exiting) return;
        var first = MessageBox.Show(this,
            "Thoát sẽ dừng đồng bộ và đóng ứng dụng.\nCác file chưa xong sẽ xử lý lại khi mở ứng dụng.\n\nAnh muốn tiếp tục?",
            "Xác nhận thoát (1/2)", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (first != DialogResult.Yes) return;
        var second = MessageBox.Show(this,
            "Xác nhận lần cuối: THOÁT HẲN DriveSync?\nỨng dụng sẽ không còn chạy ngầm.",
            "Xác nhận thoát (2/2)", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (second != DialogResult.Yes) return;
        _exiting = true;
        _exitButton.Enabled = false;
        _login.Enabled = false;
        ApplySyncStatus(new SyncStatus(SyncState.Stopping, "Đang thoát", "Dừng lượt đồng bộ an toàn"));
        try
        {
            await StopSyncAsync();
            _exitRequested = true;
            Close();
        }
        catch (Exception ex)
        {
            _exiting = false;
            _exitButton.Enabled = true;
            _login.Enabled = true;
            MessageBox.Show(this, "Không dừng đồng bộ được: " + ex.GetType().Name,
                "Không thể thoát", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string RunKeyName => @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "DriveSyncPortableUI";

    private static bool ReadStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyName, false);
            return key?.GetValue(RunValue) as string == "\"" + Application.ExecutablePath + "\"";
        }
        catch { return false; }
    }

    private void ChangeStartup()
    {
        if (_updatingStartup) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyName, true)
                ?? throw new InvalidOperationException("Không thể ghi HKCU Run.");
            if (_startup.Checked)
                key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
            else key.DeleteValue(RunValue, false);
        }
        catch (Exception ex)
        {
            _updatingStartup = true;
            _startup.Checked = !_startup.Checked;
            _updatingStartup = false;
            MessageBox.Show(this, "Windows không cho thay đổi tự khởi động.\n" + ex.Message,
                "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _sync?.Dispose();
        _api?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }
}
