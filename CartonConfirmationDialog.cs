namespace SupraAutoPackLe;

internal enum CartonDecision { Print, Correct, Exit }

/// <summary>Operator confirmation only; this dialog never dispatches WMS actions.</summary>
internal sealed class CartonConfirmationDialog : Form
{
    public CartonDecision Decision { get; private set; } = CartonDecision.Exit;

    public CartonConfirmationDialog(string heading, string countText, string instructions, bool canPrint)
    {
        Text = "IN TEM CHẴN · ĐỐI CHIẾU SỐ KIỆN";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        // Roughly 50% larger than v1.8.11 (660 × 395); stay within the work area.
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
        ClientSize = new Size(Math.Min(990, area.Width - 60), Math.Min(590, area.Height - 80));
        MinimumSize = new Size(720, 460);
        BackColor = Color.FromArgb(247, 249, 252);
        Font = new Font("Segoe UI", 15);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28),
            ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = heading,
            ForeColor = canPrint ? Color.FromArgb(23, 48, 76) : Color.FromArgb(165, 40, 36),
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = countText,
            BackColor = Color.White,
            ForeColor = canPrint ? Color.FromArgb(15, 94, 59) : Color.FromArgb(176, 36, 32),
            Font = new Font("Segoe UI", 49, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = instructions.Replace("\\n", "\n"),
            Font = new Font("Segoe UI", 18), TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 12, 12, 12) }, 0, 2);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = canPrint ? 3 : 2 };
        for(var i=0;i<actions.ColumnCount;i++)
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f/actions.ColumnCount));
        var print = MakeButton("OK · IN TEM", Color.FromArgb(23, 132, 75));
        var correct = MakeButton("SỬA SỐ KIỆN", Color.FromArgb(196, 118, 28));
        var exit = MakeButton("THOÁT", Color.FromArgb(100, 110, 120));
        print.Click += (_, _) => Finish(CartonDecision.Print);
        correct.Click += (_, _) => Finish(CartonDecision.Correct);
        exit.Click += (_, _) => Finish(CartonDecision.Exit);
        // Owner-approved order: OK → Sửa số kiện → Thoát.
        var next = 0;
        if(canPrint) actions.Controls.Add(print, next++, 0);
        actions.Controls.Add(correct, next++, 0);
        actions.Controls.Add(exit, next, 0);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
        if(canPrint) AcceptButton = print;
        CancelButton = exit;
    }

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text, Dock = DockStyle.Fill, BackColor = color, ForeColor = Color.White,
        Font = new Font("Segoe UI", 15, FontStyle.Bold),
        FlatStyle = FlatStyle.Flat, Margin = new Padding(5), UseVisualStyleBackColor = false
    };

    private void Finish(CartonDecision decision)
    {
        Decision = decision;
        DialogResult = DialogResult.OK;
        Close();
    }
}
