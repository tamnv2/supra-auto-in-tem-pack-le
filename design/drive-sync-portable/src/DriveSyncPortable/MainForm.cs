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

    private readonly Label _statusSymbol = Theme.Text("●", 18, 17, 43, 43, 18, true, Theme.Green);
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
    private readonly RoundedButton _preferences = new("Giới hạn / Bộ lọc")
        { Size = new Size(159, 29) };
    private readonly ToggleSwitch _startup = new();
    private readonly NotifyIcon _tray;
    private readonly Label _footer = Theme.Text("DriveSync Portable · Google OAuth · Token bảo vệ bởi Windows",
        0, 0, 820, 20, 8, false, Theme.Muted);
    private readonly Label _folderHeader = Theme.Text("Thư mục đồng bộ hai chiều", 0, 0, 400, 21, 10, true);
    private readonly Label _privacy = Theme.Text("Đóng cửa sổ (X): tiếp tục chạy ngầm · Thoát ứng dụng: xác nhận hai lần.",
        0, 0, 900, 22, 8, false, Theme.Muted);
    private readonly ListView _historyList = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false,
        HideSelection = false, GridLines = false, BorderStyle = BorderStyle.None,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, Font = new Font("Segoe UI", 9.5f),
        BackColor = Theme.White, ForeColor = Theme.Ink,
        ShowItemToolTips = true
    };
    private readonly Label _activityCount = Theme.Text("0 hoạt động", 0, 11, 180, 22, 8, false, Theme.Muted);
    private bool _paused;
    private bool _exitRequested;
    private bool _exiting;
    private bool _trayTipShown;
    private string _lastErrorNotification = "";
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
            _hero, _upCard, _downCard, _quotaCard, _account, _folderHeader, _local, _remote,
            _settings, _activity, _privacy, _footer
        });

        BuildHero();
        BuildSpeedCards();
        BuildQuotaCard();
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
        _preferences.Click += async (_, _) =>
        {
            if (_config is null) return;
            using var dialog = new SyncPreferencesDialog(_config.Data);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _config.Data.UploadLimitKiBps = dialog.UploadLimitKiBps;
            _config.Data.DownloadLimitKiBps = dialog.DownloadLimitKiBps;
            _config.Data.IgnoredExtensions = dialog.IgnoredExtensions;
            string previousProxy = _config.Data.ProxyUrl;
            _config.Data.ProxyUrl = dialog.ProxyUrl;
            _config.Save();
            await StopSyncAsync();
            if (!string.Equals(previousProxy, dialog.ProxyUrl, StringComparison.Ordinal))
            {
                _api?.Dispose();
                _api = new GoogleDriveApi(_config);
            }
            await StartSyncIfReadyAsync();
        };
        _startup.CheckedChanged += (_, _) => ChangeStartup();

        _startup.Checked = ReadStartup();
        Resize += (_, _) => LayoutCards();
        Shown += (_, _) => LayoutCards();
        LayoutCards();
        InitializeSync();
        StartQuotaMonitor();
    }

    private Panel MakeHeader()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 63, BackColor = Theme.White };
        // Use a real icon image: Unicode arrows were clipped on machines lacking
        // compatible Segoe UI glyph metrics.
        top.Controls.Add(new PictureBox
        {
            Image = Icon.ToBitmap(),
            Location = new Point(18, 13),
            Size = new Size(38, 38),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Theme.White
        });
        top.Controls.Add(Theme.Text("DriveSync Portable", 69, 9, 360, 26, 16, true));
        top.Controls.Add(Theme.Text("Google Drive · Windows Desktop", 70, 34, 350, 17, 8, false, Theme.Muted));
        var version = Theme.Text("v0.4.1 · 1 GMAIL / 1 THƯ MỤC", 0, 19, 225, 26, 8, true, Theme.Blue);
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
        _statusSymbol.AutoEllipsis = false;
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
        _settings.Controls.Add(Theme.Text("Tự khởi động cùng Windows", 18, 37, 240, 21, 9, true));
        _settings.Controls.Add(_startup);
        _settings.Controls.AddRange(new Control[] {
            _preferences, _background, _togglePause, _exitButton
        });
    }

    private void BuildHistory()
    {
        _activity.Controls.Add(Theme.Text("Lịch sử đồng bộ", 18, 10, 300, 25, 11, true));
        _activityCount.TextAlign = ContentAlignment.MiddleRight;
        _activity.Controls.Add(_activityCount);

        _historyList.Columns.Add("Thời gian", 158);
        _historyList.Columns.Add("Thao tác", 155);
        _historyList.Columns.Add("Tệp / nội dung", 495);
        _historyList.Columns.Add("Kết quả", 116);
        _activity.Controls.Add(_historyList);
        _historyList.Resize += (_, _) => _historyList.Invalidate();
    }

    private void LayoutCards()
    {
        if (_viewport.IsDisposed || _viewport.ClientSize.Width < 1) return;
        // Keep the entire dashboard in one visually coherent column on ultra-wide
        // monitors. Every section shares precisely the same left/right alignment.
        const int outerMargin = 18;
        const int gap = 12;
        int availableWidth = Math.Max(0, _viewport.ClientSize.Width);
        int w = Math.Max(735, Math.Min(1328, availableWidth - outerMargin * 2));
        int x = Math.Max(outerMargin, (availableWidth - w) / 2);
        int half = (w - gap) / 2;
        int y = 14;

        _viewport.SuspendLayout();
        try
        {
            _hero.SetBounds(x, y, w, 84);
            _pause.SetBounds(w - 151, 24, 133, 36);
            _statusSymbol.SetBounds(20, 20, 43, 43);
            _statusTitle.SetBounds(76, 14, Math.Max(210, w - 252), 29);
            _statusSubtitle.SetBounds(76, 44, Math.Max(210, w - 252), 24);

            y += 84 + gap;
            _upCard.SetBounds(x, y, half, 76);
            _downCard.SetBounds(x + half + gap, y, w - half - gap, 76);
            _upSpeed.SetBounds(20, 32, 155, 33);
            _downSpeed.SetBounds(20, 32, 155, 33);
            _upInfo.SetBounds(185, 35, Math.Max(90, half - 204), 25);
            _downInfo.SetBounds(185, 35, Math.Max(90, w - half - gap - 204), 25);

            y += 76 + gap;
            _quotaCard.SetBounds(x, y, w, 142);
            _quotaStorage.SetBounds(18, 34, Math.Max(150, w - 210), 22);
            _quotaApi.SetBounds(18, 59, w - 36, 21);
            _quotaNote.SetBounds(18, 84, w - 36, 20);
            _queueInfo.SetBounds(18, 109, w - 36, 21);
            _driveSearch.SetBounds(w - 320, 11, 142, 33);
            _quotaRefresh.SetBounds(w - 165, 11, 147, 33);

            y += 142 + gap;
            _account.SetBounds(x, y, w, 84);
            _accountName.SetBounds(70, 29, Math.Max(180, w - 280), 22);
            _accountDesc.SetBounds(70, 52, Math.Max(180, w - 280), 20);
            _login.SetBounds(w - 166, 32, 147, 35);

            y += 84 + gap;
            _folderHeader.SetBounds(x + 2, y, w - 4, 23);
            y += 28;
            _local.SetBounds(x, y, half, 80);
            _remote.SetBounds(x + half + gap, y, w - half - gap, 80);
            _localPath.SetBounds(18, 38, Math.Max(140, half - 190), 26);
            _remotePath.SetBounds(18, 38, Math.Max(140, w - half - gap - 190), 26);
            _localChoose.SetBounds(half - 148, 35, 129, 34);
            _remoteChoose.SetBounds(w - half - gap - 148, 35, 129, 34);

            y += 80 + gap;
            // At narrow widths the startup toggle and action buttons cannot all
            // fit side by side. Move actions to their own row rather than allowing
            // overlapping controls (seen at ~825px Windows window widths).
            bool compactActions = w < 850;
            int settingsHeight = compactActions ? 119 : 78;
            _settings.SetBounds(x, y, w, settingsHeight);
            _preferences.SetBounds(225, 6, 159, 29);
            _startup.Location = new Point(298, compactActions ? 36 : 40);
            if (compactActions)
            {
                const int actionsWidth = 142 + 12 + 131 + 12 + 154;
                int actionLeft = Math.Max(20, (w - actionsWidth) / 2);
                _background.SetBounds(actionLeft, 76, 142, 35);
                _togglePause.SetBounds(actionLeft + 154, 76, 131, 35);
                _exitButton.SetBounds(actionLeft + 297, 76, 154, 35);
            }
            else
            {
                _background.SetBounds(w - 468, 33, 142, 35);
                _togglePause.SetBounds(w - 316, 33, 131, 35);
                _exitButton.SetBounds(w - 174, 33, 154, 35);
            }

            y += settingsHeight + gap;
            int wantedHistory = 255 + Math.Max(0, _historyList.Items.Count - 7) * 23;
            int roomForHistory = Math.Max(252, _viewport.ClientSize.Height - y - 51);
            int historyHeight = Math.Max(252, Math.Min(440, Math.Min(wantedHistory, roomForHistory)));
            _activity.SetBounds(x, y, w, historyHeight);
            _activityCount.SetBounds(w - 196, 13, 176, 21);
            _historyList.SetBounds(17, 45, w - 34, historyHeight - 60);
            int tableWidth = Math.Max(630, _historyList.Width - SystemInformation.VerticalScrollBarWidth - 6);
            _historyList.Columns[0].Width = 158; // dd/MM/yyyy HH:mm:ss must be visible
            _historyList.Columns[1].Width = 146;
            _historyList.Columns[3].Width = 116;
            _historyList.Columns[2].Width = Math.Max(210, tableWidth - 420);

            y += historyHeight + 11;
            _privacy.SetBounds(x + 2, y, w - 4, 21);
            _footer.Visible = false; // Remove duplicate tiny footer text.
            _viewport.AutoScrollMinSize = new Size(w + outerMargin * 2, y + 35);
        }
        finally
        {
            _viewport.ResumeLayout(true);
        }
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
                accent = Theme.Blue; background = Theme.PaleBlue; symbol = "●"; break;
        }
        _hero.FillColor = background;
        _statusSymbol.Text = symbol;
        _statusSymbol.ForeColor = accent;
        _statusSymbol.BackColor = background;
        _statusTitle.Text = status.Title;
        _statusSubtitle.Text = status.Detail;
        string trayText = "DriveSync: " + status.Title;
        _tray.Text = trayText.Length <= 63 ? trayText : trayText[..63];
        bool error = status.State is SyncState.NeedsAttention or SyncState.AuthenticationError
            or SyncState.FolderUnavailable;
        if (!error)
            _lastErrorNotification = "";
        else if (_lastErrorNotification != status.Title)
        {
            _lastErrorNotification = status.Title;
            _tray.Visible = true;
            _tray.ShowBalloonTip(3500, "DriveSync cần xử lý",
                status.Title + ": " + status.Detail, ToolTipIcon.Warning);
        }
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
        if (!Visible) RestoreFromTray();
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
        _quotaTimer?.Stop();
        _quotaTimer?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }
}
