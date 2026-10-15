using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SupraAutoPackLe;

public sealed class MainForm : Form
{
    private readonly LocalStore _store=new();
    private readonly WmsBrowser _browser;
    private readonly Stopwatch _watch=new();
    private readonly TextBox _tote=new(){Width=320,Font=new Font("Segoe UI",14)};
    private readonly TextBox _qty=new(){Width=160,Font=new Font("Segoe UI",14)};
    private readonly Button _ok=MakeButton("OK – CHỌN NHÂN SỰ",200);
    private readonly Button _show=MakeButton("HIỆN WEB",115);
    private readonly Button _hide=MakeButton("ẨN WEB",110);
    private readonly Button _stop=MakeButton("DỪNG / XỬ LÝ TAY",195);
    private readonly Button _recover=MakeButton("XÁC MINH WMS SẴN SÀNG",215);
    private readonly Button _peopleButton=MakeButton("QUẢN LÝ NHÂN SỰ",190);
    private readonly Button _exportXlsx=MakeButton("XUẤT LỊCH SỬ XLSX",190);
    private readonly Button _exportLog=MakeButton("XUẤT LOG",135);
    private readonly Button _update=MakeButton("CẬP NHẬT",130);
    private readonly Label _status=new(){AutoSize=false,Height=68,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    private readonly DataGridView _history=new(){
        Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
        AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.AllCells,BackgroundColor=Color.White
    };
    private readonly Color _navy=Color.FromArgb(26,43,69);
    private readonly Label _historyHeading=new(){AutoSize=false,Dock=DockStyle.Fill,
        Font=new Font("Segoe UI",11,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
    private DateTime _displayDate=DateTime.Today;
    private readonly System.Windows.Forms.Timer _midnightCheck=new(){Interval=60_000};
    private List<string> _people;
    private CancellationTokenSource? _cancel;
    private bool _busy;
    private bool _needsRecovery;
    private string _stage="INIT";
    private static readonly Regex ToteFormat=new("^[A-Za-z0-9_-]{2,64}$",RegexOptions.Compiled);
    private static readonly Regex CodeFormat=new("^[A-Za-z0-9_-]{5,50}$",RegexOptions.Compiled);

    public MainForm()
    {
        _people=_store.LoadPeople();
        _browser=new WmsBrowser(_store);
        Text="SUPRA – AUTO IN TEM PACK LẺ | "+Application.ProductVersion;
        Font=new Font("Segoe UI",10);
        MinimumSize=new Size(780,610);Width=1040;Height=790;
        StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(247,249,252);
        Compose();
        RefreshHistory();
        _midnightCheck.Tick+=(_,_)=>{if(_displayDate!=DateTime.Today)RefreshHistory();};
        _midnightCheck.Start();
        Status("INIT","Mở cửa sổ WMS, đăng nhập và chọn máy in trước khi dùng.");
        Shown+=async (_,_)=>{
            _browser.Reveal();
            try
            {
                await _browser.InitializeAsync();
                Status("READY","Đăng nhập WMS và chọn máy in. Sau đó ẩn cửa sổ WMS để thao tác.");
            }
            catch { Status("FAULT","Không khởi động được WMS. Cần WebView2 Runtime."); }
            _tote.Focus();
        };
        FormClosing+=(_,_)=>{
            _midnightCheck.Stop();
            _midnightCheck.Dispose();
            _cancel?.Cancel();
            _store.Trace("APP","CLOSING",_busy?"UNCERTAIN":"OK",_watch.ElapsedMilliseconds);
            _browser.Terminate();
        };
    }
    private static Button MakeButton(string label,int width)
    {
        return new Button{Text=label,Width=width,Height=38,
          FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(30,102,175),
          ForeColor=Color.White,UseVisualStyleBackColor=false};
    }
    private void Compose()
    {
        var title=new Panel{Dock=DockStyle.Top,Height=62,BackColor=_navy};
        title.Controls.Add(new Label{Text="SUPRA  |  AUTO IN TEM PACK LẺ",Dock=DockStyle.Fill,
            Padding=new Padding(18,0,0,0),TextAlign=ContentAlignment.MiddleLeft,
            ForeColor=Color.White,Font=new Font("Segoe UI",17,FontStyle.Bold)});
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(16,12,16,12)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,94));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var inputs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,8)};
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        var t=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,10,0)};
        t.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
        t.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        t.Controls.Add(new Label{Text="QUÉT MÃ TOTE",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
        _tote.Dock=DockStyle.Fill;_tote.Margin=new Padding(0,0,0,6);t.Controls.Add(_tote,0,1);
        var q=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,10,0)};
        q.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
        q.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        q.Controls.Add(new Label{Text="SỐ KIỆN",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
        _qty.Dock=DockStyle.Fill;_qty.Margin=new Padding(0,0,0,6);q.Controls.Add(_qty,0,1);
        _ok.Dock=DockStyle.Fill;_ok.Margin=new Padding(2,16,0,6);
        _ok.BackColor=Color.FromArgb(25,122,78);
        inputs.Controls.Add(t,0,0);inputs.Controls.Add(q,1,0);inputs.Controls.Add(_ok,2,0);
        root.Controls.Add(inputs,0,0);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,
            FlowDirection=FlowDirection.LeftToRight,Margin=new Padding(0),Padding=new Padding(0,6,0,2)};
        actions.Controls.AddRange([_peopleButton,_show,_hide,_stop,_recover,_update]);
        root.Controls.Add(actions,0,1);
        _status.Dock=DockStyle.Fill;_status.Height=46;_status.AutoSize=false;
        _status.Padding=new Padding(12,0,8,0);_status.TextAlign=ContentAlignment.MiddleLeft;
        _status.BackColor=Color.White;_status.Margin=new Padding(0,2,0,7);
        root.Controls.Add(_status,0,2);
        var historyToolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};
        historyToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        historyToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));
        historyToolbar.Controls.Add(_historyHeading,0,0);
        var exports=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,
            FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0)};
        _exportXlsx.Width=175;_exportLog.Width=125;
        exports.Controls.AddRange([_exportLog,_exportXlsx]);
        historyToolbar.Controls.Add(exports,1,0);
        root.Controls.Add(historyToolbar,0,3);
        _history.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        _history.BackgroundColor=Color.White;
        _history.BorderStyle=BorderStyle.FixedSingle;
        _history.RowHeadersVisible=false;
        _history.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        _history.MultiSelect=false;
        _history.ColumnHeadersHeight=38;
        _history.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _history.RowTemplate.Height=32;
        _history.EnableHeadersVisualStyles=false;
        _history.ColumnHeadersDefaultCellStyle.BackColor=_navy;
        _history.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        _history.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
        _history.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(244,247,251);
        _history.DefaultCellStyle.SelectionBackColor=Color.FromArgb(215,228,244);
        _history.DefaultCellStyle.SelectionForeColor=Color.FromArgb(20,36,55);
        root.Controls.Add(_history,0,4);
        Controls.Add(root);Controls.Add(title);
        var columns=new[]{"Ngày","Bắt đầu","Kết thúc","Giây","Nhân sự","Mã SO","Mã DO","Kết quả"};
        foreach(var item in columns)_history.Columns.Add(item,item);
        var weights=new float[]{13,10,10,6,13,13,13,22};
        for(var i=0;i<weights.Length;i++){_history.Columns[i].FillWeight=weights[i];_history.Columns[i].MinimumWidth=i==7?175:65;}
        _tote.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_qty.Focus();_qty.SelectAll();}};
        _qty.KeyDown+=async (_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await OnSubmitAsync();}};
        _ok.Click+=async (_,_)=>await OnSubmitAsync();
        _peopleButton.Click+=(_,_)=>ManagePeople();
        _show.Click+=(_,_)=>_browser.Reveal();
        _hide.Click+=(_,_)=>_browser.Hide();
        _stop.Click+=(_,_)=>{
            if(!_busy){_browser.Reveal();return;}
            if(MessageBox.Show("Dừng xử lý tự động? Nếu lệnh in đã gửi, KHÔNG được tự in lại.",
                "Xác nhận",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes)
            {_cancel?.Cancel();_browser.Reveal();}
        };
        _recover.Click+=async (_,_)=>{
            var p=await _browser.PeekAsync();
            if(p?.ready==true){_needsRecovery=false;Status("READY","Đã xác nhận WMS về ô Tote.");
                UpdateControls();_tote.Focus();}
            else{_browser.Reveal();MessageBox.Show("WMS chưa trở về màn hình quét Tote. Hãy hoàn tất bằng tay.");}
        };
        _exportXlsx.Click+=(_,_)=>ExportXlsx();
        _exportLog.Click+=(_,_)=>ExportLogs();
        _update.Click+=async (_,_)=>{
            if(_busy){MessageBox.Show("Không cập nhật khi đang xử lý Tote.");return;}
            _update.Enabled=false;
            try{await UpdateClient.CheckAndInstallAsync(this,_store);}
            finally{_update.Enabled=true;}
        };
        UpdateControls();
    }
    private void UpdateControls()
    {
        var allow=!_busy&&!_needsRecovery;
        _ok.Enabled=allow;_tote.Enabled=allow;_qty.Enabled=allow;
        _peopleButton.Enabled=!_busy;_recover.Enabled=!_busy&&_needsRecovery;
        _update.Enabled=!_busy;
    }
    private void Status(string stage,string message)
    {
        _stage=stage;
        var title=stage switch
        {
            "INIT"=>"Khởi tạo","READY"=>"Sẵn sàng","FAULT"=>"Cần kiểm tra WMS",
            "TOTE"=>"Nhập Tote","PACK"=>"Đóng gói","QUANTITY"=>"Số kiện",
            "UPDATE"=>"Xác nhận","PRINT"=>"In nhãn","FINISH"=>"Hoàn tất",
            "RETURN"=>"Chờ màn hình quét",_=>stage
        };
        _status.Text="TRẠNG THÁI: "+title+"  |  "+message;
        _store.Trace(stage,"STAGE_CHANGED","OK",_watch.ElapsedMilliseconds);
    }
    private void RefreshHistory()
    {
        _displayDate=DateTime.Today;
        _history.Rows.Clear();
        var today=_store.FilterHistory(_displayDate,_displayDate);
        foreach(var r in today.Reverse().Take(500))
            _history.Rows.Add(r.Date,r.Start,r.End,r.Seconds,r.Person,r.SO,r.DO,LocalStore.ResultLabel(r.Result));
        _historyHeading.Text="LỊCH SỬ HÔM NAY  |  "+_displayDate.ToString("dd/MM/yyyy")+"  |  "+today.Count+" lượt";
    }
    private static string Summary(BrowserPeek? p) => p==null?"NO_SNAPSHOT":
        $"T={p.toteCount};P={p.packCount};Q={p.qtyCount};U={p.updateCount};R={p.printCount};L={p.printLabelCount};F={p.finishCount};Y={p.yesCount};N={p.noCount};D={p.dialogCount};S={(p.outboundSlipPrompt?1:0)}";
    private async Task<BrowserPeek> WaitAsync(Func<BrowserPeek,bool> predicate,string code,int seconds,CancellationToken ct)
    {
        var time=Stopwatch.StartNew();
        _store.Trace(_stage,"WAIT_"+code,"WAIT",_watch.ElapsedMilliseconds);
        BrowserPeek? last=null;
        var stableSamples=0;
        while(time.Elapsed<TimeSpan.FromSeconds(seconds))
        {
            ct.ThrowIfCancellationRequested();
            last=await _browser.PeekAsync();
            if(last?.ok==true && predicate(last))
            {
                // Two consecutive observations instead of clicking on a transient render.
                if(++stableSamples>=2)
                {
                    _store.Trace(_stage,"WAIT_"+code,"DONE",_watch.ElapsedMilliseconds,Summary(last));
                    return last;
                }
            }
            else stableSamples=0;
            await Task.Delay(350,ct);
        }
        _store.Trace(_stage,"WAIT_"+code,"TIMEOUT",_watch.ElapsedMilliseconds,Summary(last));
        throw new WorkflowHalt("Hết thời gian chờ bước "+code+". Không tự lặp lại lệnh có thể in/đóng gói trùng.");
    }
    private async Task ActionAsync(string action,string? value,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var r=await _browser.PerformAsync(action,value);
        var reason=Regex.IsMatch(r.reason??"","^[A-Z0-9_]{1,60}$")?r.reason:"UNSPECIFIED";
        _store.Trace(_stage,"ACTION_"+action,r.ok?"OK":"BLOCKED",_watch.ElapsedMilliseconds,r.ok?"DONE":reason);
        if(!r.ok)throw new WorkflowHalt("Không xác định được đối tượng WMS ở bước "+action+". Mở Web để xử lý.");
    }
    private async Task OnSubmitAsync()
    {
        if(_busy||_needsRecovery)return;
        var tote=_tote.Text.Trim().ToUpperInvariant();
        if(!ToteFormat.IsMatch(tote)){MessageBox.Show("Mã Tote không hợp lệ.");return;}
        if(!int.TryParse(_qty.Text.Trim(),out var count)||count<1||count>9999)
        {MessageBox.Show("Số kiện phải từ 1 đến 9999.");return;}
        var ready=await _browser.PeekAsync();
        if(ready?.ready!=true)
        {_browser.Reveal();MessageBox.Show("WMS chưa ở màn hình quét Tote. Vui lòng đăng nhập và chọn máy in.");return;}
        _people=_store.LoadPeople();
        if(_people.Count==0){ManagePeople();return;}
        var person=ChoosePerson();
        if(person==null)return;
        if(MessageBox.Show("Tote: "+tote+"\nSố kiện: "+count+"\nNgười thực hiện: "+person+
            "\n\nBắt đầu đóng gói và in nhãn?", "XÁC NHẬN",MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question)!=DialogResult.OK)return;
        _busy=true;_cancel=new CancellationTokenSource();_watch.Restart();UpdateControls();
        var started=DateTime.Now;
        string so="",doCode="";var success=false;var resultCode="UNCERTAIN_CHECK_WMS";
        _store.Trace("TOTE","JOB_STARTED","OK");
        try
        {
            var ct=_cancel.Token;
            Status("TOTE","Nhập Tote và Enter...");
            await ActionAsync("TOTE",tote,ct);
            await _browser.EnterAsync();
            var p=await WaitAsync(x=>x.packCount==1,"PACK_BUTTON",30,ct);
            so=p.so;doCode=p.Do;
            if(!CodeFormat.IsMatch(so)||!CodeFormat.IsMatch(doCode))
                throw new WorkflowHalt("Không đọc được Mã SO/Mã DO trong THÔNG TIN SO ĐANG ĐÓNG GÓI.");
            Status("PACK","Click ĐÓNG GÓI HÀNG CHẴN");
            await ActionAsync("PACK",null,ct);
            await WaitAsync(x=>x.qtyCount==1&&x.updateCount==1,"COUNT_DIALOG",20,ct);
            Status("QUANTITY","Thay hẳn giá trị số kiện cũ...");
            await ActionAsync("COUNT",count.ToString(),ct);
            await ActionAsync("UPDATE",null,ct);
            await WaitAsync(x=>x.yesCount==1&&x.dialogCount==1,"UPDATE_CONFIRM",20,ct);
            Status("UPDATE","Xác nhận Cập Nhật → Có");
            await ActionAsync("YES",null,ct);
            await WaitAsync(x=>x.printCount==1&&x.finishCount==1&&x.dialogCount==0,"PACKED_PAGE",30,ct);
            Status("PRINT","Mở hộp thoại in nhãn...");
            await ActionAsync("PRINT",null,ct);
            await WaitAsync(x=>x.printLabelCount==1&&x.dialogCount==1,"PRINT_DIALOG",20,ct);
            await ActionAsync("PRINT_LABEL",null,ct);
            await WaitAsync(x=>x.printLabelCount==0&&x.dialogCount==0&&x.finishCount==1,"PRINT_CLOSED",25,ct);
            Status("FINISH","Hoàn thành đóng gói...");
            await ActionAsync("FINISH",null,ct);
            await WaitAsync(x=>x.yesCount==1&&x.dialogCount==1,"FINISH_CONFIRM",20,ct);
            await ActionAsync("YES",null,ct);
            await WaitAsync(x=>x.noCount==1&&x.dialogCount==1&&x.outboundSlipPrompt,"NO_INSPECTION_DIALOG",20,ct);
            await ActionAsync("NO",null,ct);
            Status("RETURN","Chờ quay về ô quét PTVC...");
            await WaitAsync(x=>x.ready&&x.toteCount==1&&x.dialogCount==0,"RETURN_PTVC",35,ct);
            success=true;
            _store.Trace("RETURN","JOB_UI_COMPLETE","DONE",_watch.ElapsedMilliseconds);
            Status("READY","Đã trở về ô Tote; cho phép bắn lượt tiếp theo.");
        }
        catch(OperationCanceledException)
        {
            resultCode="OPERATOR_CANCELLED";
            _store.Trace("STOP","JOB_INTERRUPTED","UNCERTAIN",_watch.ElapsedMilliseconds);
            Status("FAULT","Đã dừng. Mở Web kiểm tra thủ công; KHÔNG tự in lại.");
            _needsRecovery=true;_browser.Reveal();
        }
        catch(WorkflowHalt e)
        {
            _store.Trace("FAULT","JOB_HALTED","UNCERTAIN",_watch.ElapsedMilliseconds);
            Status("FAULT",e.Message);
            _needsRecovery=true;_browser.Reveal();
        }
        catch
        {
            _store.Trace("FAULT","JOB_EXCEPTION","UNCERTAIN",_watch.ElapsedMilliseconds);
            Status("FAULT","Lỗi bất ngờ, mở WMS xử lý bằng tay.");
            _needsRecovery=true;_browser.Reveal();
        }
        finally
        {
            _watch.Stop();
            var ended=DateTime.Now;
            _store.AppendHistory(new HistoryRow(started.ToString("dd/MM/yyyy"),
                started.ToString("HH:mm:ss"),ended.ToString("HH:mm:ss"),
                (long)Math.Round(_watch.Elapsed.TotalSeconds),person,so,doCode,
                success?"UI_WORKFLOW_COMPLETE":resultCode));
            RefreshHistory();
            _tote.Clear();_qty.Clear();
            _busy=false;_cancel?.Dispose();_cancel=null;
            UpdateControls();_tote.Focus();
        }
    }
    private string? ChoosePerson()
    {
        using var f=new Form{Text="CHỌN NHÂN SỰ – không có tên mặc định",Width=630,Height=520,
            StartPosition=FormStartPosition.CenterParent};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(12)};
        string? selected=null;
        foreach(var name in _people)
        {
            var copy=name;var b=MakeButton(copy,210);b.Height=44;
            b.Click+=(_,_)=>{selected=copy;f.DialogResult=DialogResult.OK;f.Close();};
            panel.Controls.Add(b);
        }
        f.Controls.Add(panel);
        return f.ShowDialog(this)==DialogResult.OK?selected:null;
    }
    private void ManagePeople()
    {
        if(_busy)return;
        using var f=new Form{Text="Nhân sự dùng chung 2 ca",Width=610,Height=560,StartPosition=FormStartPosition.CenterParent};
        var list=new ListBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",11)};
        void Reload(){_people=_store.LoadPeople();list.Items.Clear();list.Items.AddRange(_people.Cast<object>().ToArray());}
        Reload();
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=100,AutoScroll=true,WrapContents=true};
        var add=MakeButton("THÊM",82);var bulk=MakeButton("NHẬP DẤU PHẨY",150);
        var edit=MakeButton("SỬA",82);var delete=MakeButton("XÓA",82);var clear=MakeButton("XÓA TOÀN BỘ",145);
        add.Click+=(_,_)=>{var s=Prompt("Thêm tên","");if(s!=null){_people.Add(s);_store.SavePeople(_people);Reload();}};
        bulk.Click+=(_,_)=>{
            var s=Prompt("Nhập nhiều tên, cách nhau bằng dấu phẩy","");
            if(s==null)return;
            var parts=s.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(parts.Length<1||parts.Length>200||parts.Any(x=>!LocalStore.ValidName(x)))
            {MessageBox.Show("Danh sách phải có 1–200 tên hợp lệ, mỗi tên tối đa 70 ký tự.");return;}
            _people.AddRange(parts);_store.SavePeople(_people);Reload();
        };
        edit.Click+=(_,_)=>{if(list.SelectedIndex<0)return;
            var i=list.SelectedIndex;var s=Prompt("Sửa nhân sự",_people[i]);
            if(s!=null){_people[i]=s;_store.SavePeople(_people);Reload();}};
        delete.Click+=(_,_)=>{if(list.SelectedIndex<0)return;
            if(MessageBox.Show("Xóa người được chọn?", "Xác nhận",MessageBoxButtons.YesNo)==DialogResult.Yes)
            {_people.RemoveAt(list.SelectedIndex);_store.SavePeople(_people);Reload();}};
        clear.Click+=(_,_)=>{
            if(MessageBox.Show("XÓA TOÀN BỘ DANH SÁCH NHÂN SỰ? Lịch sử đã ghi vẫn được giữ.",
                "Xác nhận xóa toàn bộ",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes)
            {_people.Clear();_store.SavePeople(_people);Reload();}
        };
        buttons.Controls.AddRange([add,bulk,edit,delete,clear]);
        f.Controls.Add(list);f.Controls.Add(buttons);f.ShowDialog(this);
    }
    private string? Prompt(string title,string initial)
    {
        using var f=new Form{Text=title,Width=490,Height=185,StartPosition=FormStartPosition.CenterParent};
        var input=new TextBox{Text=initial,Location=new Point(16,16),Width=440};
        var ok=MakeButton("LƯU",105);ok.Location=new Point(16,65);
        ok.Click+=(_,_)=>{if(input.Text.Length>12000){MessageBox.Show("Nội dung quá dài.");return;}
            f.DialogResult=DialogResult.OK;f.Close();};
        f.Controls.AddRange([input,ok]);
        if(f.ShowDialog(this)!=DialogResult.OK)return null;
        var value=input.Text.Trim();
        if(!LocalStore.ValidName(value)&&!value.Contains(','))
        {MessageBox.Show("Tên không hợp lệ.");return null;}
        return value;
    }
    private void ExportXlsx()
    {
        using var f=new Form{Text="Bộ lọc xuất lịch sử Excel",Width=480,Height=292,
            MinimumSize=new Size(480,292),StartPosition=FormStartPosition.CenterParent,
            FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var mode=new ComboBox{Left=18,Top=20,Width=420,DropDownStyle=ComboBoxStyle.DropDownList};
        mode.Items.AddRange(["Tất cả","Một ngày","Khoảng ngày"]);mode.SelectedIndex=1;
        var from=new DateTimePicker{Left=18,Top=65,Width=200,Format=DateTimePickerFormat.Short,Value=DateTime.Today};
        var to=new DateTimePicker{Left=235,Top=65,Width=200,Format=DateTimePickerFormat.Short,Value=DateTime.Today};
        var result=new ComboBox{Left=18,Top=110,Width=420,DropDownStyle=ComboBoxStyle.DropDownList};
        result.Items.AddRange(["Tất cả kết quả","Đã hoàn tất trên WMS","Chưa hoàn tất / cần kiểm tra"]);
        result.SelectedIndex=0;
        void SyncDates(){from.Enabled=mode.SelectedIndex!=0;to.Enabled=mode.SelectedIndex==2;}
        mode.SelectedIndexChanged+=(_,_)=>SyncDates();SyncDates();
        var ok=MakeButton("TIẾP TỤC XUẤT EXCEL",215);ok.Left=18;ok.Top=164;
        ok.Click+=(_,_)=>{
            if(mode.SelectedIndex==2&&to.Value.Date<from.Value.Date)
            {MessageBox.Show(f,"Ngày kết thúc không được trước ngày bắt đầu.");return;}
            f.DialogResult=DialogResult.OK;f.Close();
        };
        f.Controls.AddRange([mode,from,to,result,ok]);
        if(f.ShowDialog(this)!=DialogResult.OK)return;
        DateTime? start=mode.SelectedIndex==0?null:from.Value.Date;
        DateTime? end=mode.SelectedIndex==0?null:mode.SelectedIndex==1?from.Value.Date:to.Value.Date;
        var filter=result.SelectedIndex switch{1=>"COMPLETED",2=>"INCOMPLETE",_=>"ALL"};
        using var dialog=new SaveFileDialog{Title="Xuất lịch sử XLSX",
            Filter="Excel Workbook (*.xlsx)|*.xlsx",
            FileName="SUPRA_Lich_Su_In_Tem_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".xlsx"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            _store.ExportHistoryXlsx(dialog.FileName,start,end,filter);
            _store.Trace("EXPORT","HISTORY_XLSX","DONE");
            MessageBox.Show(this,"Đã xuất lịch sử Excel theo bộ lọc đã chọn.");
        }
        catch
        {
            _store.Trace("EXPORT","HISTORY_XLSX","ERROR");
            MessageBox.Show(this,"Xuất lịch sử thất bại. Kiểm tra file Excel có đang mở không.");
        }
    }
    private void ExportLogs()
    {
        using var f=new Form{Text="CHỌN NGÀY XUẤT LOG",Width=435,Height=240,StartPosition=FormStartPosition.CenterParent};
        var mode=new ComboBox{Location=new Point(16,18),Width=365,DropDownStyle=ComboBoxStyle.DropDownList};
        mode.Items.AddRange(["Tất cả","Một ngày","Khoảng ngày"]);mode.SelectedIndex=0;
        var from=new DateTimePicker{Location=new Point(16,64),Width=170,Format=DateTimePickerFormat.Short,Value=DateTime.Today};
        var to=new DateTimePicker{Location=new Point(205,64),Width=170,Format=DateTimePickerFormat.Short,Value=DateTime.Today};
        var ok=MakeButton("XUẤT ZIP LOG",160);ok.Location=new Point(16,120);
        ok.Click+=(_,_)=>{f.DialogResult=DialogResult.OK;f.Close();};
        f.Controls.AddRange([mode,from,to,ok]);
        if(f.ShowDialog(this)!=DialogResult.OK)return;
        DateTime? start=mode.SelectedIndex==0?null:from.Value.Date;
        DateTime? end=mode.SelectedIndex==0?null:mode.SelectedIndex==1?from.Value.Date:to.Value.Date;
        if(start.HasValue&&end.HasValue&&end<start){MessageBox.Show("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");return;}
        using var dialog=new SaveFileDialog{Filter="ZIP (*.zip)|*.zip",
            FileName="SUPRA_Logs_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".zip"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{_store.Trace("EXPORT","LOG_EXPORT","OK");_store.ExportLogs(dialog.FileName,start,end);MessageBox.Show("Đã xuất log ZIP.");}
        catch{_store.Trace("EXPORT","LOG_EXPORT","ERROR");MessageBox.Show("Xuất log thất bại.");}
    }
}
internal sealed class WorkflowHalt : Exception
{
    public WorkflowHalt(string message):base(message){}
}
