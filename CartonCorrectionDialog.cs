using System.Globalization;

namespace SupraAutoPackLe;

internal enum CorrectionDecision { Submit, Back, Exit }

/// <summary>Explicit operator-entered carton quantity. No WMS actions are issued here.</summary>
internal sealed class CartonCorrectionDialog : Form
{
    private readonly TextBox _quantity = new()
    {
        Dock = DockStyle.Fill, Font = new Font("Segoe UI", 35, FontStyle.Bold),
        TextAlign = HorizontalAlignment.Center, MaxLength = 7
    };
    public CorrectionDecision Decision { get; private set; } = CorrectionDecision.Back;
    public int Quantity { get; private set; }

    public CartonCorrectionDialog(string currentCount)
    {
        Text = "IN TEM CHẴN · SỬA SỐ KIỆN";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0,0,1200,800);
        ClientSize = new Size(Math.Min(900,area.Width-60),Math.Min(530,area.Height-80));
        MinimumSize = new Size(680,430);
        Font = new Font("Segoe UI",15);
        BackColor = Color.FromArgb(247,249,252);
        var root = new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(28),
            RowCount=5,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,65));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,90));
        root.Controls.Add(new Label {Text="NHẬP SỐ KIỆN CẦN SỬA",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",22,FontStyle.Bold),
            ForeColor=Color.FromArgb(26,43,69),
            TextAlign=ContentAlignment.MiddleCenter},0,0);
        root.Controls.Add(new Label {Text="Số kiện WMS hiện tại: "+currentCount,
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",19),
            TextAlign=ContentAlignment.MiddleCenter},0,1);
        root.Controls.Add(new Label {Text="Số kiện đúng sau khi đối chiếu phiếu kiểm",
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",17,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleCenter},0,2);
        var inputShell=new Panel {Dock=DockStyle.Fill,Padding=new Padding(50,16,50,16)};
        _quantity.Height=80;
        inputShell.Controls.Add(_quantity);
        root.Controls.Add(inputShell,0,3);
        _quantity.KeyPress+=(_,e)=>{
            if(!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
                e.Handled=true;
        };
        var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3};
        for(var i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var submit=MakeButton("XÁC NHẬN SỬA",Color.FromArgb(23,132,75));
        var back=MakeButton("QUAY LẠI",Color.FromArgb(30,102,175));
        var exit=MakeButton("THOÁT",Color.FromArgb(100,110,120));
        submit.Click+=(_,_)=>{
            if(!int.TryParse(_quantity.Text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out var count) ||
                count<1 || count>999999)
            {
                MessageBox.Show(this,"Số kiện phải là số nguyên dương từ 1 đến 999999.",
                    "SỐ KIỆN KHÔNG HỢP LỆ",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                _quantity.SelectAll();
                _quantity.Focus();
                return;
            }
            Quantity=count;
            Finish(CorrectionDecision.Submit);
        };
        back.Click+=(_,_)=>Finish(CorrectionDecision.Back);
        exit.Click+=(_,_)=>Finish(CorrectionDecision.Exit);
        actions.Controls.Add(submit,0,0);
        actions.Controls.Add(back,1,0);
        actions.Controls.Add(exit,2,0);
        root.Controls.Add(actions,0,4);
        Controls.Add(root);
        AcceptButton=submit;
        CancelButton=back;
        Shown+=(_,_)=>_quantity.Focus();
    }

    private static Button MakeButton(string text,Color color)=>new()
    {
        Text=text,Dock=DockStyle.Fill,BackColor=color,ForeColor=Color.White,
        Font=new Font("Segoe UI",14,FontStyle.Bold),FlatStyle=FlatStyle.Flat,
        UseVisualStyleBackColor=false,Margin=new Padding(5)
    };
    private void Finish(CorrectionDecision decision)
    {
        Decision=decision;
        DialogResult=DialogResult.OK;
        Close();
    }
}
