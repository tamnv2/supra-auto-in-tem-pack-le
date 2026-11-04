namespace DriveSyncPortable;

public sealed partial class MainForm
{
    private SettingsStore? _config;
    private GoogleDriveApi? _api;
    private SyncEngine? _sync;
    private bool _settingUp;

    private void InitializeSync()
    {
        try
        {
            _config = new SettingsStore();
            _api = new GoogleDriveApi(_config);
            LoadActivityHistory();
            _localPath.Text = string.IsNullOrWhiteSpace(_config.Data.LocalFolder)
                ? "Chưa chọn thư mục" : _config.Data.LocalFolder;
            _remotePath.Text = string.IsNullOrWhiteSpace(_config.Data.RemoteFolderId)
                ? "Chưa chọn thư mục" : _config.Data.RemoteFolderName;
            _accountName.Text = _api.IsSignedIn ? _config.Data.GoogleEmail : "Chưa đăng nhập Google";
            _accountDesc.Text = _api.IsSignedIn
                ? "Google OAuth · Kết nối được lưu an toàn theo user Windows"
                : "Đăng nhập trên trình duyệt Google · Không nhập mật khẩu vào EXE";
            _login.Text = _api.IsSignedIn ? "Đổi tài khoản" : "Đăng nhập Google";
            ApplySyncStatus(_api.IsSignedIn
                ? new SyncStatus(SyncState.SetupRequired, "Sẵn sàng kết nối",
                    "Chọn đủ hai thư mục để tự đồng bộ")
                : new SyncStatus(SyncState.SetupRequired, "Chưa đăng nhập Google",
                    "Đăng nhập Gmail để bắt đầu"));
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
            ApplySyncStatus(new SyncStatus(SyncState.NeedsAttention,
                "Lỗi cấu hình", "Đồng bộ bị khóa để bảo vệ dữ liệu"));
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
            string oldEmail = _config.Data.GoogleEmail;
            SetStatus("Đang đăng nhập Google", "Xác thực trên trình duyệt mặc định");
            await _api.SignInAsync(CancellationToken.None);
            if (!string.Equals(oldEmail, _config.Data.GoogleEmail, StringComparison.OrdinalIgnoreCase))
            {
                // Never reuse a folder ID or sync checkpoint from another Google account.
                _config.Data.RemoteFolderId = "";
                _config.Data.RemoteFolderName = "";
                _remotePath.Text = "Chưa chọn thư mục";
                ResetMapping();
            }
            _accountName.Text = _config.Data.GoogleEmail;
            _accountDesc.Text = "Google OAuth · Phiên đăng nhập được mã hóa trong Windows";
            _login.Text = "Đổi tài khoản";
            AddActivity("Đã đăng nhập Google");
            SetStatus("Đăng nhập thành công", "Chọn thư mục laptop và Google Drive");
            await StartSyncIfReadyAsync();
            _lastQuotaFetch = DateTimeOffset.MinValue;
            _quotaStorage.Text = "Dung lượng Drive: đang cập nhật";
            await RefreshQuotaAsync(true);
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
        try { SyncScopeGuard.ValidateLocalRoot(dialog.SelectedPath); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Thư mục không hợp lệ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        SyncScopeGuard.ValidateRemoteRoot(dialog.SelectedId);
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
        _config.Data.ProtectedUploadSession = "";
        _config.Data.Records.Clear();
        _config.Data.FolderRecords.Clear();
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
        try
        {
            SyncScopeGuard.ValidateLocalRoot(_config.Data.LocalFolder);
            SyncScopeGuard.ValidateRemoteRoot(_config.Data.RemoteFolderId);
        }
        catch (Exception ex)
        {
            SetStatus("Đã khóa đồng bộ", ex.Message);
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
        var active = _sync;
        _sync.StateChanged += state => UI(() =>
        {
            if (ReferenceEquals(_sync, active)) ApplySyncStatus(state);
        });
        _sync.Progress += transfer => UI(() =>
        {
            if (ReferenceEquals(_sync, active)) ApplyTransfer(transfer);
        });
        _sync.QueueChanged += (total, attempted) => UI(() =>
        {
            if (ReferenceEquals(_sync, active))
                _queueInfo.Text = "Hàng đợi: " + Math.Max(0, total - attempted)
                    + " file chờ  ·  Đã kiểm tra: " + attempted + "/" + total;
        });
        _sync.Activity += activity => UI(() =>
        {
            if (ReferenceEquals(_sync, active)) AddActivity(activity);
        });
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

    private void SetStatus(string title, string detail)
    {
        SyncState state = title switch
        {
            "Đang đăng nhập Google" => SyncState.Connecting,
            "Đang đồng bộ..." => SyncState.Checking,
            "Chưa sẵn sàng" or "Chưa bắt đầu" or "Đăng nhập thành công" => SyncState.SetupRequired,
            "Đăng nhập chưa thành công" => SyncState.AuthenticationError,
            "Không thể khởi động đồng bộ" => SyncState.NeedsAttention,
            _ => SyncState.Checking
        };
        ApplySyncStatus(new SyncStatus(state, title, detail));
    }

    private string ActivityPath => Path.Combine(
        _config?.StorageFolder ?? SettingsStore.BaseFolder, "activity-history.log");

    private void LoadActivityHistory()
    {
        try
        {
            if (!File.Exists(ActivityPath)) return;
            foreach (string line in File.ReadLines(ActivityPath).TakeLast(500))
                if (!string.IsNullOrWhiteSpace(line)) InsertActivityRow(line, persist: false);
            LayoutCards();
        }
        catch
        {
            // History is optional. It must never prevent actual synchronization.
        }
    }

    private void AddActivity(string activity)
    {
        string line = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " | " + activity
            .Replace('\r', ' ').Replace('\n', ' ');
        InsertActivityRow(line, persist: true);
    }

    private void InsertActivityRow(string line, bool persist)
    {
        string[] pair = line.Split(" | ", 2, StringSplitOptions.None);
        string time = pair.Length == 2 ? pair[0] : DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        string activity = pair.Length == 2 ? pair[1] : line;
        string action, outcome, description = activity;
        if (activity.StartsWith("↑", StringComparison.Ordinal))
        {
            action = "Tải lên"; outcome = "Hoàn tất"; description = activity.TrimStart('↑', ' ');
        }
        else if (activity.StartsWith("↓", StringComparison.Ordinal))
        {
            action = "Tải xuống"; outcome = "Hoàn tất"; description = activity.TrimStart('↓', ' ');
        }
        else if (activity.StartsWith("Lỗi:", StringComparison.OrdinalIgnoreCase)
                 || activity.StartsWith("Chưa đồng bộ được:", StringComparison.OrdinalIgnoreCase))
        {
            action = "Lỗi"; outcome = "Cần xử lý";
        }
        else if (activity.Contains("xung đột", StringComparison.OrdinalIgnoreCase)
                 || activity.Contains("xung-dot", StringComparison.OrdinalIgnoreCase))
        {
            action = "Xung đột"; outcome = "Đã giữ bản";
        }
        else if (activity.Contains("thùng rác", StringComparison.OrdinalIgnoreCase)
                 || activity.Contains("Recovery", StringComparison.OrdinalIgnoreCase))
        {
            action = "Xóa/khôi phục"; outcome = "Đã xử lý";
        }
        else
        {
            action = "Hệ thống"; outcome = "Thông tin";
        }

        var item = new ListViewItem(time);
        item.SubItems.Add(action);
        item.SubItems.Add(description);
        item.SubItems.Add(outcome);
        item.ToolTipText = activity;
        if (outcome == "Cần xử lý") item.ForeColor = Color.FromArgb(179, 66, 54);
        _historyList.BeginUpdate();
        _historyList.Items.Insert(0, item);
        while (_historyList.Items.Count > 500)
            _historyList.Items.RemoveAt(_historyList.Items.Count - 1);
        _historyList.EndUpdate();
        _activityCount.Text = _historyList.Items.Count + " hoạt động gần đây";
        if (persist && _historyList.Items.Count < 20) LayoutCards();

        if (!persist) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ActivityPath)!);
            File.AppendAllText(ActivityPath, line + Environment.NewLine, System.Text.Encoding.UTF8);
            if (_historyList.Items.Count == 500 && _historyList.Items.Count % 50 == 0)
            {
                // Retain a bounded journal without request or network usage.
                var file = new FileInfo(ActivityPath);
                if (file.Length > 512 * 1024)
                    File.WriteAllLines(ActivityPath, File.ReadLines(ActivityPath).TakeLast(500));
            }
        }
        catch
        {
            // History storage errors do not block file syncing.
        }
    }

    private void ShowRecentActivity()
    {
        // The full-width scrollable history is already visible on the dashboard.
        _historyList.Focus();
        if (_historyList.Items.Count > 0)
            _historyList.EnsureVisible(0);
    }
}
