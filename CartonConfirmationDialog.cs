namespace SupraAutoPackLe;

internal enum CartonDecision { Print, Correct, Exit }

/// <summary>Operator decision only. SKU warnings never call WMS actions or
/// disable the print button when the existing carton/range checks are valid.</summary>
internal sealed class CartonConfirmationDialog : Form
{
    public CartonDecision Decision { get; private set; } = CartonDecision.Exit;
    private readonly Panel _auditHost=new() { Dock=DockStyle.Fill,AutoScroll=true,
        BackColor=Color.White,Padding=new Padding(3) };
    private readonly string _invalidInstructions;
    private readonly bool _canPrint;
    private readonly Color _red=Color.FromArgb(187,25,31);
    private readonly Color _navy=Color.FromArgb(23,48,76);

    public CartonConfirmationDialog(string heading,string countText,string instructions,bool canPrint)
    {
        _canPrint=canPrint;
        _invalidInstructions=instructions.Replace("\\n","\n");
        Text="IN TEM CHẴN · ĐỐI CHIẾU SỐ KIỆN";
        StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        AutoScaleMode=AutoScaleMode.Dpi;
        MaximizeBox=false;
        MinimizeBox=false;
        ShowInTaskbar=false;
        var area=Screen.PrimaryScreen?.WorkingArea??new Rectangle(0,0,1200,800);
        ClientSize=new Size(Math.Min(990,area.Width-60),Math.Min(590,area.Height-80));
        MinimumSize=new Size(720,460);
        BackColor=Color.FromArgb(247,249,252);
        Font=new Font("Segoe UI",12);
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24,18,24,18),
            ColumnCount=1,RowCount=4};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
        root.Controls.Add(new Label {Dock=DockStyle.Fill,Text=heading,
            ForeColor=canPrint?_navy:Color.FromArgb(165,40,36),
            Font=new Font("Segoe UI",22,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleCenter},0,0);
        root.Controls.Add(new Label {Dock=DockStyle.Fill,Text=countText,
            BackColor=Color.White,
            ForeColor=canPrint?Color.FromArgb(0,105,49):_red,
            Font=new Font("Segoe UI",49,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleCenter},0,1);
        _auditHost.Margin=new Padding(0,12,0,8);
        root.Controls.Add(_auditHost,0,2);

        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=canPrint?3:2};
        for(var i=0;i<actions.ColumnCount;i++)
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/actions.ColumnCount));
        var print=MakeButton("OK · IN TEM",Color.FromArgb(23,132,75));
        var correct=MakeButton("SỬA SỐ KIỆN",Color.FromArgb(196,118,28));
        var exit=MakeButton("THOÁT",Color.FromArgb(100,110,120));
        print.Click+=(_,_)=>Finish(CartonDecision.Print);
        correct.Click+=(_,_)=>Finish(CartonDecision.Correct);
        exit.Click+=(_,_)=>Finish(CartonDecision.Exit);
        var next=0;
        if(canPrint)actions.Controls.Add(print,next++,0);
        actions.Controls.Add(correct,next++,0);
        actions.Controls.Add(exit,next,0);
        root.Controls.Add(actions,0,3);
        Controls.Add(root);
        if(canPrint)AcceptButton=print;
        CancelButton=exit;
        UpdateSkuAudit(null);
    }

    public void UpdateSkuAudit(SkuAuditResult? audit)
    {
        if(IsDisposed)return;
        _auditHost.SuspendLayout();
        try
        {
            _auditHost.Controls.Clear();
            if(audit is null)
            {
                AddNotice("ĐANG ĐỐI CHIẾU SKU...",Color.FromArgb(53,76,101),
                    "Hệ thống đang đọc THÔNG TIN CHÍNH. Có thể xác nhận hoặc sửa số kiện.");
                return;
            }
            if(audit.state=="EMPTY_WATCH")
            {
                AddNotice("CHƯA CÓ SKU CẦN KIỂM TRA",Color.FromArgb(53,76,101),
                    "Danh sách SKU hiện đang trống. Kiểm tra số kiện thực tế trước khi in.");
                return;
            }
            if(audit.state!="OK")
            {
                AddNotice("CHƯA ĐỌC ĐƯỢC DANH SÁCH SKU",_red,
                    "Không thể kết luận đơn hàng không có SKU cần kiểm tra. "+
                    "Hãy kiểm tra tab WMS · THÔNG TIN CHÍNH thủ công.");
                return;
            }
            if(audit.matches.Count==0)
            {
                AddNotice("KHÔNG CÓ SKU CẦN CẢNH BÁO",Color.FromArgb(20,106,62),
                    "Đã đối chiếu "+audit.totalRows+" dòng SKU. Kiểm tra số kiện thực tế trước khi in.");
                return;
            }
            var rows=audit.matches;
            var table=new TableLayoutPanel {
                Dock=DockStyle.Top,ColumnCount=5,RowCount=rows.Count+1,
                Height=65+rows.Count*68,Margin=new Padding(0),BackColor=Color.FromArgb(255,244,244)
            };
            foreach(var pct in new[]{20f,39f,11f,14f,16f})
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,pct));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,65));
            var heading=new Label {
                Text="⚠ CÓ SKU CẦN KIỂM TRA ("+rows.Count+")  ·  Kiểm tra hàng trước khi in tem",
                Dock=DockStyle.Fill,Font=new Font("Segoe UI",13,FontStyle.Bold),
                ForeColor=_red,TextAlign=ContentAlignment.MiddleLeft,
                Padding=new Padding(14,6,6,4)
            };
            table.Controls.Add(heading,0,0);
            table.SetColumnSpan(heading,5);
            for(var i=0;i<rows.Count;i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
                var item=rows[i];
                var texts=new [] {
                    "SKU: "+item.sku,
                    item.productName,
                    "PCB: "+item.pcb,
                    "SL Đặt: "+item.orderedQuantity,
                    "Base Unit: "+item.baseUnit
                };
                for(var col=0;col<texts.Length;col++)
                {
                    var label=new Label {
                        Text=texts[col],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
                        Padding=new Padding(8,4,4,4),Margin=new Padding(1),
                        BorderStyle=BorderStyle.FixedSingle,BackColor=Color.White,
                        Font=new Font("Segoe UI",col==1?10.5f:11f,
                            col==0?FontStyle.Bold:FontStyle.Regular),
                        ForeColor=col==0?_red:Color.FromArgb(30,39,50)
                    };
                    new ToolTip().SetToolTip(label,texts[col]);
                    table.Controls.Add(label,col,i+1);
                }
            }
            _auditHost.Controls.Add(table);
            if(!_canPrint)
            {
                var invalid=new Label {
                    Text=_invalidInstructions,Dock=DockStyle.Top,Height=62,
                    TextAlign=ContentAlignment.MiddleCenter,
                    ForeColor=_red,Font=new Font("Segoe UI",10,FontStyle.Bold)
                };
                _auditHost.Controls.Add(invalid);
                invalid.BringToFront();
            }
        }
        finally { _auditHost.ResumeLayout(true); }
    }

    private void AddNotice(string heading,Color color,string subtext)
    {
        var panel=new TableLayoutPanel {Dock=DockStyle.Top,Height=132,
            BackColor=Color.White,RowCount=2,ColumnCount=1};
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        panel.Controls.Add(new Label {Text=heading,Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=color,
            TextAlign=ContentAlignment.MiddleCenter},0,0);
        panel.Controls.Add(new Label {
            Text=_canPrint?subtext:(_invalidInstructions+"\n"+subtext),
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),
            ForeColor=Color.FromArgb(52,62,73),
            TextAlign=ContentAlignment.MiddleCenter
        },0,1);
        _auditHost.Controls.Add(panel);
    }

    private static Button MakeButton(string text,Color color)=>new()
    {
        Text=text,Dock=DockStyle.Fill,BackColor=color,ForeColor=Color.White,
        Font=new Font("Segoe UI",15,FontStyle.Bold),
        FlatStyle=FlatStyle.Flat,Margin=new Padding(5),UseVisualStyleBackColor=false
    };

    private void Finish(CartonDecision decision)
    {
        Decision=decision;
        DialogResult=DialogResult.OK;
        Close();
    }
}
