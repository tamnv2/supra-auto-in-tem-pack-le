namespace DriveSyncPortable;

internal sealed class SyncPreferencesDialog : Form
{
    private readonly NumericUpDown _upload = new() { Minimum = 0, Maximum = 1_000_000, ThousandsSeparator = true, Width = 150 };
    private readonly NumericUpDown _download = new() { Minimum = 0, Maximum = 1_000_000, ThousandsSeparator = true, Width = 150 };
    private readonly TextBox _extensions = new() { Width = 380, PlaceholderText = ".tmp;.bak (dấu chấm phẩy)" };

    public int UploadLimitKiBps => (int)_upload.Value;
    public int DownloadLimitKiBps => (int)_download.Value;
    public string IgnoredExtensions { get; private set; } = "";

    public SyncPreferencesDialog(AppSettings settings)
    {
        Text = "Tùy chọn đồng bộ";
        ClientSize = new Size(555, 292);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);
        _upload.Value = Math.Clamp(settings.UploadLimitKiBps, 0, 1_000_000);
        _download.Value = Math.Clamp(settings.DownloadLimitKiBps, 0, 1_000_000);
        _extensions.Text = settings.IgnoredExtensions;

        Controls.Add(new Label { Left = 20, Top = 18, Width = 500, Height = 26,
            Text = "0 = Không giới hạn; KB/s đo theo KiB (1 KiB = 1024 bytes)." });
        Controls.Add(new Label { Left = 20, Top = 57, Width = 200, Height = 23, Text = "Tốc độ tải lên (KiB/s)" });
        Controls.Add(new Label { Left = 20, Top = 95, Width = 200, Height = 23, Text = "Tốc độ tải xuống (KiB/s)" });
        _upload.Left = 255; _upload.Top = 54;
        _download.Left = 255; _download.Top = 92;
        Controls.Add(_upload); Controls.Add(_download);

        Controls.Add(new Label { Left = 20, Top = 139, Width = 500, Height = 22,
            Text = "Bỏ qua phần mở rộng file (không đồng bộ cả hai chiều):" });
        _extensions.Left = 20; _extensions.Top = 166;
        Controls.Add(_extensions);
        var save = new Button { Text = "Lưu thiết lập", Left = 366, Top = 230,
            Width = 158, Height = 35 };
        var cancel = new Button { Text = "Hủy", Left = 258, Top = 230,
            Width = 98, Height = 35, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            var tokens = _extensions.Text.Split(new[] { ';', ',' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Any(t => t.Length < 2 || t.Length > 24 || !t.StartsWith('.')
                || t.IndexOfAny(new[] { '/', '\\', '*', '?' }) >= 0))
            {
                MessageBox.Show(this, "Định dạng phần mở rộng: .tmp;.bak",
                    "Bộ lọc không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            IgnoredExtensions = string.Join(';', tokens.Distinct(StringComparer.OrdinalIgnoreCase));
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(save); Controls.Add(cancel);
        AcceptButton = save; CancelButton = cancel;
    }
}
