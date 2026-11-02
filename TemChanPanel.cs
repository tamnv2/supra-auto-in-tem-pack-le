using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SupraAutoPackLe;

/// <summary>
/// Carton-label operation: SO + Enter only. Separate verified carton-correction
/// audit and XLSX reporting; no writes to loose packing personnel/productivity.
/// </summary>
internal sealed class TemChanPanel : UserControl
{
    private readonly WmsBrowser _browser;
    private readonly LocalStore _log;
    private readonly Stopwatch _workClock=new();
    private readonly Action _showBrowser;
    private readonly Action _hideBrowser;
    private readonly Action _manualReadyAcknowledged;
    private readonly TextBox _so = new() { Font=new Font("Segoe UI",23,FontStyle.Bold),
        CharacterCasing=CharacterCasing.Upper,Width=400,Dock=DockStyle.Fill };
    private readonly RichTextBox _steps = new() { ReadOnly=true, Dock=DockStyle.Fill,
        BackColor=Color.White,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11.5f) };
    private readonly Button _browserToggle = new() { Text="HIỆN TRÌNH DUYỆT",Height=48,
        Dock=DockStyle.Fill,BackColor=Color.FromArgb(30,102,175),ForeColor=Color.White };
    private readonly Button _manual = new() { Text="ĐÃ KIỂM TRA WMS · MỞ KHÓA",Height=48,
        Dock=DockStyle.Fill,Visible=false };
    private readonly Button _supraReady = new() { Text="SUPRA SẴN SÀNG",Height=48,
        Dock=DockStyle.Fill,BackColor=Color.FromArgb(26,120,77),
        ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold) };
    private readonly Button _stopPrint = new() { Text="DỪNG IN TEM",Height=46,
        Dock=DockStyle.Fill,BackColor=Color.FromArgb(177,49,49),ForeColor=Color.White,
        Font=new Font("Segoe UI",11,FontStyle.Bold) };
    private readonly Button _exportCorrections = new() { Text="XUẤT BÁO CÁO SỬA KIỆN",
        Height=48,Dock=DockStyle.Fill,BackColor=Color.FromArgb(32,95,153),
        ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold) };
    private readonly Button _correctionDone = new() { Text="ĐÃ SỬA XONG · KIỂM TRA LẠI",Height=46,
        Dock=DockStyle.Fill,Visible=false,BackColor=Color.FromArgb(23,132,75),
        ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold) };
    private CancellationTokenSource? _printCancel;
    private TaskCompletionSource<bool>? _correctionGate;
    private readonly Label _state = new() { Text="SẴN SÀNG · Nhập mã SO và nhấn Enter",
        Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),
        TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
    private static readonly Regex SoFormat = new("^[A-Z0-9_-]{5,50}$",RegexOptions.Compiled);
    private bool _busy;
    private bool _printerReady;
    private bool _manualPrinterApproval;
    public bool PrinterReady=>_printerReady;
    public bool OperatorReady=>_manualPrinterApproval;
    public void PrinterProgress(string message)
    {
        if(IsDisposed)return;
        _state.Text="THIẾT LẬP MÁY IN · "+message;
        Step(message);
    }
    public void SetPrinterReady(bool ready,string printer="")
    {
        _printerReady=ready;
        _manualPrinterApproval=false;
        if(IsDisposed)return;
        _supraReady.Visible=!ready&&!_busy&&!_uncertain;
        _so.Enabled=ready&&!_busy&&!_uncertain;
        if(ready)
        {
            _state.Text="SẴN SÀNG · Máy in "+printer+" · Nhập SO và nhấn Enter";
            Step("Máy in WMS đã xác minh: "+printer+". Sẵn sàng nhận SO.");
            FocusSo();
        }
        else
        {
            _state.Text="CHỜ THIẾT LẬP MÁY IN · Đăng nhập và chọn máy in WMS";
            Step("Chưa xác minh máy in. Ô SO tạm khóa.");
        }
    }
    private sealed record PendingCorrection(string Before,int After,string Reason);
    private int _browserToggleGeneration;
    private bool _uncertain;
    public bool IsBusy=>_busy;
    public bool NeedsManualReview=>_uncertain;
    public bool BrowserIsShown {get;set;}

    public TemChanPanel(WmsBrowser browser,Action showBrowser,Action hideBrowser,
        LocalStore log,Action manualReadyAcknowledged)
    {
        _browser=browser;_showBrowser=showBrowser;_hideBrowser=hideBrowser;
        _log=log;_manualReadyAcknowledged=manualReadyAcknowledged;
        Dock=DockStyle.Fill;BackColor=Color.FromArgb(247,249,252);
        var shell = new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24,20,24,20),
            ColumnCount=1,RowCount=5 };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,156));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        shell.Controls.Add(new Label { Dock=DockStyle.Fill,
            Text="IN TEM CHẴN | Nhập SO để mở trực tiếp đơn hàng",
            ForeColor=Color.FromArgb(26,43,69),TextAlign=ContentAlignment.MiddleLeft,
            Font=new Font("Segoe UI",18,FontStyle.Bold) },0,0);
        var row = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,75));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
        row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        row.Controls.Add(new Label { Text="MÃ SO",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",12,FontStyle.Bold) },0,0);
        _so.Margin=new Padding(0,0,12,5);
        row.Controls.Add(_so,0,1);
        var hint=new Label { Text="Nhấn Enter để in nhãn",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",10),TextAlign=ContentAlignment.MiddleLeft };
        row.Controls.Add(hint,1,1);
        shell.Controls.Add(row,0,1);
        shell.Controls.Add(_state,0,2);
        var commands=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=3 };
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        commands.RowStyles.Add(new RowStyle(SizeType.Percent,33.3f));
        commands.RowStyles.Add(new RowStyle(SizeType.Percent,33.3f));
        commands.RowStyles.Add(new RowStyle(SizeType.Percent,33.4f));
        commands.Controls.Add(_browserToggle,0,0);
        commands.Controls.Add(_supraReady,1,0);
        commands.Controls.Add(_manual,2,0);
        commands.Controls.Add(_stopPrint,0,1);
        commands.Controls.Add(_correctionDone,1,1);
        commands.SetColumnSpan(_correctionDone,2);
        commands.Controls.Add(_exportCorrections,0,2);
        commands.SetColumnSpan(_exportCorrections,3);
        shell.Controls.Add(commands,0,3);
        var wrap=new Panel { Dock=DockStyle.Fill,Padding=new Padding(14),BackColor=Color.White };
        wrap.Controls.Add(_steps);
        shell.Controls.Add(wrap,0,4);
        Controls.Add(shell);
        _so.Enabled=false;
        // No BeginInvoke in HandleCreated: native handles may be torn down while
        // the tab is added/removed during startup or an operation switch.
        _so.KeyDown += async (_,e) => {
            if(e.KeyCode==Keys.Enter) { e.SuppressKeyPress=true; await RunAsync(); }
        };
        _browserToggle.Click+=(_,_)=>{
            ++_browserToggleGeneration;
            if(BrowserIsShown)_hideBrowser();else _showBrowser();
        };
        _exportCorrections.Click+=(_,_)=>ExportCorrections();
        _stopPrint.Click+=(_,_)=>RequestStop();
        _correctionDone.Click+=(_,_)=>ConfirmCorrectionFinished();
        _supraReady.Click+=(_,_)=> {
            if(IsDisposed||_busy||_uncertain||_printerReady)return;
            if(!_browser.Ready)
            {
                _log.Trace("EVEN","MANUAL_READY_BROWSER_UNAVAILABLE","BLOCKED");
                MessageBox.Show(this,"Trình duyệt WMS chưa khởi tạo. Hãy mở WMS trước.",
                    "Chưa sẵn sàng",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            if(MessageBox.Show(this,
                "Xác nhận anh đã đăng nhập WMS và tự kiểm tra/thiết lập máy in phù hợp.\n\n"+
                "Bỏ qua bước phần mềm tự chọn máy in để mở ô nhập SO. "+
                "Lệnh in sẽ chỉ được gửi sau khi anh nhập SO và WMS vượt qua "+
                "kiểm tra trạng thái đơn, phạm vi kiện và nút in.\n\n"+
                "Phần mềm không xác nhận máy in vật lý đã hoạt động. Tiếp tục?",
                "SUPRA SẴN SÀNG · Xác nhận thủ công",
                MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)
            {
                _log.Trace("EVEN","MANUAL_READY_DECLINED","BLOCKED");
                return;
            }
            AcceptManualReady();
        };
        _manual.Click+=(_,_)=> {
            if(!_uncertain || _busy)return;
            if(MessageBox.Show(this,
                "Anh đã kiểm tra trực tiếp trên WMS rằng lệnh in trước đó không còn đang xử lý "+
                "và sẽ không tự in lại SO cũ?\n\nChỉ mở khóa để nhập SO mới, không gửi lại lệnh in.",
                "Xác nhận kiểm tra WMS",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)
                return;
            _uncertain=false;_manual.Visible=false;_so.Enabled=_printerReady;
            _supraReady.Visible=!_printerReady;
            _log.Trace("EVEN","MANUAL_REVIEW_ACCEPTED","DONE");
            Step("Đã xác nhận xử lý thủ công. Có thể nhập SO tiếp theo.");
            _state.Text="SẴN SÀNG · Đã kiểm tra WMS";
            FocusSo();
        };
        Step("Ứng dụng kiểm tra trạng thái SO, số thùng nguyên dương và phạm vi in trước khi gửi lệnh.");
    }

    // Called only after the operator confirms the explicit WMS responsibility
    // dialog. Acknowledgment bypasses PRINTER SETUP only, never SO status,
    // carton-range or exactly-once print safeguards.
    private void AcceptManualReady()
    {
        if(_busy||_uncertain||_printerReady||IsDisposed)return;
        _manualReadyAcknowledged(); // cancel any superseded automatic setup first
        _manualPrinterApproval=true;
        _printerReady=true;
        _supraReady.Visible=false;
        _so.Enabled=true;
        _state.Text="SUPRA SẴN SÀNG · Đã xác nhận thủ công · Nhập SO và Enter";
        Step("Đã xác nhận WMS thủ công. Bỏ qua tự chọn máy in; WMS vẫn kiểm tra từng SO.");
        _log.Trace("EVEN","OPERATOR_MANUAL_READY","DONE");
        _hideBrowser();
        FocusSo();
    }

    public void ConfirmCorrectionFinished()
    {
        if(!_busy || _correctionGate is null || _correctionGate.Task.IsCompleted)return;
        _log.Trace("EVEN","MANUAL_CORRECTION_ACKNOWLEDGED","DONE",_workClock.ElapsedMilliseconds);
        _correctionGate.TrySetResult(true);
    }

    private void RequestStop()
    {
        if(!_busy)
        {
            Step("Không có lượt in tem tự động đang chạy.");
            return;
        }
        if(MessageBox.Show(this,
            "Dừng thao tác in tem đang chạy? Log và nghiệp vụ khác vẫn tiếp tục. "+
            "Nếu lệnh in đã được gửi, phải kiểm tra WMS và máy in trước khi xử lý tiếp.",
            "DỪNG IN TEM",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)
            return;
        _log.Trace("EVEN","STOP_REQUESTED","DONE",_workClock.ElapsedMilliseconds);
        _printCancel?.Cancel();
        _correctionGate?.TrySetResult(false);
    }

    private void ExportCorrections()
    {
        if(_busy)
        {
            MessageBox.Show(this,"Hãy chờ lượt sửa/in tem đang chạy kết thúc trước khi xuất báo cáo.",
                "Đang xử lý",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }
        try
        {
            var available=_log.CartonCorrections();
            if(available.Count==0)
            {
                MessageBox.Show(this,
                    "Chưa có lịch sử sửa kiện được xác minh trên phiên bản hỗ trợ ghi nhận.",
                    "Chưa có dữ liệu",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            using var filter=new Form { Text="Lọc báo cáo sửa kiện",Width=480,Height=250,
                StartPosition=FormStartPosition.CenterParent,
                FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,
                MinimizeBox=false };
            var choice=new ComboBox {Left=18,Top=15,Width=420,
                DropDownStyle=ComboBoxStyle.DropDownList};
            choice.Items.AddRange(["Tất cả","Một ngày","Khoảng ngày"]);
            choice.SelectedIndex=0;
            var from=new DateTimePicker {Left=18,Top=60,Width=200,
                Format=DateTimePickerFormat.Short,Value=DateTime.Today};
            var to=new DateTimePicker {Left=235,Top=60,Width=200,
                Format=DateTimePickerFormat.Short,Value=DateTime.Today};
            void Sync(){from.Enabled=choice.SelectedIndex!=0;to.Enabled=choice.SelectedIndex==2;}
            choice.SelectedIndexChanged+=(_,_)=>Sync();
            Sync();
            var accept=new Button {Text="TIẾP TỤC XUẤT EXCEL",Left=18,Top=122,
                Width=300,Height=50,BackColor=Color.FromArgb(30,102,175),
                ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold)};
            accept.Click+=(_,_)=>{
                if(choice.SelectedIndex==2 && to.Value.Date<from.Value.Date)
                {
                    MessageBox.Show(filter,"Ngày kết thúc không được trước ngày bắt đầu.");
                    return;
                }
                filter.DialogResult=DialogResult.OK;
                filter.Close();
            };
            filter.Controls.AddRange([choice,from,to,accept]);
            if(filter.ShowDialog(this)!=DialogResult.OK)return;
            DateTime? fromDate=choice.SelectedIndex==0?null:from.Value.Date;
            DateTime? toDate=choice.SelectedIndex==0?null:
                choice.SelectedIndex==1?from.Value.Date:to.Value.Date;
            using var save=new SaveFileDialog {
                Title="Xuất báo cáo sửa kiện Excel",
                Filter="Excel Workbook (*.xlsx)|*.xlsx",
                FileName="SUPRA_Bao_Cao_Sua_Kien_"+
                    DateTime.Now.ToString("yyyyMMdd_HHmmss")+".xlsx"
            };
            if(save.ShowDialog(this)!=DialogResult.OK)return;
            var rows=_log.FilterCartonCorrections(fromDate,toDate).Count;
            _log.ExportCartonCorrectionsXlsx(save.FileName,fromDate,toDate);
            _log.Trace("EXPORT","CARTON_CORRECTIONS_XLSX","DONE",0,"ROWS="+rows);
            MessageBox.Show(this,"Đã xuất "+rows+" lượt sửa kiện ra Excel."+
                (rows==0?" Không có bản ghi phù hợp bộ lọc đã chọn.":""),
                "Xuất báo cáo sửa kiện",MessageBoxButtons.OK,
                rows>0?MessageBoxIcon.Information:MessageBoxIcon.Warning);
        }
        catch(Exception)
        {
            _log.Trace("EXPORT","CARTON_CORRECTIONS_XLSX","ERROR");
            MessageBox.Show(this,
                "Không xuất được báo cáo sửa kiện. Kiểm tra file đang mở, quyền ghi hoặc dữ liệu lịch sử.",
                "Xuất báo cáo thất bại",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }

    public void FocusSo()
    {
        // Only focus synchronously when the SO input can accept focus.
        // Queued BeginInvoke callbacks outliving a removed/recreated TabPage
        // caused intermittent UI exceptions even after the first v1.8.1 guard.
        if(_busy||_uncertain||IsDisposed||Disposing||!Visible||!IsHandleCreated)return;
        try
        {
            if(_so.IsDisposed||!_so.IsHandleCreated||!_so.CanFocus||!_so.Enabled)return;
            _so.Focus();
            _so.SelectAll();
        }
        catch(ObjectDisposedException)
        {
            // The active tab was disposed as the focus notification arrived.
        }
        catch(InvalidOperationException)
        {
            // Tab is changing; the next activation will focus the SO input.
        }
    }
    public void UpdateBrowserButton()
    {
        _browserToggle.Text=BrowserIsShown?"ẨN TRÌNH DUYỆT":"HIỆN TRÌNH DUYỆT";
    }
    private void Step(string text)
    {
        if(IsDisposed)return;
        _steps.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);
        _steps.ScrollToCaret();
    }
    private static string WaitCode(string label) => label switch
    {
        "Mở trang chi tiết / đọc trạng thái" => "DETAIL_STATUS",
        "Mở THÔNG TIN KIỆN HÀNG" => "PARCEL_TAB",
        "WMS ổn định / nút in kiện hàng" => "PRINT_READY",
        "Mở Tùy chọn in nhãn" => "LABEL_DIALOG",
        "WMS xác nhận đóng hộp thoại" => "LABEL_CLOSED",
        _ => "UNKNOWN"
    };
    private static string SafeUiCounts(TemChanPeek? p) =>
        "PRINT="+(p?.printCount??0)+
        ";DIALOG="+(p?.dialogOpen==true?1:0)+
        ";TAB="+(p?.tabSelected==true?1:0)+
        ";STATUS="+(p?.statusValid==true?1:0);
    private void Alert(string text)
    {
        _state.Text="CẢNH BÁO · "+text;
        Step("CẢNH BÁO: "+text);
        _log.Trace("EVEN","USER_ALERT","BLOCKED",_workClock.ElapsedMilliseconds);
        MessageBox.Show(this,text,"IN TEM CHẴN · Cần xác nhận",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
    private async Task<TemChanPeek> WaitAsync(string so,Func<TemChanPeek,bool> predicate,
        string stage,int seconds,CancellationToken cancellationToken=default)
    {
        var watch=Stopwatch.StartNew();int stable=0;TemChanPeek? last=null;
        var code=WaitCode(stage);
        _log.Trace("EVEN","WAIT_"+code,"WAIT",_workClock.ElapsedMilliseconds);
        while(watch.Elapsed<TimeSpan.FromSeconds(seconds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last=await _browser.TemChanPeekAsync(so);
            if(last?.ok==true && predicate(last))
            {
                if(++stable>=2)
                {
                    _log.Trace("EVEN","WAIT_"+code,"DONE",watch.ElapsedMilliseconds,
                        SafeUiCounts(last));
                    return last;
                }
            }
            else stable=0;
            await Task.Delay(360,cancellationToken);
        }
        _log.Trace("EVEN","WAIT_"+code,"TIMEOUT",watch.ElapsedMilliseconds,
            SafeUiCounts(last));
        throw new TemChanStop(stage+" không đạt yêu cầu trong "+seconds+
            " giây. "+(last?.reason??"WMS_NOT_READY")+
            " (nút in="+(last?.printCount??0)+", tab="+(last?.tabSelected==true?"đã chọn":"chưa chọn")+
            ", nguồn trạng thái="+(last?.statusSource??"UNKNOWN")+").");
    }
    private async Task ExecuteAsync(string action,string so,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.Trace("EVEN","ACTION_"+action,"WAIT",_workClock.ElapsedMilliseconds);
        var result=await _browser.TemChanPerformAsync(action,so);
        _log.Trace("EVEN","ACTION_"+action,result.ok?"DONE":"BLOCKED",
            _workClock.ElapsedMilliseconds);
        cancellationToken.ThrowIfCancellationRequested();
        if(!result.ok)throw new TemChanStop("Không xác nhận được thao tác "+action+
            " ("+result.reason+"). Không tự click lại.");
    }
    private async Task<HandoverPeek> WaitHandoverAsync(string so,int quantity,
        Func<HandoverPeek,bool> predicate,string stage,int timeoutSeconds,CancellationToken ct)
    {
        var clock=Stopwatch.StartNew();
        var stable=0;
        while(clock.Elapsed<TimeSpan.FromSeconds(timeoutSeconds))
        {
            ct.ThrowIfCancellationRequested();
            var peek=await _browser.HandoverPeekAsync(so,quantity);
            ct.ThrowIfCancellationRequested();
            if(peek?.ok==true && predicate(peek))
            {
                if(++stable>=2)
                {
                    _log.Trace("EVEN","HANDOVER_WAIT_"+stage,"DONE",clock.ElapsedMilliseconds);
                    return peek;
                }
            }
            else stable=0;
            await Task.Delay(350,ct);
        }
        _log.Trace("EVEN","HANDOVER_WAIT_"+stage,"TIMEOUT",clock.ElapsedMilliseconds);
        throw new TemChanStop("Không xác minh được bước sửa kiện "+stage+
            " trong WMS. Dừng tự động để tránh sửa nhầm SO.");
    }

    private async Task HandoverStepAsync(string action,string so,int quantity,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result=await _browser.HandoverActionAsync(action,so,quantity);
        ct.ThrowIfCancellationRequested();
        if(!result.ok)
            throw new TemChanStop("Không thực hiện được bước sửa kiện "+action+
                " ("+result.reason+"). Không tự click lại.");
        Step("Đã hoàn tất bước sửa kiện "+action+".");
    }

    private async Task AutoCorrectCartonsAsync(string so,int quantity,CancellationToken ct,
        Action markPossiblySubmitted)
    {
        ct.ThrowIfCancellationRequested();
        if(!_printerReady)throw new TemChanStop("Phiên WMS đã thay đổi; không sửa kiện.");
        if(!await _browser.OpenCorrectionAsync())
            throw new TemChanStop("Không mở được WMS sửa kiện.");
        _showBrowser();
        _state.Text="ĐANG SỬA KIỆN · Chưa in tem";
        Step("Mở trang bàn giao SO; tìm đúng đơn, sửa số lượng và xác nhận.");
        await WaitHandoverAsync(so,quantity,
            x=>x.searchInputCount==1 && x.searchButtonCount==1,"SEARCH_READY",30,ct);
        await HandoverStepAsync("SEARCH_INPUT",so,quantity,ct);
        await WaitHandoverAsync(so,quantity,x=>x.searchValueMatches,"SO_READBACK",8,ct);
        await HandoverStepAsync("SEARCH_CLICK",so,quantity,ct);
        await WaitHandoverAsync(so,quantity,
            x=>x.rowCount==1 && x.rowActionCount==1,"EXACT_SO_ROW",25,ct);
        await HandoverStepAsync("OPEN_UPDATE",so,quantity,ct);
        await WaitHandoverAsync(so,quantity,
            x=>x.updateDialogCount==1 && x.quantityInputCount==1,
            "UPDATE_DIALOG",15,ct);
        await HandoverStepAsync("SET_QUANTITY",so,quantity,ct);
        await WaitHandoverAsync(so,quantity,
            x=>x.quantityMatches && x.updateButtonCount==1,
            "QUANTITY_READBACK",8,ct);
        // Cập Nhật may already change WMS even before the confirmation dialog.
        markPossiblySubmitted();
        await HandoverStepAsync("UPDATE",so,quantity,ct);
        await WaitHandoverAsync(so,quantity,
            x=>x.confirmDialogCount==1 && x.confirmYesCount==1,
            "CONFIRMATION",15,ct);
        ct.ThrowIfCancellationRequested();
        // Clicking Yes changes server state; do not retry after dispatch uncertainty.
        markPossiblySubmitted();
        await HandoverStepAsync("CONFIRM_YES",so,quantity,ct);
        await WaitHandoverAsync(so,quantity,
            x=>x.confirmDialogCount==0 && x.updateDialogCount==0,
            "CONFIRM_CLOSED",20,ct);
        Step("WMS đã đóng xác nhận sửa. Đang tải lại SO để kiểm tra số kiện thực tế.");
        _log.Trace("EVEN","HANDOVER_UPDATE_UI_COMPLETE","DONE",
            _workClock.ElapsedMilliseconds);
    }

    private async Task RunAsync()
    {
        if(_busy || _uncertain)
        {
            _log.Trace("EVEN","SUBMIT_BLOCKED","BLOCKED",0,
                _uncertain?"REVIEW_REQUIRED":"OPERATION_BUSY");
            return;
        }
        if(!_printerReady)
        {
            Alert("Chưa xác nhận SUPRA SẴN SÀNG hoặc máy in WMS. Không thể in tem.");
            return;
        }
        string so=_so.Text.Trim().ToUpperInvariant();
        if(!SoFormat.IsMatch(so))
        {
            _log.Trace("EVEN","SO_VALIDATION","BLOCKED");
            Alert("Mã SO không hợp lệ (5–50 ký tự A-Z, số, _ hoặc -).");
            return;
        }
        if(!_browser.Ready)
        {
            _log.Trace("EVEN","BROWSER_NOT_READY","BLOCKED");
            Alert("Web WMS chưa sẵn sàng. Hãy đăng nhập trước.");
            return;
        }
        _printCancel?.Dispose();
        _printCancel=new CancellationTokenSource();
        var ct=_printCancel.Token;
        _workClock.Restart();
        _log.Trace("EVEN","JOB_STARTED","DONE");
        _busy=true;
        _so.Enabled=false;
        _manual.Visible=false;
        _supraReady.Visible=false;
        _correctionDone.Visible=false;
        bool printIssued=false;
        bool correctionMayHaveSubmitted=false;
        int? expectedAfterCorrection=null;
        PendingCorrection? pendingCorrection=null;
        try
        {
            // One SO can return here after manual correction. Always re-read WMS.
            while(true)
            {
                ct.ThrowIfCancellationRequested();
                if(!_printerReady)
                    throw new TemChanStop("Phiên WMS đã thay đổi. Xác nhận SUPRA SẴN SÀNG trước khi tiếp tục.");
                _browser.FocusPrintTab();
                _state.Text="ĐANG KIỂM TRA SO · Chưa in tem";
                Step("Mở chi tiết SO để đọc trạng thái và Tổng số lượng / Tổng SKU.");
                if(!_browser.NavigateTemChan(so))
                    throw new TemChanStop("Không mở được trang chi tiết SO.");
                var p=await WaitAsync(so,x=>x.statusKnown && x.tabReady && x.skuKnown,
                    "Mở trang chi tiết / đọc trạng thái",30,ct);
                if(!p.statusValid)
                {
                    _log.Trace("EVEN","STATUS_NOT_VALID","BLOCKED",_workClock.ElapsedMilliseconds);
                    throw new TemChanStop("SO chưa có trạng thái 'Đã lưu trữ hàng sau đóng gói'.");
                }
                if(p.skuCount<1)
                    throw new TemChanStop("Không xác minh được Tổng SKU từ WMS.");
                _log.Trace("EVEN",p.skuCount==1?"ONE_SKU":"MULTI_SKU","DONE",
                    _workClock.ElapsedMilliseconds);
                Step(p.skuCount==1?"Đơn 1 SKU: dự kiến 01 kiện; phải kiểm tra phạm vi WMS.":"Đơn nhiều SKU: đọc số thùng kết thúc.");
                await ExecuteAsync("TAB",so,ct);
                await WaitAsync(so,x=>x.tabSelected,"Mở THÔNG TIN KIỆN HÀNG",15,ct);
                p=await WaitAsync(so,x=>x.statusValid && x.tabSelected && x.printCount==1,
                    "WMS ổn định / nút in kiện hàng",25,ct);
                await ExecuteAsync("OPEN_PRINT",so,ct);
                p=await WaitAsync(so,x=>x.dialogOpen,"Mở Tùy chọn in nhãn",15,ct);
                ct.ThrowIfCancellationRequested();

                if(expectedAfterCorrection.HasValue)
                {
                    if(!p.rangeValid || p.total!=expectedAfterCorrection.Value ||
                       p.finish!=expectedAfterCorrection.Value || p.begin!=1)
                        throw new TemChanStop("WMS chưa xác nhận số kiện vừa sửa. "+
                            "Hãy kiểm tra lại trên WMS; không tự in tem.");
                    if(pendingCorrection is null ||
                       pendingCorrection.After!=expectedAfterCorrection.Value)
                        throw new TemChanStop("Thiếu thông tin ghi nhận lịch sử sửa kiện.");
                    var correctionRow=new CartonCorrectionRow(
                        Guid.NewGuid().ToString("N"),so,DateTime.Now,
                        pendingCorrection.Before,pendingCorrection.After,
                        pendingCorrection.Reason);
                    if(!_log.TryAppendCartonCorrection(correctionRow))
                        throw new TemChanStop("WMS đã sửa kiện nhưng không lưu được lịch sử. "+
                            "Kiểm tra dữ liệu máy trước khi xử lý tiếp.");
                    _log.Trace("EVEN","HANDOVER_NEW_QUANTITY_VERIFIED","DONE",
                        _workClock.ElapsedMilliseconds);
                    correctionMayHaveSubmitted=false;
                    expectedAfterCorrection=null;
                    pendingCorrection=null;
                }
                // Never equate SKU count with cartons until the WMS range agrees.
                bool countMatchesOneSku=p.skuCount!=1 || (p.total==1 && p.finish==1);
                bool canPrint=p.rangeValid && p.fullRange && countMatchesOneSku;
                var rawCount=(p.finishRaw??"").Trim();
                if(string.IsNullOrWhiteSpace(rawCount))rawCount="KHÔNG ĐỌC ĐƯỢC";
                string countText=p.skuCount==1 && p.rangeValid && countMatchesOneSku
                    ? "01 KIỆN" : (p.rangeValid?p.finish.ToString("00"):rawCount)+" KIỆN";
                bool decimalValue=rawCount.Contains('.') || rawCount.Contains(',');
                string heading=canPrint?"XÁC NHẬN SỐ KIỆN":
                    decimalValue?"SỐ KIỆN THẬP PHÂN KHÔNG HỢP LỆ":
                    "SỐ KIỆN / PHẠM VI KHÔNG HỢP LỆ";
                string instructions=canPrint
                    ? "Yêu cầu người lấy hàng đọc số kiện thực tế để đối chiếu.\n"+
                      "In phiếu kiểm và rà soát lại đúng số kiện.\n\n"+
                      "Đúng: OK để in tem.    Sai: Sửa lại số kiện."
                    : "Vui lòng in phiếu kiểm và rà soát lại đúng số kiện để sửa kiện.\n"+
                      "Phần mềm không tự làm tròn hoặc in với số kiện không hợp lệ.\n"+
                      (p.skuCount==1?"SO có 1 SKU nhưng số kiện WMS chưa khớp 01.":"")+
                      (p.rangeValid && !p.fullRange?"Phạm vi in không bao phủ toàn bộ số thùng.":"");
                _log.Trace("EVEN","CARTON_CONFIRMATION_SHOWN",canPrint?"DONE":"BLOCKED",
                    _workClock.ElapsedMilliseconds,
                    "ONE_SKU="+(p.skuCount==1?1:0)+";RANGE_OK="+(canPrint?1:0));
                bool needRecheck=false;
                // QUAY LẠI from the correction input returns to THIS carton notification.
                while(true)
                {
                    using var prompt=new CartonConfirmationDialog(
                        heading,countText,instructions,canPrint);
                    prompt.ShowDialog(this);
                    ct.ThrowIfCancellationRequested();
                    if(prompt.Decision==CartonDecision.Exit)
                    {
                        _log.Trace("EVEN","CARTON_OPERATOR_EXIT","DONE",
                            _workClock.ElapsedMilliseconds);
                        _state.Text="ĐÃ THOÁT · Không gửi lệnh in";
                        Step("Người vận hành thoát lượt SO. Không in tem.");
                        _so.Clear();
                        return;
                    }
                    if(prompt.Decision==CartonDecision.Correct)
                    {
                        _log.Trace("EVEN","CARTON_OPERATOR_CORRECT","DONE",
                            _workClock.ElapsedMilliseconds);
                        using var correction=new CartonCorrectionDialog(countText);
                        correction.ShowDialog(this);
                        ct.ThrowIfCancellationRequested();
                        if(correction.Decision==CorrectionDecision.Back)
                        {
                            Step("Quay lại hộp đối chiếu số kiện; chưa sửa WMS.");
                            continue;
                        }
                        if(correction.Decision==CorrectionDecision.Exit)
                        {
                            _state.Text="ĐÃ THOÁT · Không sửa / in tem";
                            _so.Clear();
                            return;
                        }
                        // Preserve original WMS value, including noninteger values.
                        var original=p.finishRaw?.Trim();
                        if(string.IsNullOrWhiteSpace(original))
                            original=p.finish>0?p.finish.ToString():"Không đọc được";
                        pendingCorrection=new PendingCorrection(
                            original,correction.Quantity,correction.Reason);
                        bool browserInitiallyShown=BrowserIsShown;
                        int toggleGeneration=_browserToggleGeneration;
                        await AutoCorrectCartonsAsync(so,correction.Quantity,ct,
                            ()=>correctionMayHaveSubmitted=true);
                        expectedAfterCorrection=correction.Quantity;
                        _log.Trace("EVEN","CARTON_RECHECK_AFTER_CORRECTION","WAIT",
                            _workClock.ElapsedMilliseconds);
                        _browser.FocusPrintTab();
                        // Retain an explicitly shown WMS browser. Never override a
                        // user toggle made while the correction automation was running.
                        if(!browserInitiallyShown &&
                           toggleGeneration==_browserToggleGeneration)
                            _hideBrowser();
                        needRecheck=true;
                        break;
                    }
                    if(!canPrint)
                        throw new TemChanStop("Số kiện/phạm vi WMS không hợp lệ. Không in.");
                    break;
                }
                if(needRecheck)continue;

                // Revalidate all critical values immediately before the one-shot click.
                ct.ThrowIfCancellationRequested();
                var readback=await _browser.TemChanPeekAsync(so);
                ct.ThrowIfCancellationRequested();
                if(!_printerReady)
                    throw new TemChanStop("Phiên đăng nhập WMS đã thay đổi trước lệnh in.");
                if(readback?.ok!=true || !readback.statusValid || !readback.skuKnown ||
                   readback.skuCount!=p.skuCount || !readback.dialogOpen ||
                   !readback.rangeValid || !readback.fullRange ||
                   readback.begin!=p.begin || readback.finish!=p.finish ||
                   readback.total!=p.total)
                    throw new TemChanStop("WMS thay đổi sau khi xác nhận; dừng trước lệnh in.");
                _log.Trace("EVEN","CARTON_OPERATOR_APPROVED","DONE",_workClock.ElapsedMilliseconds);
                _state.Text="ĐANG GỬI LỆNH IN · Không thao tác SO mới";
                // Non-idempotent command: never retry if dispatch status is unknown.
                printIssued=true;
                _log.Trace("EVEN","PRINT_MAY_HAVE_ISSUED","UNCERTAIN",_workClock.ElapsedMilliseconds);
                await ExecuteAsync("PRINT_LABEL",so,ct);
                Step("Đã gửi thao tác In Nhãn; chờ WMS đóng hộp thoại.");
                await WaitAsync(so,x=>!x.dialogOpen,
                    "WMS xác nhận đóng hộp thoại",25,ct);
                _state.Text="HOÀN TẤT GIAO DIỆN · Sẵn sàng nhập SO khác";
                Step("Hoàn tất một vòng giao diện. Chưa xác minh tem ra máy in vật lý.");
                _so.Clear();
                _log.Trace("EVEN","JOB_UI_COMPLETE","DONE",_workClock.ElapsedMilliseconds);
                break;
            }
        }
        catch(OperationCanceledException)
        {
            _log.Trace("EVEN","JOB_STOPPED",printIssued?"UNCERTAIN":"DONE",
                _workClock.ElapsedMilliseconds);
            if(printIssued || correctionMayHaveSubmitted)
            {
                _uncertain=true;
                _manual.Visible=true;
                _showBrowser();
                Alert(printIssued
                    ? "Đã dừng. Lệnh in có thể đã gửi; kiểm tra WMS/máy in, không tự in lại."
                    : "Đã dừng khi cập nhật kiện có thể đã gửi; kiểm tra WMS trước khi xử lý SO mới.");
            }
            else
            {
                _state.Text="ĐÃ DỪNG · Không gửi lệnh in";
                Step("Đã dừng tự động in tem, các nghiệp vụ và log vẫn hoạt động.");
            }
        }
        catch(TemChanStop e)
        {
            _log.TraceException("EVEN","JOB_HALTED",e,_workClock.ElapsedMilliseconds);
            _log.Trace("EVEN","PRINT_ISSUED_FLAG",printIssued?"UNCERTAIN":"BLOCKED",
                _workClock.ElapsedMilliseconds,"VALUE="+(printIssued?1:0));
            if(printIssued || correctionMayHaveSubmitted)
            {
                _uncertain=true;
                _manual.Visible=true;
                _showBrowser();
                Alert(e.Message+(printIssued
                    ? " Lệnh in có thể đã gửi. Kiểm tra WMS/máy in, không in lại."
                    : " Cập nhật số kiện có thể đã gửi. Kiểm tra WMS trước khi xử lý SO mới."));
            }
            else Alert(e.Message);
        }
        catch(Exception ex)
        {
            _log.TraceException("EVEN","JOB_EXCEPTION",ex,_workClock.ElapsedMilliseconds);
            _uncertain=true;
            _manual.Visible=true;
            _showBrowser();
            Alert("Lỗi ngoài dự kiến. Kiểm tra trực tiếp WMS trước khi mở khóa SO.");
        }
        finally
        {
            _log.Trace("EVEN","JOB_FINISHED","DONE",_workClock.ElapsedMilliseconds,
                "UNCERTAIN="+(_uncertain?1:0));
            _workClock.Stop();
            _correctionGate=null;
            _correctionDone.Visible=false;
            _printCancel?.Dispose();
            _printCancel=null;
            _busy=false;
            _so.Enabled=_printerReady&&!_uncertain;
            _supraReady.Visible=!_printerReady&&!_uncertain;
            if(!_uncertain)FocusSo();
        }
    }
}
internal sealed class TemChanStop(string message) : Exception(message);
