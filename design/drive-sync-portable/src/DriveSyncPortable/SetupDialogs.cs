namespace DriveSyncPortable;

internal sealed class GoogleCredentialsDialog : Form
{
    private readonly TextBox _json = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Top,
        Height = 138,
        Font = new Font("Consolas", 9),
        PlaceholderText = "Dán JSON OAuth Desktop đã tải từ Google Cloud..."
    };
    public string ClientId { get; private set; } = "";
    public string ClientSecret { get; private set; } = "";

    public GoogleCredentialsDialog()
    {
        Text = "Thiết lập Google OAuth · Một lần";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(570, 360);
        Padding = new Padding(22);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label {
            Text = "Google yêu cầu OAuth Client riêng.\nTạo loại Desktop app trên Google Cloud, tải JSON và dán nội dung bên dưới.",
            Dock = DockStyle.Fill, ForeColor = Theme.Ink, Font = new Font("Segoe UI", 9)
        }, 0, 0);
        layout.Controls.Add(_json, 0, 1);
        var link = new LinkLabel { Text = "Mở Google Cloud → OAuth Clients", Dock = DockStyle.Fill };
        link.LinkClicked += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://console.cloud.google.com/auth/clients") { UseShellExecute = true });
        layout.Controls.Add(link, 0, 2);
        layout.Controls.Add(new Label {
            Text = "Client Secret được mã hóa theo user Windows. Không đưa JSON lên GitHub.",
            Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8)
        }, 0, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var confirm = new Button { Text = "Lưu & tiếp tục", Width = 150, Height = 32 };
        var cancel = new Button { Text = "Hủy", Width = 95, Height = 32, DialogResult = DialogResult.Cancel };
        confirm.Click += (_, _) =>
        {
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(_json.Text);
                var root = json.RootElement;
                if (!root.TryGetProperty("installed", out var installed))
                    throw new InvalidDataException("Phải dùng JSON OAuth loại Desktop app (installed).");
                string id = installed.GetProperty("client_id").GetString() ?? "";
                string secret = installed.TryGetProperty("client_secret", out var value) ? value.GetString() ?? "" : "";
                if (!id.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Client ID không hợp lệ.");
                ClientId = id; ClientSecret = secret;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception)
            {
                MessageBox.Show(this, "JSON không hợp lệ. Hãy tải OAuth Client loại Desktop từ Google Cloud, không phải API key hoặc Service Account.",
                    "Không đọc được OAuth JSON", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        buttons.Controls.Add(confirm);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        AcceptButton = confirm;
        CancelButton = cancel;
    }
}

internal sealed class RemoteFolderPicker : Form
{
    private readonly GoogleDriveApi _drive;
    private readonly ListBox _folders = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) };
    private readonly Label _location = new() { AutoSize = false, Dock = DockStyle.Top, Height = 35 };
    private readonly Button _select = new() { Text = "Chọn thư mục này", Width = 155, Height = 35 };
    private readonly Button _back = new() { Text = "Lên một cấp", Width = 112, Height = 35 };
    private readonly Button _create = new() { Text = "+ Tạo thư mục", Width = 130, Height = 35 };
    private readonly Stack<(string id, string path)> _history = new();
    private readonly List<DriveItem> _current = new();
    private string _currentId = "root";
    private string _currentPath = "My Drive";
    private bool _busy;
    public string SelectedId { get; private set; } = "";
    public string SelectedName { get; private set; } = "";

    public RemoteFolderPicker(GoogleDriveApi api)
    {
        _drive = api;
        Text = "Chọn thư mục trên Google Drive";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 530);
        MinimumSize = new Size(510, 400);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        _location.Text = "My Drive";
        _location.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        body.Controls.Add(_folders);
        body.Controls.Add(_location);
        var footer = new FlowLayoutPanel {
            Dock = DockStyle.Bottom, Height = 65, Padding = new Padding(12),
            FlowDirection = FlowDirection.LeftToRight
        };
        footer.Controls.Add(_back);
        footer.Controls.Add(_create);
        footer.Controls.Add(_select);
        Controls.Add(body);
        Controls.Add(footer);
        _folders.DoubleClick += async (_, _) => await EnterSelectedAsync();
        _back.Click += async (_, _) =>
        {
            if (_history.TryPop(out var prev))
            {
                _currentId = prev.id;
                _currentPath = prev.path;
                await RefreshFoldersAsync();
            }
        };
        _select.Click += (_, _) =>
        {
            SelectedId = _currentId;
            SelectedName = _currentPath;
            DialogResult = DialogResult.OK;
            Close();
        };
        _create.Click += async (_, _) =>
        {
            if (_busy) return;
            string? name = PromptName();
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                _busy = true;
                await _drive.CreateFolderAsync(name, _currentId, CancellationToken.None);
                _busy = false;
                await RefreshFoldersAsync();
            }
            catch (Exception ex)
            {
                _busy = false;
                MessageBox.Show(this, "Không tạo được thư mục Drive: " + ex.GetType().Name,
                    "Google Drive", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Shown += async (_, _) => await RefreshFoldersAsync();
    }

    private async Task EnterSelectedAsync()
    {
        if (_busy || _folders.SelectedIndex < 0 || _folders.SelectedIndex >= _current.Count) return;
        var folder = _current[_folders.SelectedIndex];
        _history.Push((_currentId, _currentPath));
        _currentId = folder.Id;
        _currentPath += " / " + folder.Name;
        await RefreshFoldersAsync();
    }

    private async Task RefreshFoldersAsync()
    {
        if (_busy) return;
        _busy = true;
        _select.Enabled = false;
        _folders.Items.Clear();
        _location.Text = _currentPath + " (đang tải...)";
        try
        {
            _current.Clear();
            _current.AddRange((await _drive.ListChildrenAsync(_currentId, CancellationToken.None))
                .Where(x => x.Folder).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase));
            foreach (var folder in _current) _folders.Items.Add("▣  " + folder.Name);
            _location.Text = _currentPath;
        }
        catch (Exception ex)
        {
            _location.Text = _currentPath;
            MessageBox.Show(this, "Không tải được thư mục Drive: " + ex.GetType().Name,
                "Google Drive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _select.Enabled = true; _busy = false; }
    }

    private string? PromptName()
    {
        using var form = new Form { Text = "Tạo thư mục Drive", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(420, 172), FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false };
        var text = new TextBox { Left = 16, Top = 16, Width = 370 };
        var ok = new Button { Text = "Tạo", Left = 275, Top = 65, Width = 110,
            DialogResult = DialogResult.OK };
        form.Controls.Add(text);
        form.Controls.Add(ok);
        form.AcceptButton = ok;
        return form.ShowDialog(this) == DialogResult.OK ? text.Text.Trim() : null;
    }
}
