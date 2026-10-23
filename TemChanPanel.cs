using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SupraAutoPackLe;

/// <summary>
/// Carton-label operation: SO + Enter only. No personnel, statistics, XLSX or
/// business-history writes. The existing Tote packing workflow is untouched.
/// </summary>
internal sealed class TemChanPanel : UserControl
{
    private readonly WmsBrowser _browser;
    private readonly Action _showBrowser;
    private readonly Action _hideBrowser;
    private readonly TextBox _so = new() { Font=new Font("Segoe UI",23,FontStyle.Bold),
        CharacterCasing=CharacterCasing.Upper,Width=400,Dock=DockStyle.Fill };
    private readonly RichTextBox _steps = new() { ReadOnly=true, Dock=DockStyle.Fill,
        BackColor=Color.White,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11.5f) };
    private readonly Button _browserToggle = new() { Text="HIỆN TRÌNH DUYỆT",Height=48,
        Dock=DockStyle.Fill,BackColor=Color.FromArgb(30,102,175),ForeColor=Color.White };
    private readonly Button _manual = new() { Text="ĐÃ KIỂM TRA WMS · MỞ KHÓA",Height=48,
        Dock=DockStyle.Fill,Visible=false };
    private readonly Label _state = new() { Text="SẴN SÀNG · Nhập mã SO và nhấn Enter",
        Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),
        TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
    private static readonly Regex SoFormat = new("^[A-Z0-9_-]{5,50}$",RegexOptions.Compiled);
    private bool _busy;
    private bool _printerReady;
    public bool PrinterReady=>_printerReady;
    public void PrinterProgress(string message)
    {
        if(IsDisposed)return;
        _state.Text="THIẾT LẬP MÁY IN · "+message;
        Step(message);
    }
    public void SetPrinterReady(bool ready,string printer="")
    {
        _printerReady=ready;
        if(IsDisposed)return;
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
    private bool _uncertain;
    public bool IsBusy=>_busy;
    public bool NeedsManualReview=>_uncertain;
    public bool BrowserIsShown {get;set;}

    public TemChanPanel(WmsBrowser browser,Action showBrowser,Action hideBrowser)
    {
        _browser=browser;_showBrowser=showBrowser;_hideBrowser=hideBrowser;
        Dock=DockStyle.Fill;BackColor=Color.FromArgb(247,249,252);
        var shell = new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24,20,24,20),
            ColumnCount=1,RowCount=5 };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
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
        var commands=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1 };
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        commands.Controls.Add(_browserToggle,0,0);
        commands.Controls.Add(_manual,1,0);
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
        _browserToggle.Click+=(_,_)=>{if(BrowserIsShown)_hideBrowser();else _showBrowser();};
        _manual.Click+=(_,_)=> {
            if(!_uncertain || _busy)return;
            if(MessageBox.Show(this,
                "Anh đã kiểm tra trực tiếp trên WMS rằng lệnh in trước đó không còn đang xử lý "+
                "và sẽ không tự in lại SO cũ?\n\nChỉ mở khóa để nhập SO mới, không gửi lại lệnh in.",
                "Xác nhận kiểm tra WMS",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)
                return;
            _uncertain=false;_manual.Visible=false;_so.Enabled=true;
            Step("Đã xác nhận xử lý thủ công. Có thể nhập SO tiếp theo.");
            _state.Text="SẴN SÀNG · Đã kiểm tra WMS";
            FocusSo();
        };
        Step("Ứng dụng kiểm tra trạng thái SO, số thùng nguyên dương và phạm vi in trước khi gửi lệnh.");
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
    private void Alert(string text)
    {
        _state.Text="CẢNH BÁO · "+text;
        Step("CẢNH BÁO: "+text);
        MessageBox.Show(this,text,"IN TEM CHẴN · Cần xác nhận",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
    private async Task<TemChanPeek> WaitAsync(string so,Func<TemChanPeek,bool> predicate,
        string stage,int seconds)
    {
        var watch=Stopwatch.StartNew();int stable=0;TemChanPeek? last=null;
        while(watch.Elapsed<TimeSpan.FromSeconds(seconds))
        {
            last=await _browser.TemChanPeekAsync(so);
            if(last?.ok==true && predicate(last))
            {
                if(++stable>=2)return last;
            }
            else stable=0;
            await Task.Delay(360);
        }
        throw new TemChanStop(stage+" không đạt yêu cầu trong "+seconds+
            " giây. "+(last?.reason??"WMS_NOT_READY")+
            " (nút in="+(last?.printCount??0)+", tab="+(last?.tabSelected==true?"đã chọn":"chưa chọn")+
            ", nguồn trạng thái="+(last?.statusSource??"UNKNOWN")+").");
    }
    private async Task ExecuteAsync(string action,string so)
    {
        var result=await _browser.TemChanPerformAsync(action,so);
        if(!result.ok)throw new TemChanStop("Không xác nhận được thao tác "+action+
            " ("+result.reason+"). Không tự click lại.");
    }
    private async Task RunAsync()
    {
        if(_busy || _uncertain)return;
        if(!_printerReady)
        {
            Alert("Chưa chọn và xác nhận máy in WMS. Không thể in tem.");
            return;
        }
        string so=_so.Text.Trim().ToUpperInvariant();
        if(!SoFormat.IsMatch(so)) {Alert("Mã SO không hợp lệ (5–50 ký tự A-Z, số, _ hoặc -).");return;}
        if(!_browser.Ready) {Alert("Web WMS chưa sẵn sàng. Hãy đăng nhập trước.");return;}
        _busy=true;_so.Enabled=false;_manual.Visible=false;
        bool printIssued=false;
        try
        {
            _state.Text="ĐANG XỬ LÝ · "+so;
            Step("Bắt đầu lượt in. Truy cập chi tiết SO.");
            if(!_browser.NavigateTemChan(so))
                throw new TemChanStop("Không mở được trang chi tiết SO.");
            var p=await WaitAsync(so,x=>x.statusKnown && x.tabReady,
                "Mở trang chi tiết / đọc trạng thái",30);
            if(!p.statusValid)
                throw new TemChanStop("SO chưa sẵn sàng in tem. Yêu cầu trạng thái: "+
                    "'Đã lưu trữ hàng sau đóng gói'.");
            Step("Trạng thái WMS hợp lệ: Đã lưu trữ hàng sau đóng gói.");
            await ExecuteAsync("TAB",so);
            await WaitAsync(so,x=>x.tabSelected,"Mở THÔNG TIN KIỆN HÀNG",15);
            Step("Đã chọn tab THÔNG TIN KIỆN HÀNG.");
            p=await WaitAsync(so,x=>x.statusValid && x.tabSelected &&
                x.printCount==1,"WMS ổn định / nút in kiện hàng",25);
            Step("WMS đã xác nhận trạng thái, tab kiện hàng và đúng một nút in.");
            await ExecuteAsync("OPEN_PRINT",so);
            p=await WaitAsync(so,x=>x.dialogOpen,"Mở Tùy chọn in nhãn",15);
            if(!p.rangeValid)throw new TemChanStop("Số thùng không hợp lệ: "+p.dialogReason+
                ". Số thùng bắt đầu/kết thúc và tổng số thùng phải là số nguyên dương, "+
                "không vượt phạm vi.");
            Step("Phạm vi hợp lệ: "+p.begin+"–"+p.finish+" / "+p.total+" thùng.");
            _state.Text="ĐANG GỬI LỆNH IN · Không thao tác SO mới";
            // This command is non-idempotent. If response is lost, don't attempt again.
            printIssued=true;
            await ExecuteAsync("PRINT_LABEL",so);
            Step("Đã gửi thao tác In Nhãn; chờ WMS đóng hộp thoại.");
            await WaitAsync(so,x=>!x.dialogOpen,"WMS xác nhận đóng hộp thoại",25);
            _state.Text="HOÀN TẤT GIAO DIỆN · Sẵn sàng nhập SO khác";
            Step("Hoàn tất một vòng giao diện. Chưa xác minh tem ra máy in vật lý.");
            _so.Clear();
        }
        catch(TemChanStop e)
        {
            // Explicit operator acknowledgement, no auto retry.
            if(printIssued)
            {
                _uncertain=true;
                _manual.Visible=true;
                _showBrowser();
                Alert(e.Message+" Lệnh in có thể đã gửi: kiểm tra WMS/máy in, KHÔNG tự in lại.");
            }
            else Alert(e.Message);
        }
        catch(Exception)
        {
            _uncertain=true;
            _manual.Visible=true;
            _showBrowser();
            Alert("Lỗi ngoài dự kiến. Kiểm tra trực tiếp WMS trước khi mở khóa SO.");
        }
        finally
        {
            _busy=false;
            _so.Enabled=!_uncertain;
            if(!_uncertain)FocusSo();
        }
    }
}
internal sealed class TemChanStop(string message) : Exception(message);
