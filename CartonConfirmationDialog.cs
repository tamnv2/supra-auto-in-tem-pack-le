namespace SupraAutoPackLe;

internal enum CartonDecision { Print, Correct, Exit }

/// <summary>Operator confirmation only. Never issues a WMS print or correction command.</summary>
internal sealed class CartonConfirmationDialog : Form
{
    public CartonDecision Decision { get; private set; } = CartonDecision.Exit;

    public CartonConfirmationDialog(string heading, string countText, string instructions, bool canPrint)
    {
        Text = "IN TEM CHẴN · ĐỐI CHIẾU SỐ KIỆN";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(660, 395);
        BackColor = Color.FromArgb(247, 249, 252);
        Font = new Font("Segoe UI", 11);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20),
            ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = heading,
            ForeColor = canPrint ? Color.FromArgb(23, 48, 76) : Color.FromArgb(165, 40, 36),
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = countText,
            BackColor = Color.White,
            ForeColor = canPrint ? Color.FromArgb(15, 94, 59) : Color.FromArgb(176, 36, 32),
            Font = new Font("Segoe UI", 33, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = instructions,
            Font = new Font("Segoe UI", 12), TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(15, 8, 15, 8) }, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var exit = MakeButton("THOÁT", Color.FromArgb(100, 110, 120));
        var correct = MakeButton("SỬA SỐ KIỆN", Color.FromArgb(196, 118, 28));
        var print = MakeButton("OK · IN TEM", Color.FromArgb(23, 132, 75));
        print.Enabled = canPrint;
        print.Visible = canPrint; // Invalid/decimal values have no print option.
        exit.Click += (_, _) => Finish(CartonDecision.Exit);
        correct.Click += (_, _) => Finish(CartonDecision.Correct);
        print.Click += (_, _) => Finish(CartonDecision.Print);
        actions.Controls.Add(exit, 0, 0);
        actions.Controls.Add(correct, 1, 0);
        actions.Controls.Add(print, 2, 0);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
        CancelButton = exit;
    }

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text, Dock = DockStyle.Fill, BackColor = color, ForeColor = Color.White,
        Font = new Font("Segoe UI", 10, FontStyle.Bold),
        FlatStyle = FlatStyle.Flat, Margin = new Padding(4), UseVisualStyleBackColor = false
    };

    private void Finish(CartonDecision decision)
    {
        Decision = decision;
        DialogResult = DialogResult.OK;
        Close();
    }
}
