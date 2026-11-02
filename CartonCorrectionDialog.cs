using System.Globalization;

namespace SupraAutoPackLe;

internal enum CorrectionDecision { Submit, Back, Exit }

/// <summary>Enter correction quantity and required reason before issuing any WMS change.</summary>
internal sealed class CartonCorrectionDialog : Form
{
    private readonly TextBox _quantity=new()
    {
        Dock=DockStyle.Fill,Font=new Font("Segoe UI",33,FontStyle.Bold),
        TextAlign=HorizontalAlignment.Center,MaxLength=7
    };
    private readonly TextBox _reason=new()
    {
        Dock=DockStyle.Fill,Font=new Font("Segoe UI",17),Multiline=true,
        AcceptsReturn=true,MaxLength=300,ScrollBars=ScrollBars.Vertical
    };
    public CorrectionDecision Decision { get; private set; }=CorrectionDecision.Back;
    public int Quantity { get; private set; }
    public string Reason { get; private set; }="";

    public CartonCorrectionDialog(string currentCount)
    {
        Text="IN TEM CHẴN · SỬA SỐ KIỆN";
        StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        AutoScaleMode=AutoScaleMode.Dpi;
        MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
        var area=Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0,0,1200,900);
        ClientSize=new Size(Math.Min(940,area.Width-60),Math.Min(650,area.Height-80));
        MinimumSize=new Size(690,515);
        Font=new Font("Segoe UI",15);
        BackColor=Color.FromArgb(247,249,252);
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),
            RowCount=7,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,60)); // header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,55)); // old count
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); // quantity label
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,95)); // quantity input
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); // reason label
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); // reason input
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,82)); // buttons
        root.Controls.Add(new Label {Text="NHẬP SỐ KIỆN CẦN SỬA",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",22,FontStyle.Bold),
            ForeColor=Color.FromArgb(26,43,69),
            TextAlign=ContentAlignment.MiddleCenter},0,0);
        root.Controls.Add(new Label {Text="Số kiện WMS hiện tại: "+currentCount,
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",17),
            TextAlign=ContentAlignment.MiddleCenter},0,1);
        root.Controls.Add(new Label {Text="Số kiện đúng sau khi đối chiếu phiếu kiểm",
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",16,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleLeft},0,2);
        var inputShell=new Panel {Dock=DockStyle.Fill,Padding=new Padding(35,7,35,12)};
        inputShell.Controls.Add(_quantity);
        root.Controls.Add(inputShell,0,3);
        root.Controls.Add(new Label {Text="LÝ DO SỬA KIỆN (bắt buộc, tối đa 300 ký tự)",
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",16,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleLeft},0,4);
        var reasonShell=new Panel {Dock=DockStyle.Fill,Padding=new Padding(4,0,4,6)};
        reasonShell.Controls.Add(_reason);
        root.Controls.Add(reasonShell,0,5);
        _quantity.KeyPress+=(_,e)=>{
            if(!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))e.Handled=true;
        };
        var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3};
        for(var i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var submit=MakeButton("XÁC NHẬN SỬA",Color.FromArgb(23,132,75));
        var back=MakeButton("QUAY LẠI",Color.FromArgb(30,102,175));
        var exit=MakeButton("THOÁT",Color.FromArgb(100,110,120));
        submit.Click+=(_,_)=>{
            if(!int.TryParse(_quantity.Text.Trim(),NumberStyles.None,
                CultureInfo.InvariantCulture,out var count) || count<1 || count>999999)
            {
                MessageBox.Show(this,"Số kiện phải là số nguyên dương từ 1 đến 999999.",
                    "SỐ KIỆN KHÔNG HỢP LỆ",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                _quantity.Focus();_quantity.SelectAll();return;
            }
            var reason=string.Join(" ",_reason.Text.Split(
                (char[]?)null,StringSplitOptions.RemoveEmptyEntries)).Trim();
            if(reason.Length<3 || reason.Length>300 ||
                reason.Any(char.IsControl))
            {
                MessageBox.Show(this,"Nhập lý do sửa kiện tối thiểu 3 và tối đa 300 ký tự.",
                    "CHƯA CÓ LÝ DO",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                _reason.Focus();return;
            }
            Quantity=count;Reason=reason;Finish(CorrectionDecision.Submit);
        };
        back.Click+=(_,_)=>Finish(CorrectionDecision.Back);
        exit.Click+=(_,_)=>Finish(CorrectionDecision.Exit);
        actions.Controls.Add(submit,0,0);
        actions.Controls.Add(back,1,0);
        actions.Controls.Add(exit,2,0);
        root.Controls.Add(actions,0,6);
        Controls.Add(root);
        AcceptButton=submit;CancelButton=back;
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
