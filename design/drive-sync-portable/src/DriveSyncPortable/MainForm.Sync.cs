namespace DriveSyncPortable;

public sealed partial class MainForm
{
    private SettingsStore? _config;
    private GoogleDriveApi? _api;
    private SyncEngine? _sync;
    private readonly List<string> _recent = new();
    private bool _settingUp;

    private void InitializeSync()
    {
        try
        {
            _config = new SettingsStore();
            _api = new GoogleDriveApi(_config);
            _localPath.Text = string.IsNullOrWhiteSpace(_config.Data.LocalFolder)
                ? "Chưa chọn thư mục" : _config.Data.LocalFolder;
            _remotePath.Text = string.IsNullOrWhiteSpace(_config.Data.RemoteFolderId)
                ? "Chưa chọn thư mục" : _config.Data.RemoteFolderName;
            _accountName.Text = _api.IsSignedIn ? _config.Data.GoogleEmail : "Chưa đăng nhập Google";
            _accountDesc.Text = _api.IsSignedIn
                ? "Google OAuth · Kết nối được lưu an toàn theo user Windows"
                : "Đăng nhập trên trình duyệt Google · Không nhập mật khẩu vào EXE";
            _login.Text = _api.IsSignedIn ? "Đổi tài khoản" : "Đăng nhập Google";
            _statusTitle.Text = _api.IsSignedIn ? "Sẵn sàng đồng bộ" : "Chưa kết nối Google";
            _statusSubtitle.Text = _api.IsSignedIn
                ? "Chọn hai thư mục hoặc chờ kiểm tra tự động"
                : "Đăng nhập Google để bắt đầu";
            _privacy.Text = "🔒  OAuth trình duyệt Google · Token mã hóa bằng Windows DPAPI";
            Shown += async (_, _) =>
            {
                await Task.Yield();
                if (_config?.Data.MappingApproved == true)
                    await StartSyncIfReadyAsync();
            };
        }
        catch (Exception ex)
        {
            _statusTitle.Text = "Không tải được cấu hình";
            _statusSubtitle.Text = "Đồng bộ đã bị khóa để bảo vệ dữ liệu";
            MessageBox.Show(this, "Không đọc được cấu hình DriveSync, chương trình sẽ không đồng bộ.\n\n" +
                ex.Message, "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SignInGoogleAsync()
    {
        if (_settingUp || _config is null || _api is null) return;
        _settingUp = true;
        _login.Enabled = false;
        try
        {
            if (string.IsNullOrWhiteSpace(_config.Data.ClientId) ||
                ModifierKeys.HasFlag(Keys.Shift))
            {
                using var setup = new GoogleCredentialsDialog();
                if (setup.ShowDialog(this) != DialogResult.OK) return;
                _config.Data.ClientId = setup.ClientId;
                _config.SetClientSecret(setup.ClientSecret);
                _config.Save();
            }

            await StopSyncAsync();
            SetStatus("Đang đăng nhập Google", "Xác thực trên trình duyệt mặc định");
            await _api.SignInAsync(CancellationToken.None);
            _accountName.Text = _config.Data.GoogleEmail;
            _accountDesc.Text = "Google OAuth · Phiên đăng nhập được mã hóa trong Windows";
            _login.Text = "Đổi tài khoản";
            AddActivity("Đã đăng nhập Google");
            SetStatus("Đăng nhập thành công", "Chọn thư mục laptop và Google Drive");
            await StartSyncIfReadyAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Đăng nhập bị hủy", "Bấm đăng nhập để thử lại");
        }
        catch (Exception ex)
        {
            SetStatus("Đăng nhập chưa thành công", "Kiểm tra Client ID và quyền Google");
            MessageBox.Show(this, "Đăng nhập Google thất bại. " + ex.Message +
                "\n\nNếu OAuth Client đang ở chế độ Testing, hãy kiểm tra danh sách test users và trạng thái consent.",
                "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _settingUp = false; _login.Enabled = true; }
    }

    private async Task ChooseLocalFolderAsync()
    {
        if (_config is null || _settingUp) return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn thư mục trên laptop để đồng bộ",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (!string.IsNullOrEmpty(_config.Data.LocalFolder) && Directory.Exists(_config.Data.LocalFolder))
            dialog.InitialDirectory = _config.Data.LocalFolder;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!Directory.Exists(dialog.SelectedPath)) return;
        if (string.Equals(_config.Data.LocalFolder, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))
            return;
        await StopSyncAsync();
        _config.Data.LocalFolder = dialog.SelectedPath;
        ResetMapping();
        _localPath.Text = dialog.SelectedPath;
        _localPath.AccessibleDescription = dialog.SelectedPath;
        AddActivity("Đã chọn thư mục laptop");
        await StartSyncIfReadyAsync();
    }

    private async Task ChooseRemoteFolderAsync()
    {
        if (_config is null || _api is null || _settingUp) return;
        if (!_api.IsSignedIn)
        {
            MessageBox.Show(this, "Đăng nhập Google trước khi chọn thư mục Drive.",
                "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new RemoteFolderPicker(_api);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (string.Equals(_config.Data.RemoteFolderId, dialog.SelectedId, StringComparison.Ordinal))
            return;
        await StopSyncAsync();
        _config.Data.RemoteFolderId = dialog.SelectedId;
        _config.Data.RemoteFolderName = dialog.SelectedName;
        ResetMapping();
        _remotePath.Text = dialog.SelectedName;
        _remotePath.AccessibleDescription = dialog.SelectedName;
        AddActivity("Đã chọn thư mục Google Drive");
        await StartSyncIfReadyAsync();
    }

    private void ResetMapping()
    {
        if (_config is null) return;
        _config.Data.MappingApproved = false;
        _config.Data.ChangeToken = "";
        _config.Data.Records.Clear();
        _config.Save();
    }

    private async Task StartSyncIfReadyAsync()
    {
        if (_config is null || _api is null) return;
        if (!_api.IsSignedIn || !Directory.Exists(_config.Data.LocalFolder)
            || string.IsNullOrWhiteSpace(_config.Data.RemoteFolderId))
        {
            SetStatus("Chưa sẵn sàng", "Đăng nhập và chọn đủ hai thư mục");
            return;
        }

        if (!_config.Data.MappingApproved)
        {
            var decision = MessageBox.Show(this,
                "Đồng bộ hai chiều giữa:\n\nLAPTOP: " + _config.Data.LocalFolder +
                "\nGOOGLE DRIVE: " + _config.Data.RemoteFolderName +
                "\n\nTrong lần đầu, file trùng tên nhưng khác nội dung sẽ được giữ bản xung đột để không mất dữ liệu." +
                "\n\nCÓ: Đồng bộ cả thao tác xóa (Drive dùng Thùng rác; laptop lưu Recovery)." +
                "\nKHÔNG: Chỉ đồng bộ thêm/sửa, không tự đồng bộ xóa." +
                "\nHỦY: Chưa bắt đầu.",
                "Xác nhận đồng bộ lần đầu",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (decision == DialogResult.Cancel)
            {
                SetStatus("Chưa bắt đầu", "Chờ xác nhận đồng bộ lần đầu");
                return;
            }
            _config.Data.MirrorDeletes = decision == DialogResult.Yes;
            _config.Data.MappingApproved = true;
            _config.Save();
        }
        await StopSyncAsync();
        _sync = new SyncEngine(_config, _api);
        _sync.Status += status => UI(() => SetStatus(status,
            "Đang theo dõi thư mục · Drive kiểm tra thay đổi tối đa mỗi 60 giây"));
        _sync.Activity += activity => UI(() => AddActivity(activity));
        try
        {
            _sync.Start();
            _paused = false;
            _pause.Text = "⏸  Tạm dừng";
            _togglePause.Text = "⏸  Tạm dừng";
            SetStatus("Đang đồng bộ...", "Có thể thu nhỏ để ứng dụng chạy ngầm");
        }
        catch (Exception ex)
        {
            SetStatus("Không thể khởi động đồng bộ", ex.GetType().Name);
            MessageBox.Show(this, "Không khởi động được đồng bộ: " + ex.Message,
                "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task StopSyncAsync()
    {
        if (_sync is null) return;
        var previous = _sync;
        _sync = null;
        try { await previous.StopAsync(); }
        finally { previous.Dispose(); }
    }

    private void UI(Action callback)
    {
        try
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) callback(); }));
        }
        catch (InvalidOperationException) { }
    }

    private void SetStatus(string status, string subtitle)
    {
        if (_paused) return;
        _statusTitle.Text = status;
        _statusSubtitle.Text = subtitle;
    }

    private void AddActivity(string activity)
    {
        string message = DateTime.Now.ToString("HH:mm:ss") + " · " + activity;
        _recent.Insert(0, message);
        if (_recent.Count > 100) _recent.RemoveAt(_recent.Count - 1);
        var rows = _activity.Controls.OfType<Label>()
            .Where(label => label.Left == 50 && (label.Top == 57 || label.Top == 96 || label.Top == 135))
            .OrderBy(label => label.Top).ToList();
        for (int i = 0; i < rows.Count; i++)
            rows[i].Text = i < _recent.Count ? _recent[i] : "";
    }

    private void ShowRecentActivity()
    {
        using var dialog = new Form {
            Text = "DriveSync · Hoạt động gần đây", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(690, 430), MinimumSize = new Size(450, 300)
        };
        var contents = new TextBox {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9),
            Text = _recent.Count == 0 ? "Chưa có hoạt động đồng bộ." : string.Join(Environment.NewLine, _recent)
        };
        dialog.Controls.Add(contents);
        dialog.ShowDialog(this);
    }
}
