namespace DriveSyncPortable;

internal sealed class FilePermissionsForm : Form
{
    private readonly GoogleDriveApi _api;
    private readonly DriveItem _file;
    private readonly string _rootId;
    private readonly ListBox _existing = new() { Dock = DockStyle.Fill };
    private readonly TextBox _email = new() { Width = 260, PlaceholderText = "email@gmail.com" };
    private readonly ComboBox _role = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _add = new() { Text = "Cấp quyền", Width = 115 };
    private readonly Button _remove = new() { Text = "Thu hồi đã chọn", Width = 145 };
    private readonly Label _status = new() { AutoSize = true, Text = "Chỉ thao tác file đang chọn." };
    private List<DrivePermission> _permissions = new();

    public FilePermissionsForm(GoogleDriveApi api, DriveItem file, string rootId)
    {
        _api = api;
        _file = file;
        _rootId = rootId;
        Text = "Quản lý chia sẻ — " + file.Name;
        Size = new Size(655, 510);
        MinimumSize = new Size(530, 390);
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(12);
        _role.Items.AddRange(new object[] { "Viewer", "Editor" });
        _role.SelectedIndex = 0;
        _existing.SelectedIndexChanged += (_, _) =>
        {
            int i = _existing.SelectedIndex;
            _remove.Enabled = i >= 0 && i < _permissions.Count
                && _permissions[i].Role != "owner";
        };
        _remove.Enabled = false;

        var input = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 92 };
        input.Controls.Add(_email);
        input.Controls.Add(_role);
        input.Controls.Add(_add);
        input.Controls.Add(_remove);
        input.Controls.Add(_status);
        Controls.Add(_existing);
        Controls.Add(input);

        Shown += async (_, _) => await ReloadAsync();
        _add.Click += async (_, _) =>
        {
            var role = _role.SelectedIndex == 1 ? "writer" : "reader";
            if (MessageBox.Show(this,
                "Cấp quyền " + _role.Text + " đối với file: " + _file.Name
                + "\nCho: " + _email.Text.Trim() + "?",
                "Xác nhận chia sẻ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _add.Enabled = false;
            try
            {
                await _api.AddPermissionInScopeAsync(_file.Id, _rootId, _email.Text.Trim(),
                    role, CancellationToken.None);
                await ReloadAsync();
                _status.Text = "Đã cấp quyền.";
            }
            catch (Exception ex) { _status.Text = "Cấp quyền thất bại: " + ex.GetType().Name; }
            finally { _add.Enabled = true; }
        };
        _remove.Click += async (_, _) =>
        {
            int i = _existing.SelectedIndex;
            if (i < 0 || i >= _permissions.Count) return;
            var permission = _permissions[i];
            if (permission.Role == "owner") return;
            if (MessageBox.Show(this,
                "Thu hồi quyền " + permission.Role + " của " + permission.Email + "?",
                "Xác nhận thu hồi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _remove.Enabled = false;
            try
            {
                await _api.RemovePermissionInScopeAsync(_file.Id, _rootId,
                    permission.Id, CancellationToken.None);
                await ReloadAsync();
                _status.Text = "Đã thu hồi quyền.";
            }
            catch (Exception ex) { _status.Text = "Thu hồi thất bại: " + ex.GetType().Name; }
        };
    }

    private async Task ReloadAsync()
    {
        _status.Text = "Đang đọc quyền từ Drive...";
        try
        {
            _permissions = await _api.ReadPermissionsInScopeAsync(
                _file.Id, _rootId, CancellationToken.None);
            _existing.Items.Clear();
            foreach (var p in _permissions)
                _existing.Items.Add((p.Email.Length > 0 ? p.Email : p.Type)
                    + " · " + p.Role);
            _status.Text = "Có " + _permissions.Count + " mục quyền.";
        }
        catch (Exception ex) { _status.Text = "Không đọc được quyền: " + ex.GetType().Name; }
        _remove.Enabled = false;
    }
}
