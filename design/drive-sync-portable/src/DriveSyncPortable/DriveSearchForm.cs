using System.Diagnostics;

namespace DriveSyncPortable;

// Search is explicitly user-triggered, so this dialog causes no background API usage.
internal sealed class DriveSearchForm : Form
{
    private readonly GoogleDriveApi _drive;
    private readonly TextBox _query = new() { PlaceholderText = "Tên file/thư mục (tối thiểu 2 ký tự)" };
    private readonly Button _search = new() { Text = "Tìm kiếm", Width = 105 };
    private readonly Button _open = new() { Text = "Mở trên Drive", Width = 125, Enabled = false };
    private readonly Button _copy = new() { Text = "Sao chép liên kết", Width = 145, Enabled = false };
    private readonly Label _status = new() { Text = "Chỉ tìm kiếm khi bấm nút. Tối đa 100 kết quả.", AutoSize = true };
    private readonly ListView _items = new() {
        View = View.Details, FullRowSelect = true, MultiSelect = false, Dock = DockStyle.Fill
    };

    public DriveSearchForm(GoogleDriveApi api)
    {
        _drive = api;
        Text = "Tìm kiếm trên Google Drive";
        ClientSize = new Size(840, 540);
        MinimumSize = new Size(660, 390);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(12);

        _items.Columns.Add("Tên", 390);
        _items.Columns.Add("Loại", 140);
        _items.Columns.Add("Dung lượng", 110);
        _items.Columns.Add("Sửa đổi", 150);
        _items.SelectedIndexChanged += (_, _) => {
            bool selected = _items.SelectedItems.Count > 0;
            _open.Enabled = selected;
            _copy.Enabled = selected;
        };
        _items.DoubleClick += (_, _) => OpenSelected();

        var filter = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 47 };
        _query.Width = 560;
        _query.Margin = new Padding(0, 6, 10, 5);
        _search.Margin = new Padding(0, 5, 0, 0);
        filter.Controls.Add(_query);
        filter.Controls.Add(_search);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
        actions.Controls.Add(_open);
        actions.Controls.Add(_copy);
        actions.Controls.Add(_status);
        _status.Margin = new Padding(14, 9, 0, 0);
        _open.Click += (_, _) => OpenSelected();
        _copy.Click += (_, _) => {
            if (SelectedUrl() is string url) Clipboard.SetText(url);
        };
        _search.Click += async (_, _) => await SearchAsync();

        Controls.Add(_items);
        Controls.Add(actions);
        Controls.Add(filter);
        AcceptButton = _search;
    }

    private async Task SearchAsync()
    {
        _search.Enabled = false;
        _status.Text = "Đang tìm...";
        _items.Items.Clear();
        try
        {
            var files = await _drive.SearchByNameAsync(_query.Text, CancellationToken.None);
            foreach (var item in files)
            {
                string type = item.Folder ? "Thư mục" : item.NativeGoogleFile ? "Google Docs" : "File";
                var row = new ListViewItem(new[] {
                    item.Name, type,
                    item.Folder || item.NativeGoogleFile ? "" : TransferFormat.Size(item.Size),
                    item.ModifiedTime
                }) { Tag = item.Id, ToolTipText = item.Name };
                _items.Items.Add(row);
            }
            _status.Text = files.Count + " kết quả (tối đa 100)";
        }
        catch (Exception ex)
        {
            _status.Text = "Tìm kiếm thất bại: " + ex.GetType().Name;
        }
        finally { _search.Enabled = true; }
    }

    private string? SelectedUrl()
    {
        if (_items.SelectedItems.Count != 1) return null;
        string id = _items.SelectedItems[0].Tag as string ?? "";
        return id.Length == 0 ? null : "https://drive.google.com/open?id=" + Uri.EscapeDataString(id);
    }

    private void OpenSelected()
    {
        if (SelectedUrl() is string url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
