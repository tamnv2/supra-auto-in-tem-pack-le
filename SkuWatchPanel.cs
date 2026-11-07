namespace SupraAutoPackLe;

/// <summary>CRUD list used only for read-only SO matching in the even-label flow.</summary>
internal sealed class SkuWatchPanel : UserControl
{
    private readonly SkuWatchStore _store;
    private readonly LocalStore _log;
    private readonly DataGridView _grid=new() {
        Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false,
        AllowUserToDeleteRows=false, MultiSelect=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible=false,
        BackgroundColor=Color.White, BorderStyle=BorderStyle.None
    };
    private readonly Label _status=new() {Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
        Font=new Font("Segoe UI",10),ForeColor=Color.FromArgb(50,65,86)};

    public SkuWatchPanel(SkuWatchStore store,LocalStore log)
    {
        _store=store;_log=log;Dock=DockStyle.Fill;
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20),
            RowCount=3,ColumnCount=1,BackColor=Color.FromArgb(247,249,252)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,370));
        header.Controls.Add(new Label{Text="SKU CẦN KIỂM TRA",Dock=DockStyle.Fill,
            TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",17,FontStyle.Bold),
            ForeColor=Color.FromArgb(23,48,76)},0,0);
        var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
        foreach(var _ in Enumerable.Range(0,3))buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var add=Button("THÊM",Color.FromArgb(190,38,50));
        var edit=Button("SỬA",Color.FromArgb(50,88,137));
        var delete=Button("XÓA",Color.FromArgb(106,113,122));
        buttons.Controls.Add(add,0,0);buttons.Controls.Add(edit,1,0);buttons.Controls.Add(delete,2,0);
        header.Controls.Add(buttons,1,0);
        root.Controls.Add(header,0,0);
        root.Controls.Add(_status,0,1);
        _grid.Columns.Add("STT","STT");
        _grid.Columns.Add("SKU","SKU");
        _grid.Columns.Add("Added","Thời gian thêm");
        _grid.Columns.Add("Updated","Sửa lần cuối");
        _grid.Columns[0].FillWeight=13;
        _grid.Columns[1].FillWeight=35;
        _grid.Columns[2].FillWeight=26;
        _grid.Columns[3].FillWeight=26;
        root.Controls.Add(_grid,0,2);
        Controls.Add(root);
        add.Click+=(_,_)=>Change("ADD");
        edit.Click+=(_,_)=>Change("EDIT");
        delete.Click+=(_,_)=>Change("DELETE");
        _grid.CellDoubleClick+=(_,_)=>Change("EDIT");
        RefreshList();
    }

    private static Button Button(string text,Color color)=>new() {
        Text=text,Dock=DockStyle.Fill,BackColor=color,ForeColor=Color.White,
        Font=new Font("Segoe UI",10,FontStyle.Bold),FlatStyle=FlatStyle.Flat,
        Margin=new Padding(4),UseVisualStyleBackColor=false
    };

    private string? SelectedSku() =>
        _grid.SelectedRows.Count==1 ? _grid.SelectedRows[0].Cells[1].Value?.ToString():null;

    public void RefreshList()
    {
        try
        {
            var rows=_store.Items();
            _grid.Rows.Clear();
            var index=0;
            foreach(var row in rows)
                _grid.Rows.Add(++index,row.SKU,row.AddedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                    row.UpdatedAt.ToString("dd/MM/yyyy HH:mm:ss"));
            _status.Text=$"Danh sách lưu trên máy này: {rows.Count} SKU. Chỉ đối chiếu khi quét SO; không sửa dữ liệu WMS.";
        }
        catch(Exception)
        {
            _status.Text="Lỗi đọc danh sách SKU; kiểm tra tệp dữ liệu trước khi dùng.";
            _log.Trace("SKU","WATCH_LIST_READ","ERROR");
            MessageBox.Show(this,"Không đọc được danh sách SKU đã lưu. Không tự xóa dữ liệu.",
                "SKU cần kiểm tra",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void Change(string operation)
    {
        var selected=SelectedSku();
        if(operation!="ADD" && selected==null)
        {
            MessageBox.Show(this,"Chọn một dòng SKU trước khi thao tác.");return;
        }
        if(operation=="DELETE" && MessageBox.Show(this,
            "Xóa SKU đã chọn khỏi danh sách cần kiểm tra?","Xác nhận xóa",
            MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        string? value=operation=="DELETE"?selected:AskSku(operation=="ADD"?"Thêm SKU":"Sửa SKU",selected??"");
        if(value==null)return;
        try
        {
            if(operation=="ADD")_store.Add(value);
            else if(operation=="EDIT")_store.Edit(selected!,value);
            else _store.Delete(selected!);
            _log.Trace("SKU","WATCH_LIST_"+operation,"DONE");
            RefreshList();
        }
        catch(Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
            or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            _log.Trace("SKU","WATCH_LIST_"+operation,"ERROR");
            MessageBox.Show(this,ex is IOException or UnauthorizedAccessException or InvalidDataException
                or System.Text.Json.JsonException ? "Không thể cập nhật danh sách SKU trên máy này.":ex.Message,
                "SKU cần kiểm tra",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }

    private string? AskSku(string heading,string old)
    {
        using var dialog=new Form {
            Text=heading,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,
            ClientSize=new Size(475,170),MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false
        };
        var box=new TextBox{Text=old,Font=new Font("Segoe UI",15,FontStyle.Bold),
            Location=new Point(23,45),Width=425,MaxLength=50};
        var ok=new Button{Text="LƯU",DialogResult=DialogResult.OK,
            Location=new Point(220,112),Size=new Size(110,40)};
        var cancel=new Button{Text="HỦY",DialogResult=DialogResult.Cancel,
            Location=new Point(337,112),Size=new Size(110,40)};
        dialog.Controls.Add(new Label{Text="Mã SKU",Location=new Point(23,13),AutoSize=true});
        dialog.Controls.Add(box);dialog.Controls.Add(ok);dialog.Controls.Add(cancel);
        dialog.AcceptButton=ok;dialog.CancelButton=cancel;
        return dialog.ShowDialog(this)==DialogResult.OK?box.Text:null;
    }
}
