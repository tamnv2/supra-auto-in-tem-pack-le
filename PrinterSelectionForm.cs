namespace SupraAutoPackLe;

/// <summary>Operator must explicitly choose from the currently scanned WMS printers.</summary>
internal sealed class PrinterSelectionForm : Form
{
    private readonly ListBox _list = new() {
        Dock=DockStyle.Fill,SelectionMode=SelectionMode.One,
        Font=new Font("Segoe UI",12),IntegralHeight=false };
    private readonly Button _confirm = new() {
        Text="SỬ DỤNG MÁY IN ĐÃ CHỌN",Dock=DockStyle.Fill,
        Font=new Font("Segoe UI",11,FontStyle.Bold),
        BackColor=Color.FromArgb(29,99,173),ForeColor=Color.White,
        Enabled=false,Height=46 };
    public string SelectedPrinter {get;private set;}="";
    public PrinterSelectionForm(IReadOnlyList<string> printers,string currentPrinter)
    {
        Text="CHỌN MÁY IN WMS";
        StartPosition=FormStartPosition.CenterParent;
        Width=640;Height=540;MinimumSize=new Size(460,370);
        Font=new Font("Segoe UI",11);BackColor=Color.FromArgb(247,249,252);
        var root=new TableLayoutPanel {
            Dock=DockStyle.Fill,Padding=new Padding(20,18,20,18),
            ColumnCount=1,RowCount=4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        root.Controls.Add(new Label {
            Text="CHỌN MÁY IN PHỤC VỤ NGHIỆP VỤ",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",15,FontStyle.Bold),
            ForeColor=Color.FromArgb(26,43,69),
            TextAlign=ContentAlignment.MiddleLeft
        },0,0);
        root.Controls.Add(new Label {
            Text="Máy in WMS hiện tại: "+(string.IsNullOrWhiteSpace(currentPrinter)?"Chưa xác định":currentPrinter)+
                 "\nChọn một máy in trong danh sách được WMS cung cấp.",
            Dock=DockStyle.Fill,AutoEllipsis=true
        },0,1);
        foreach(var value in printers)_list.Items.Add(value);
        _list.ClearSelected(); // Explicit operator choice; no preselection.
        root.Controls.Add(_list,0,2);
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        _confirm.Margin=new Padding(0,5,8,5);
        var cancel=new Button{Text="HỦY",Dock=DockStyle.Fill,Height=46,DialogResult=DialogResult.Cancel};
        cancel.Margin=new Padding(0,5,0,5);
        actions.Controls.Add(_confirm,0,0);
        actions.Controls.Add(cancel,1,0);
        root.Controls.Add(actions,0,3);
        Controls.Add(root);
        _list.SelectedIndexChanged+=(_,_)=>_confirm.Enabled=_list.SelectedItem is string;
        _list.DoubleClick+=(_,_)=>{if(_confirm.Enabled)Choose();};
        _confirm.Click+=(_,_)=>Choose();
        CancelButton=cancel;
    }
    private void Choose()
    {
        if(_list.SelectedItem is not string selected || selected.Length==0)return;
        SelectedPrinter=selected;
        DialogResult=DialogResult.OK;
        Close();
    }
}
