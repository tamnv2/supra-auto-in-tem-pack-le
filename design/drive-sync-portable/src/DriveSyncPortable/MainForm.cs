using Microsoft.Win32;

namespace DriveSyncPortable;

public sealed class MainForm : Form
{
    private readonly Panel _viewport = new() { Dock = DockStyle.Fill, BackColor = Theme.Page, AutoScroll = true };
    private readonly RoundedPanel _hero = new() { FillColor = Theme.PaleGreen, StrokeColor = Color.FromArgb(214, 236, 229) };
    private readonly RoundedPanel _account = new();
    private readonly RoundedPanel _local = new();
    private readonly RoundedPanel _remote = new();
    private readonly RoundedPanel _settings = new();
    private readonly RoundedPanel _activity = new();

    private readonly Label _statusTitle = Theme.Text("Chưa kết nối Google", 68, 18, 540, 31, 15, true);
    private readonly Label _statusSubtitle = Theme.Text("Đăng nhập để bắt đầu đồng bộ", 68, 49, 540, 24, 9, false, Theme.Muted);
    private readonly Label _accountName = Theme.Text("Chưa đăng nhập Google", 70, 42, 480, 24, 10, true);
    private readonly Label _accountDesc = Theme.Text("OAuth qua trình duyệt mặc định • Không nhập mật khẩu trong ứng dụng", 70, 64, 480, 20, 8, false, Theme.Muted);
    private readonly Label _localPath = Theme.Text("Documents\\DriveSync", 18, 54, 350, 31, 10, true);
    private readonly Label _remotePath = Theme.Text("My Drive / DriveSync", 18, 54, 350, 31, 10, true);
    private readonly RoundedButton _pause = new("⏸  Tạm dừng") { Size = new Size(128, 38) };
    private readonly RoundedButton _login = new("Đăng nhập Google")
        { Size = new Size(155, 38), FillColor = Theme.PaleBlue, TextColor = Theme.Blue, Outlined = false };
    private readonly RoundedButton _localChoose = new("Chọn thư mục") { Size = new Size(200, 36) };
    private readonly RoundedButton _remoteChoose = new("Chọn thư mục") { Size = new Size(200, 36) };
    private readonly RoundedButton _background = new("▣  Chạy ngầm")
        { FillColor = Theme.Blue, TextColor = Theme.White, Outlined = false, Size = new Size(200, 40) };
    private readonly RoundedButton _togglePause = new("⏸  Tạm dừng") { Size = new Size(130, 40) };
    private readonly ToggleSwitch _startup = new();
    private readonly NotifyIcon _tray;
    private readonly Label _footer = Theme.Text("BẢN DEMO UI · Chưa kết nối Google hoặc đồng bộ dữ liệu thật",
        0, 0, 810, 35, 8, false, Theme.Muted);
    private readonly Label _folderHeader = Theme.Text("Thư mục đồng bộ", 0, 0, 400, 28, 10, true);
    private readonly Label _folderArrow = Theme.Text("⇆  Đồng bộ tự động hai chiều", 0, 0, 600, 25, 9, true, Theme.Green);
    private readonly Label _privacy = Theme.Text("🔒  Đăng nhập qua Google OAuth · Token được bảo vệ theo tài khoản Windows ở bản hoàn chỉnh",
        0, 0, 900, 23, 8, false, Theme.Muted);
    private bool _paused;
    private bool _exitRequested;
    private bool _backgroundMode;
    private bool _updatingStartup;

