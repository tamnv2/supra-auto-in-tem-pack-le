namespace DriveSyncPortable;

internal sealed class RevisionPicker : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) };
    public string SelectedId => _list.SelectedItem is RevisionEntry e ? e.Id : "";

    private sealed record RevisionEntry(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    public RevisionPicker(IReadOnlyList<DriveRevision> revisions)
    {
        Text = "Chọn phiên bản cũ — tạo bản sao, không ghi đè";
        Size = new Size(600, 450);
        MinimumSize = new Size(490, 340);
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(12);
        Controls.Add(_list);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 53,
            FlowDirection = FlowDirection.RightToLeft };
        var restore = new Button { Text = "Khôi phục bản sao", Width = 160, Height = 34 };
        var cancel = new Button { Text = "Hủy", Width = 80, Height = 34,
            DialogResult = DialogResult.Cancel };
        restore.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(SelectedId)) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        bottom.Controls.Add(restore);
        bottom.Controls.Add(cancel);
        Controls.Add(bottom);
        foreach (var rev in revisions)
            _list.Items.Add(new RevisionEntry(rev.Id,
                $"{rev.ModifiedTime}  |  {TransferFormat.Size(rev.Size)}  |  {rev.Id}"));
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        AcceptButton = restore;
        CancelButton = cancel;
    }
}