    public MainForm()
    {
        Text = "DriveSync Portable — Bản duyệt giao diện";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 836);
        MinimumSize = new Size(805, 690);
        BackColor = Theme.Page;
        Font = new Font("Segoe UI", 9);
        Icon = SystemIcons.Application;
        AutoScaleMode = AutoScaleMode.Dpi;

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DriveSync Portable (UI Demo)",
            Visible = false
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Mở DriveSync", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Tạm dừng / Tiếp tục (mô phỏng)", null, (_, _) => TogglePause());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Thoát", null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        var header = MakeHeader();
        Controls.Add(_viewport);
        Controls.Add(header);

        _viewport.Controls.AddRange(new Control[]
        {
            _hero, _account, _folderHeader, _local, _remote, _folderArrow,
            _settings, _activity, _privacy, _footer
        });

        BuildHero();
        BuildAccount();
        BuildFolderCards();
        BuildSettings();
        BuildActivity();

        _pause.Click += (_, _) => TogglePause();
        _togglePause.Click += (_, _) => TogglePause();
        _login.Click += (_, _) => ExplainUnavailable(
            "Đăng nhập Google",
            "Giao diện đã dựng theo bản anh duyệt. Bản UI này CHƯA tích hợp Google OAuth và chưa cấp quyền Google Drive. Không yêu cầu nhập mật khẩu ở đây.");
        _remoteChoose.Click += (_, _) => ExplainUnavailable(
            "Chọn thư mục Google Drive",
            "Chức năng duyệt thư mục Google Drive chỉ hoạt động sau khi tích hợp OAuth và Google Drive API.");
        _localChoose.Click += (_, _) => ChooseLocalFolder();
        _background.Click += (_, _) => HideToTray();
        _startup.CheckedChanged += (_, _) => ChangeStartup();

        _startup.Checked = ReadStartup();
        Resize += (_, _) => LayoutCards();
        Shown += (_, _) => LayoutCards();
        LayoutCards();
    }

    private Panel MakeHeader()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.White };
        var brand = Theme.Text("↻", 26, 17, 47, 47, 24, true, Theme.Blue);
        brand.BackColor = Theme.PaleBlue;
        brand.TextAlign = ContentAlignment.MiddleCenter;
        top.Controls.Add(brand);
        top.Controls.Add(Theme.Text("DriveSync Portable", 86, 18, 350, 28, 17, true));
        top.Controls.Add(Theme.Text("Google Drive · Windows Desktop", 88, 48, 370, 20, 8, false, Theme.Muted));
        var trial = Theme.Text("BẢN DUYỆT GIAO DIỆN v0.1", 0, 27, 235, 27, 8, true, Theme.Blue);
        trial.TextAlign = ContentAlignment.MiddleCenter;
        trial.BackColor = Theme.PaleBlue;
        trial.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Controls.Add(trial);
        top.Resize += (_, _) => trial.Left = Math.Max(450, top.Width - 265);
        return top;
    }

    private void BuildHero()
    {
        var circle = Theme.Text("○", 20, 23, 44, 44, 26, true, Theme.Green);
        circle.BackColor = Color.FromArgb(216, 240, 230);
        circle.TextAlign = ContentAlignment.MiddleCenter;
        _hero.Controls.Add(circle);
        _hero.Controls.Add(_statusTitle);
        _hero.Controls.Add(_statusSubtitle);
        _hero.Controls.Add(_pause);
    }

    private void BuildAccount()
    {
        _account.Controls.Add(Theme.Text("Tài khoản Google", 20, 12, 280, 26, 10, true));
        var avatar = Theme.Text("G", 20, 48, 40, 40, 17, true, Theme.White);
        avatar.TextAlign = ContentAlignment.MiddleCenter;
        avatar.BackColor = Theme.Blue;
        _account.Controls.Add(avatar);
        _account.Controls.Add(_accountName);
        _account.Controls.Add(_accountDesc);
        _account.Controls.Add(_login);
    }

    private void BuildFolderCards()
    {
        _local.Controls.Add(Theme.Text("▣  TRÊN LAPTOP", 18, 13, 270, 24, 8, true, Theme.Muted));
        _local.Controls.Add(_localPath);
        _local.Controls.Add(_localChoose);
        _remote.Controls.Add(Theme.Text("☁  GOOGLE DRIVE", 18, 13, 270, 24, 8, true, Theme.Muted));
        _remote.Controls.Add(_remotePath);
        _remote.Controls.Add(_remoteChoose);
        _folderArrow.TextAlign = ContentAlignment.MiddleCenter;
    }

    private void BuildSettings()
    {
        _settings.Controls.Add(Theme.Text("Thiết lập nhanh", 20, 15, 280, 25, 10, true));
        _settings.Controls.Add(Theme.Text("Tự khởi động cùng Windows", 20, 58, 280, 22, 10, true));
        _settings.Controls.Add(Theme.Text("Chỉ sử dụng quyền user hiện tại", 20, 81, 280, 18, 8, false, Theme.Muted));
        _settings.Controls.Add(_startup);
        var separator = new Panel { Height = 1, BackColor = Theme.Line };
        separator.SetBounds(20, 109, 330, 1);
        separator.Name = "separator";
        _settings.Controls.Add(separator);
        _settings.Controls.Add(Theme.Text("Chạy nền trên taskbar", 20, 114, 320, 21, 9, true));
        _settings.Controls.Add(Theme.Text("Đóng cửa sổ, giao diện vẫn ở khay hệ thống", 20, 136, 350, 18, 8, false, Theme.Muted));
        _settings.Controls.Add(_background);
        _settings.Controls.Add(_togglePause);
    }

    private void BuildActivity()
    {
        _activity.Controls.Add(Theme.Text("Hoạt động gần đây", 20, 15, 260, 25, 10, true));
        var demo = Theme.Text("DỮ LIỆU MẪU", 0, 15, 130, 23, 8, true, Theme.Muted);
        demo.Name = "demoBadge";
        demo.TextAlign = ContentAlignment.MiddleRight;
        _activity.Controls.Add(demo);
        _activity.Controls.Add(Theme.Text("↑", 20, 57, 28, 24, 13, true, Theme.Green));
        _activity.Controls.Add(Theme.Text("BaoCao.xlsx · Đã tải lên", 50, 57, 290, 24, 9));
        _activity.Controls.Add(Theme.Text("↓", 20, 96, 28, 24, 13, true, Theme.Blue));
        _activity.Controls.Add(Theme.Text("KeHoach.pdf · Đã tải về", 50, 96, 290, 24, 9));
        _activity.Controls.Add(Theme.Text("✓", 20, 135, 28, 24, 13, true, Theme.Green));
        _activity.Controls.Add(Theme.Text("Không còn file đang chờ", 50, 135, 290, 24, 9));
        var log = new RoundedButton("Xem nhật ký") { Size = new Size(136, 37), Name = "logButton" };
        log.Click += (_, _) => ExplainUnavailable("Nhật ký đồng bộ",
            "Các dòng ở trên là dữ liệu minh họa. Bản UI hiện chưa chạy engine đồng bộ nên chưa có nhật ký thật.");
        _activity.Controls.Add(log);
    }

    private void LayoutCards()
    {
        if (_viewport.IsDisposed) return;
        int w = Math.Max(740, _viewport.ClientSize.Width - 48);
        int x = 24;
        int y = 20;
        _hero.SetBounds(x, y, w, 96);
        _pause.SetBounds(w - 150, 29, 128, 38);
        _statusTitle.Width = Math.Max(250, w - 250);
        _statusSubtitle.Width = Math.Max(250, w - 250);
        y += 112;
        _account.SetBounds(x, y, w, 106);
        _login.SetBounds(w - 177, 49, 155, 36);
        _accountName.Width = Math.Max(250, w - 290);
        _accountDesc.Width = Math.Max(250, w - 290);
        y += 121;
        _folderHeader.SetBounds(x + 2, y, w - 4, 29);
        y += 33;
        int half = (w - 14) / 2;
        _local.SetBounds(x, y, half, 141);
        _remote.SetBounds(x + half + 14, y, w - half - 14, 141);
        _localPath.Width = Math.Max(200, half - 36);
        _remotePath.Width = Math.Max(200, w - half - 50);
        _localChoose.SetBounds(18, 94, half - 36, 34);
        _remoteChoose.SetBounds(18, 94, w - half - 50, 34);
        y += 146;
        _folderArrow.SetBounds(x, y, w, 26);
        y += 33;
        _settings.SetBounds(x, y, half, 221);
        _activity.SetBounds(x + half + 14, y, w - half - 14, 221);
        _startup.Location = new Point(half - 67, 60);
        _background.SetBounds(20, 166, Math.Max(120, half - 184), 38);
        _togglePause.SetBounds(half - 152, 166, 132, 38);
        if (_settings.Controls["separator"] is Panel sep) sep.Width = half - 40;
        if (_activity.Controls["demoBadge"] is Label demo)
            demo.Left = Math.Max(220, _activity.Width - 160);
        if (_activity.Controls["logButton"] is RoundedButton log)
            log.SetBounds(20, 174, 134, 33);
        y += 236;
        _privacy.SetBounds(x + 4, y, w - 4, 26);
        y += 31;
        _footer.SetBounds(x + 4, y, w - 4, 28);
        _viewport.AutoScrollMinSize = new Size(w + 48, y + 48);
    }

    private void ChooseLocalFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn thư mục laptop để đồng bộ (chỉ chọn, chưa upload dữ liệu)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _localPath.Text = dialog.SelectedPath;
        _localPath.AccessibleDescription = dialog.SelectedPath;
        MessageBox.Show(this,
            "Đã chọn thư mục trên laptop.\n\nĐây là bản duyệt giao diện. Chưa có file nào được tải lên hoặc tải xuống Google Drive.",
            "DriveSync Portable — UI Demo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pause.Text = _paused ? "▶  Tiếp tục" : "⏸  Tạm dừng";
        _togglePause.Text = _pause.Text;
        _statusTitle.Text = _paused ? "Đã tạm dừng (giao diện)" : "Chưa kết nối Google";
        _statusSubtitle.Text = _paused ? "Chưa có engine đồng bộ đang chạy" : "Đăng nhập để bắt đầu đồng bộ";
        _hero.FillColor = _paused ? Color.FromArgb(255, 247, 232) : Theme.PaleGreen;
        _hero.Invalidate();
    }

    private void HideToTray()
    {
        _backgroundMode = true;
        _tray.Visible = true;
        Hide();
        _tray.ShowBalloonTip(1700, "DriveSync Portable — Bản UI",
            "Đã thu nhỏ vào khay hệ thống. Chưa đồng bộ file thật.", ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private static string RunKeyName => @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "DriveSyncPortableUI";

    private static bool ReadStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyName, false);
            var value = key?.GetValue(RunValue) as string;
            return value == "\"" + Application.ExecutablePath + "\"";
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
            else
                key.DeleteValue(RunValue, false);
        }
        catch (Exception ex)
        {
            _updatingStartup = true;
            _startup.Checked = !_startup.Checked;
            _updatingStartup = false;
            MessageBox.Show(this, "Không thể thay đổi tự khởi động. Có thể máy đang hạn chế bằng chính sách IT.\n\n" +
                ex.Message, "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExplainUnavailable(string title, string details)
    {
        MessageBox.Show(this, details, title + " — Chưa triển khai",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && _backgroundMode && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }
}
