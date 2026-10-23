using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SupraAutoPackLe;

public sealed class MainForm : Form
{
    private readonly LocalStore _store=new();
    private readonly WmsBrowser _browser;
    private readonly Stopwatch _watch=new();
    private readonly TextBox _tote=new(){Width=320,Font=new Font("Segoe UI",19,FontStyle.Bold)};
    private readonly TextBox _qty=new(){Width=160,Font=new Font("Segoe UI",19,FontStyle.Bold)};
    private readonly Button _ok=MakeButton("CHỌN NHÂN SỰ",205);
    private readonly Button _show=MakeButton("ẨN TRÌNH DUYỆT",170);
    private readonly Button _traceStart=MakeButton("BẮT ĐẦU GHI",140);
    private readonly Button _traceStop=MakeButton("KẾT THÚC & XUẤT LOG",200);
    private readonly Label _traceStatus=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
        Font=new Font("Segoe UI",9,FontStyle.Bold),AutoEllipsis=true};
    private readonly Button _stop=MakeButton("DỪNG / XỬ LÝ TAY",195);
    private readonly Button _recover=MakeButton("XÁC NHẬN WMS SẴN SÀNG",275);
    private readonly Button _peopleButton=MakeButton("QUẢN LÝ NHÂN SỰ",190);
    private readonly Button _exportXlsx=MakeButton("XUẤT LỊCH SỬ XLSX",190);
    private readonly Button _exportLog=MakeButton("XUẤT LOG",135);
    private readonly TabControl _tabs=new(){Dock=DockStyle.Fill,Font=new Font("Segoe UI",10)};
    private readonly TabPage _operationTab=new("NGHIỆP VỤ");
    private readonly TabPage _temChanTab=new("IN TEM CHẴN");
    private readonly TabPage _settingsTab=new("CÀI ĐẶT");
    private TemChanPanel? _temChanPanel;
    private bool _temChanMode;
    private bool _switchingOperation;
    private readonly TabPage _webTab=new("TRÌNH DUYỆT WMS");
    private readonly TabPage _productionTab=new("SẢN LƯỢNG HÔM NAY");
    private readonly TabPage _aboutTab=new("THÔNG TIN PHẦN MỀM");
    private readonly Label _productionOverview=new(){Dock=DockStyle.Fill,Font=new Font("Segoe UI",14,FontStyle.Bold),Padding=new Padding(10,6,6,6),ForeColor=Color.FromArgb(26,43,69),TextAlign=ContentAlignment.MiddleLeft};
    private readonly Label[] _metricCards=Enumerable.Range(0,8).Select(_=>new Label
    {
        Dock=DockStyle.Fill,Font=new Font("Segoe UI",11.5f,FontStyle.Bold),
        Padding=new Padding(14,8,8,8),TextAlign=ContentAlignment.MiddleLeft,
        BackColor=Color.White,ForeColor=Color.FromArgb(26,43,69),
        Margin=new Padding(5)
    }).ToArray();
    private readonly DataGridView _productionGrid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,BackgroundColor=Color.White,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,ScrollBars=ScrollBars.Both};
    private readonly CheckBox _startWithWindows=new(){AutoSize=true,Text="Tự động chạy khi đăng nhập Windows"};
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
    private bool _changingStartupSetting;
    private bool _setupDone;
    private bool _setupRunning;
    private Task? _setupTask;
    private bool _printerVerified;
    private bool _resettingBrowser;
    private readonly System.Windows.Forms.Timer _saveWidthsTimer=new(){Interval=650};
    private string _stage="INIT";
    private static readonly Regex ToteFormat=new("^[A-Za-z0-9_-]{2,64}$",RegexOptions.Compiled);
    private static readonly Regex CodeFormat=new("^[A-Za-z0-9_-]{5,50}$",RegexOptions.Compiled);

    public MainForm()
    {
        _people=_store.LoadPeople();
        _browser=new WmsBrowser(_store,ShowBrowser);
        _browser.LoginRequired+=()=>
        {
            if(_temChanMode){_browser.Reveal();return;}
            if(!_setupDone||IsDisposed||_resettingBrowser)return;
            _setupDone=false;
            _printerVerified=false;
            _browser.BeginStartupRouting();
            if(_busy)
            {
                _needsRecovery=true;
                Status("FAULT","Phiên WMS chuyển sang đăng nhập khi đang xử lý. Phải kiểm tra WMS thủ công.");
            }
            else
            {
                Status("INIT","WMS yêu cầu đăng nhập lại. Chờ thiết lập quầy và máy in.");
                _setupTask=RunInitialSetupAsync();
            }
            _browser.Reveal();
            UpdateControls();
        };
        Text="WMS Packing & Productivity Assistant | "+Application.ProductVersion;
        Font=new Font("Segoe UI",10);
        MinimumSize=new Size(1040,675);Width=1380;Height=850;
        StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(247,249,252);
        Compose();
        RestoreColumnWidths();
        _history.ColumnWidthChanged+=(_,_)=>QueueColumnWidthSave();
        _productionGrid.ColumnWidthChanged+=(_,_)=>QueueColumnWidthSave();
        _saveWidthsTimer.Tick+=(_,_)=>{_saveWidthsTimer.Stop();SaveColumnWidths();};
        RefreshHistory();
        _midnightCheck.Tick+=(_,_)=>{if(_displayDate!=DateTime.Today)RefreshHistory();};
        _midnightCheck.Start();
        Activated+=(_,_)=>{if(_tabs.SelectedTab==_operationTab&&!_busy&&!_needsRecovery&&_tote.Enabled)_tote.Focus();};
        Status("INIT","Mở cửa sổ WMS, đăng nhập và chọn máy in trước khi dùng.");
        Shown+=async (_,_)=>{
            using(var choice=BuildStartupChoice())
            {
                choice.ShowDialog(this);
                SwitchOperation(choice.Tag is bool selected && selected,true);
            }
            _tabs.SelectedTab=_webTab;
            try
            {
                await _browser.InitializeAsync();
                if(!_temChanMode)
                {
                    Status("INIT","Đăng nhập WMS. Ứng dụng sẽ xác nhận quầy và máy in trước khi cho quét Tote.");
                    _setupTask=RunInitialSetupAsync();
                }
                else _temChanPanel?.UpdateBrowserButton();
            }
            catch
            {
                if(!_temChanMode)Status("FAULT","Không khởi động được WMS. Cần WebView2 Runtime.");
                else MessageBox.Show(this,"Không khởi động được WMS. Cần WebView2 Runtime.",
                    "Lỗi trình duyệt",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        };
        FormClosing+=(_,e)=>{
            if(_temChanPanel?.IsBusy==true)
            {
                e.Cancel=true;
                MessageBox.Show(this,"Đang xử lý lệnh in chẵn. Không được đóng ứng dụng giữa thao tác.",
                    "Đang xử lý",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            if(_browser.Recording)
            {
                e.Cancel=true;
                ShowBrowser();
                MessageBox.Show(this,"Đang ghi thao tác WMS. Hãy bấm KẾT THÚC & XUẤT LOG trước khi đóng phần mềm.",
                    "Chưa xuất log thao tác",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            _midnightCheck.Stop();
            _midnightCheck.Dispose();
            _saveWidthsTimer.Stop();
            SaveColumnWidths();
            _saveWidthsTimer.Dispose();
            _cancel?.Cancel();
            _setupRunning=false;
            _store.Trace("APP","CLOSING",_busy?"UNCERTAIN":"OK",_watch.ElapsedMilliseconds);
            _browser.Terminate();
        };
    }
    private Form BuildStartupChoice()
    {
        var choice=new Form {
            Text="CHỌN NGHIỆP VỤ",StartPosition=FormStartPosition.CenterParent,
            Width=530,Height=255,MinimumSize=new Size(500,240),MaximizeBox=false,
            MinimizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog,
            Font=new Font("Segoe UI",11),BackColor=Color.FromArgb(247,249,252)
        };
        var layout=new TableLayoutPanel {
            Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2,RowCount=2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.Controls.Add(new Label {
            Text="Chọn nghiệp vụ cần thực hiện",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",14,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleCenter
        },0,0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0,0)!,2);
        var even=MakeButton("IN TEM CHẴN",210);
        var loose=MakeButton("IN TEM LẺ AUTO",210);
        even.Dock=DockStyle.Fill;loose.Dock=DockStyle.Fill;
        even.Margin=new Padding(4);loose.Margin=new Padding(4);
        even.Click+=(_,_)=>{choice.Tag=true;choice.DialogResult=DialogResult.OK;choice.Close();};
        loose.Click+=(_,_)=>{choice.Tag=false;choice.DialogResult=DialogResult.OK;choice.Close();};
        layout.Controls.Add(even,0,1);
        layout.Controls.Add(loose,1,1);
        choice.Controls.Add(layout);
        return choice;
    }

    private Control BuildOperationSettings()
    {
        var root=new TableLayoutPanel {
            Dock=DockStyle.Fill,Padding=new Padding(26),ColumnCount=1,RowCount=3,
            BackColor=Color.FromArgb(247,249,252)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,75));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.Controls.Add(new Label {
            Text="CÀI ĐẶT · CHỌN NGHIỆP VỤ",Dock=DockStyle.Fill,
            Font=new Font("Segoe UI",17,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleLeft
        },0,0);
        var buttons=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var ch=MakeButton("CHUYỂN SANG IN TEM CHẴN",250);
        var le=MakeButton("CHUYỂN SANG IN TEM LẺ AUTO",250);
        ch.Dock=DockStyle.Fill;le.Dock=DockStyle.Fill;
        ch.Margin=new Padding(4);le.Margin=new Padding(4);
        ch.Click+=(_,_)=>SwitchOperation(true);
        le.Click+=(_,_)=>SwitchOperation(false);
        buttons.Controls.Add(ch,0,0);buttons.Controls.Add(le,1,0);
        root.Controls.Add(buttons,0,1);
        root.Controls.Add(new Label {
            Text="Chuyển nghiệp vụ không cần khởi động lại. Không chuyển khi đang in, "+
                "đang đóng gói hoặc khi WMS chưa được kiểm tra sau một thao tác không chắc chắn. "+
                "Nghiệp vụ in tem lẻ giữ nguyên lịch sử và thống kê; in tem chẵn không lưu lịch sử.",
            Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),AutoSize=false,
            TextAlign=ContentAlignment.TopLeft,Padding=new Padding(4,24,4,4)
        },0,2);
        return root;
    }

    private void SwitchOperation(bool temChan,bool initial=false)
    {
        if(!initial && (_busy || _setupRunning || _temChanPanel?.IsBusy==true ||
            _temChanPanel?.NeedsManualReview==true || _browser.Recording ||
            _resettingBrowser || _needsRecovery))
        {
            MessageBox.Show(this,"Không thể chuyển nghiệp vụ khi có thao tác chưa kết thúc "+
                "hoặc cần kiểm tra lại WMS.", "Chưa thể chuyển",MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        if(!initial && temChan==_temChanMode)
        {
            _tabs.SelectedTab=temChan?_temChanTab:_operationTab;
            return;
        }
        _setupRunning=false;
        _switchingOperation=true;
        _tabs.SuspendLayout();
        try
        {
            // Keep a stable visible tab selected while modifying TabPages. Removing
            // the selected tab used to fire focus callbacks on controls without HWNDs.
            _tabs.SelectedTab=_settingsTab;
            _temChanMode=temChan;
            _browser.SetOperationMode(temChan);
            if(temChan)
            {
                _tabs.TabPages.Remove(_operationTab);
                _tabs.TabPages.Remove(_productionTab);
                if(!_tabs.TabPages.Contains(_temChanTab))
                    _tabs.TabPages.Insert(0,_temChanTab);
                _tabs.SelectedTab=_temChanTab;
            }
            else
            {
                _tabs.TabPages.Remove(_temChanTab);
                if(!_tabs.TabPages.Contains(_operationTab))
                    _tabs.TabPages.Insert(0,_operationTab);
                if(!_tabs.TabPages.Contains(_productionTab))
                    _tabs.TabPages.Add(_productionTab);
                _setupDone=false;_printerVerified=false;_needsRecovery=false;
                Status("INIT","Mở lại nghiệp vụ in tem lẻ, xác nhận quầy và máy in.");
                _tabs.SelectedTab=_operationTab;
            }
        }
        finally
        {
            _tabs.ResumeLayout(true);
            _switchingOperation=false;
        }
        if(!temChan && _browser.Ready)_setupTask=RunInitialSetupAsync();
        Text="WMS Packing & Productivity Assistant | "+
            (temChan?"IN TEM CHẴN":"IN TEM LẺ AUTO")+" | "+Application.ProductVersion;
        UpdateControls();
        UpdateBrowserToggle();
        if(!initial && _browser.Ready)ShowBrowser();
    }

    private static Button MakeButton(string label,int width)
    {
        return new Button{Text=label,Width=width,Height=44,Font=new Font("Segoe UI",11,FontStyle.Bold),
          FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(30,102,175),
          ForeColor=Color.White,UseVisualStyleBackColor=false};
    }
    private void Compose()
    {
        _tabs.DrawMode=TabDrawMode.OwnerDrawFixed;
        _tabs.SizeMode=TabSizeMode.Fixed;
        _tabs.ItemSize=new Size(190,50);
        _tabs.DrawItem+=(_,e)=>
        {
            var selected=e.Index==_tabs.SelectedIndex;
            var area=e.Bounds;
            using var bg=new SolidBrush(selected?_navy:Color.FromArgb(231,237,245));
            e.Graphics.FillRectangle(bg,area);
            // A concise display caption prevents WinForms from wrapping/clipping on
            // narrow screens and non-100% Windows scaling.
            var page=_tabs.TabPages[e.Index];
            var caption=page==_productionTab?"SẢN LƯỢNG":
                page==_aboutTab?"THÔNG TIN":page.Text;
            using var font=new Font("Segoe UI",selected?11f:10f,FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics,caption,font,area,
                selected?Color.White:Color.FromArgb(30,45,68),
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|
                TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        };
        var title=new Panel{Dock=DockStyle.Top,Height=62,BackColor=_navy};
        title.Controls.Add(new Label{Text="WMS  |  HỖ TRỢ ĐÓNG GÓI VÀ GHI NHẬN NĂNG SUẤT",UseMnemonic=false,Dock=DockStyle.Fill,
            Padding=new Padding(18,0,0,0),TextAlign=ContentAlignment.MiddleLeft,
            ForeColor=Color.White,Font=new Font("Segoe UI",17,FontStyle.Bold)});
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(16,12,16,12)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,116));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var inputs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,8)};
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));
        var t=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,10,0)};
        t.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        t.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        t.Controls.Add(new Label{Text="QUÉT MÃ TOTE",Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft},0,0);
        _tote.Dock=DockStyle.Fill;_tote.Margin=new Padding(0,0,0,6);t.Controls.Add(_tote,0,1);
        var q=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,10,0)};
        q.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        q.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        q.Controls.Add(new Label{Text="SỐ KIỆN",Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft},0,0);
        _qty.Dock=DockStyle.Fill;_qty.Margin=new Padding(0,0,0,6);q.Controls.Add(_qty,0,1);
        _ok.Dock=DockStyle.Fill;_ok.Margin=new Padding(6,26,0,6);
        _ok.Font=new Font("Segoe UI",12,FontStyle.Bold);
        _ok.BackColor=Color.FromArgb(25,122,78);
        inputs.Controls.Add(t,0,0);inputs.Controls.Add(q,1,0);inputs.Controls.Add(_ok,2,0);
        root.Controls.Add(inputs,0,0);
        // Four proportional columns replace the clipping-prone fixed-width flow row.
        // All actions remain visible at the application's supported minimum width.
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,
            Margin=new Padding(0),Padding=new Padding(0,5,0,5)};
        foreach(var width in new float[]{20,23,23,34})
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var actionButtons=new[]{_peopleButton,_show,_stop,_recover};
        for(var i=0;i<actionButtons.Length;i++)
        {
            actionButtons[i].Dock=DockStyle.Fill;
            actionButtons[i].Margin=new Padding(4,3,4,3);
            actionButtons[i].AutoSize=false;
            actions.Controls.Add(actionButtons[i],i,0);
        }
        root.Controls.Add(actions,0,1);
        _status.Dock=DockStyle.Fill;_status.Height=46;_status.AutoSize=false;
        _status.Padding=new Padding(12,0,8,0);_status.TextAlign=ContentAlignment.MiddleLeft;
        _status.BackColor=Color.White;_status.Margin=new Padding(0,2,0,7);
        root.Controls.Add(_status,0,2);
        var historyToolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};
        historyToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        historyToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,365));
        historyToolbar.Controls.Add(_historyHeading,0,0);
        var exports=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,
            Margin=new Padding(0)};
        exports.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
        exports.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
        exports.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        _exportXlsx.Text="XUẤT EXCEL";
        _exportXlsx.Dock=DockStyle.Fill;_exportLog.Dock=DockStyle.Fill;
        _exportXlsx.Margin=new Padding(4);_exportLog.Margin=new Padding(4);
        exports.Controls.Add(_exportXlsx,0,0);
        exports.Controls.Add(_exportLog,1,0);
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
        _operationTab.Controls.Add(root);
        _operationTab.Controls.Add(title);
                // This control is visible while the browser is selected, unlike the
        // Nghiệp vụ action button. The 44 px bar is outside WebView2, not an overlay.
        var browserLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,
            ColumnCount=1,Margin=Padding.Empty,Padding=Padding.Empty};
        browserLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        browserLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var browserHeader=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,
            BackColor=Color.FromArgb(231,237,245),Padding=new Padding(5)};
        browserHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        browserHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,147));
        browserHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
        browserHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
        _traceStatus.Text="WMS | Ghi lại thao tác để phân tích";
        _traceStatus.ForeColor=_navy;
        browserHeader.Controls.Add(_traceStatus,0,0);
        _traceStart.Dock=DockStyle.Fill;_traceStart.Margin=new Padding(2);
        _traceStop.Dock=DockStyle.Fill;_traceStop.Margin=new Padding(2);
        _traceStop.Enabled=false;
        _traceStart.BackColor=Color.FromArgb(21,119,79);
        _traceStop.BackColor=Color.FromArgb(177,66,55);
        _traceStart.Font=new Font("Segoe UI",9,FontStyle.Bold);
        _traceStop.Font=new Font("Segoe UI",9,FontStyle.Bold);
        _traceStart.Click+=async (_,_)=>await StartTraceAsync();
        _traceStop.Click+=async (_,_)=>await StopTraceAsync();
        browserHeader.Controls.Add(_traceStart,1,0);
        browserHeader.Controls.Add(_traceStop,2,0);
        var hideFromWeb=MakeButton("ẨN TRÌNH DUYỆT",198);
        hideFromWeb.Dock=DockStyle.Fill;hideFromWeb.Margin=new Padding(2);
        hideFromWeb.Font=new Font("Segoe UI",9,FontStyle.Bold);
        hideFromWeb.Click+=(_,_)=>HideBrowser();
        browserHeader.Controls.Add(hideFromWeb,3,0);
        browserLayout.Controls.Add(browserHeader,0,0);
        browserLayout.Controls.Add(_browser,0,1);
        _webTab.Controls.Add(browserLayout);
        _productionTab.Controls.Add(BuildProductionTab());
        _aboutTab.Controls.Add(BuildAboutTab());
        _temChanPanel=new TemChanPanel(_browser,ShowBrowser,HideBrowser);
        _temChanTab.Controls.Add(_temChanPanel);
        _settingsTab.Controls.Add(BuildOperationSettings());
        _tabs.TabPages.AddRange([_operationTab,_webTab,_productionTab,_aboutTab,_settingsTab]);
        _tabs.SelectedTab=_webTab; // Visible on the very first frame, before async login begins.
        _tabs.SelectedIndexChanged+=(_,_)=>{
            if(_switchingOperation)return;
            UpdateBrowserToggle();
            if(_tabs.SelectedTab==_productionTab)
                RefreshProduction(_store.FilterHistory(DateTime.Today,DateTime.Today));
            if(_tabs.SelectedTab==_operationTab && !_busy && !_needsRecovery)
                _tote.Focus();
            if(_tabs.SelectedTab==_temChanTab)
                _temChanPanel?.FocusSo();
            _tabs.Invalidate();
        };
        Controls.Add(_tabs);
        var columns=new[]{"Ngày","Bắt đầu","Kết thúc","Thời gian xử lí","Nhân sự","Mã SO","Mã DO","Số SKU","Số kiện","Pcs","Kết quả","Điểm"};
        foreach(var item in columns)_history.Columns.Add(item,item);
        var widths=new[]{108,95,95,185,160,145,145,100,100,95,238,98};
        _history.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
        _history.ScrollBars=ScrollBars.Both;
        for(var i=0;i<widths.Length;i++)_history.Columns[i].Width=widths[i];
        _tote.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_qty.Focus();_qty.SelectAll();}};
        _qty.KeyDown+=async (_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await OnSubmitAsync();}};
        _ok.Click+=async (_,_)=>await OnSubmitAsync();
        _peopleButton.Click+=(_,_)=>ManagePeople();
        _show.Click+=(_,_)=>{
            if(_tabs.TabPages.Contains(_webTab)) HideBrowser();
            else ShowBrowser();
        };
        _stop.Click+=(_,_)=>{
            if(!_busy){_browser.Reveal();return;}
            if(MessageBox.Show("Dừng xử lý tự động? Nếu lệnh in đã gửi, KHÔNG được tự in lại.",
                "Xác nhận",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes)
            {_cancel?.Cancel();_browser.Reveal();}
        };
        _recover.Click+=async (_,_)=>{
            var p=await _browser.PeekAsync();
            if(p?.ready==true)
            {
                if(!_printerVerified &&
                    MessageBox.Show(this,
                    "Đã kiểm tra trực tiếp quầy SDG.22 và máy in 21-1129-D29 (Online) trên WMS? "+
                    "Chỉ xác nhận nếu đã kiểm tra bằng tay.",
                    "Xác nhận thiết lập WMS",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                _setupDone=true;_needsRecovery=false;_setupRunning=false;
                _browser.StopStartupRouting();
                Status("READY","Đã xác nhận màn hình quét Tote và máy in sẵn sàng.");
                UpdateControls();
                HideBrowser();
                FocusToteIfIdle();
            }
            else{_browser.Reveal();MessageBox.Show("WMS chưa trở về màn hình quét Tote. Hãy hoàn tất bằng tay.");}
        };
        _exportXlsx.Click+=(_,_)=>ExportXlsx();
        _exportLog.Click+=(_,_)=>ExportLogs();
        UpdateControls();
    }

    private Control BuildProductionTab()
    {
        var root=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,
            Padding=new Padding(12,6,12,10),BackColor=Color.FromArgb(247,249,252)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,174));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,98));
        var metrics=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,
            Margin=new Padding(0),Padding=new Padding(0)
        };
        for(var i=0;i<4;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        for(var i=0;i<2;i++)metrics.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        for(var i=0;i<_metricCards.Length;i++)
            metrics.Controls.Add(_metricCards[i],i%4,i/4);
        _productionGrid.EnableHeadersVisualStyles=false;
        _productionGrid.ColumnHeadersHeight=48;
        _productionGrid.ColumnHeadersDefaultCellStyle.BackColor=_navy;
        _productionGrid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        _productionGrid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
        _productionGrid.RowTemplate.Height=36;
        _productionGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        _productionGrid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(243,247,252);
        foreach(var (name,width) in new (string,int)[]{
            ("Nhân sự",195),("DO hoàn tất",132),("Lượt hoàn tất",132),("Cần kiểm tra",132),
            ("Điểm ghi nhận",150),("Lượt có điểm",132),("Tổng thời gian xử lý",188),
            ("Thời gian ở giữa",200),("Bắt đầu",110),("Kết thúc",110),("Nhận xét",275)})
        {
            _productionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText=name,Width=width,SortMode=DataGridViewColumnSortMode.NotSortable
            });
        }
        var explanation=new TextBox
        {
            Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,WordWrap=true,
            BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,
            BackColor=Color.White,Font=new Font("Segoe UI",10.5f),TabStop=false,
            Margin=new Padding(5,8,5,0),
            Text="CÁCH HIỂU SỐ LIỆU"+Environment.NewLine+
                 "DO hoàn tất chỉ tính những lượt WMS đã trở lại trang quét Tote. "+
                 "Thời gian ở giữa là thời gian của lượt đứng giữa khi xếp các lượt từ nhanh đến chậm "+
                 "(nếu có hai lượt ở giữa, lấy trung bình của chúng). "+
                 "Tổng thời gian xử lý không phải thời gian của cả ca. "+
                 "Thiếu điểm, SKU hoặc Pcs thì không tự suy đoán hay xếp hạng nhân sự."
        };
        root.Controls.Add(_productionOverview,0,0);
        root.Controls.Add(metrics,0,1);
        root.Controls.Add(_productionGrid,0,2);
        root.Controls.Add(explanation,0,3);
        return root;
    }
    private void RefreshProduction(IReadOnlyList<HistoryRow> rows)
    {
        if(_productionGrid.Columns.Count==0)return;
        var data=DailyProduction.From(rows);
        _productionOverview.Text="SẢN LƯỢNG NGÀY "+DateTime.Today.ToString("dd/MM/yyyy");
        var cards=new[]
        {
            "DO hoàn tất"+Environment.NewLine+data.UniqueDo,
            "Lượt hoàn tất"+Environment.NewLine+data.Completed+"/"+data.Attempts,
            "Cần kiểm tra"+Environment.NewLine+data.NeedsReview,
            "Điểm ghi nhận"+Environment.NewLine+(data.Points?.ToString("0.##")??"Chưa có dữ liệu"),
            "Thời gian xử lý"+Environment.NewLine+TimeSpan.FromSeconds(data.ProcessingSeconds).ToString(@"hh\:mm\:ss"),
            "Nhân sự có dữ liệu"+Environment.NewLine+data.People.Count,
            "Lượt đã có điểm"+Environment.NewLine+data.ScoredAttempts+"/"+data.Completed,
            "Chất lượng ghi nhận"+Environment.NewLine+
                 (data.NeedsReview==0?"Không có lượt cần kiểm tra":"Cần kiểm tra WMS")
        };
        for(var i=0;i<_metricCards.Length;i++)_metricCards[i].Text=cards[i];
        _productionGrid.Rows.Clear();
        foreach(var p in data.People)
        {
            _productionGrid.Rows.Add(p.Person,p.UniqueDo,p.Completed,p.NeedsReview,
                p.Points?.ToString("0.##")??"Chưa có dữ liệu",
                p.ScoredAttempts+"/"+p.Completed,
                TimeSpan.FromSeconds(p.ProcessingSeconds).ToString(@"hh\:mm\:ss"),
                p.MedianSeconds.HasValue?p.MedianSeconds.Value+" giây":"—",
                p.FirstStart,p.LastEnd,
                p.NeedsReview>0?"Có "+p.NeedsReview+" lượt cần xác minh WMS":
                p.Completed>0?"Các lượt đã ghi nhận đều hoàn tất trên giao diện WMS":"Chưa có lượt hoàn tất");
        }
    }

    private Control BuildAboutTab()
    {
        var root=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,
            BackColor=Color.FromArgb(247,249,252),
            Padding=new Padding(15,8,15,12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var heading=new Label
        {
            Dock=DockStyle.Fill,Text="THÔNG TIN VÀ BẢO MẬT PHẦN MỀM",
            Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=_navy,
            TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0)
        };
        var actions=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,
            BackColor=Color.White,Padding=new Padding(12,6,12,4)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        _startWithWindows.Dock=DockStyle.Fill;
        _startWithWindows.Text="Tự chạy khi đăng nhập Windows";
        _startWithWindows.Font=new Font("Segoe UI",11,FontStyle.Bold);
        _startWithWindows.Checked=AutoStartManager.IsEnabled();
        _startWithWindows.CheckedChanged+=(_,_) =>
        {
            if(_changingStartupSetting)return;
            try {AutoStartManager.SetEnabled(_startWithWindows.Checked);}
            catch
            {
                _changingStartupSetting=true;
                try{_startWithWindows.Checked=AutoStartManager.IsEnabled();}
                finally{_changingStartupSetting=false;}
                MessageBox.Show(this,
                    "Không thể thay đổi tự khởi động. Chính sách công ty có thể giới hạn mục này.",
                    "Tự khởi động",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        };
        var clearBrowser=MakeButton("XÓA DỮ LIỆU TRÌNH DUYỆT",315);
        clearBrowser.Dock=DockStyle.Fill;
        clearBrowser.BackColor=Color.FromArgb(159,44,50);
        clearBrowser.Margin=new Padding(5,3,3,4);
        clearBrowser.Click+=async (_,_)=>await ResetBrowserAsync(clearBrowser);
        var note=new Label
        {
            Dock=DockStyle.Fill,AutoEllipsis=false,TextAlign=ContentAlignment.MiddleLeft,
            Font=new Font("Segoe UI",9.5f),ForeColor=Color.FromArgb(70,80,95),
            Text="Xóa dữ liệu trình duyệt sẽ đăng xuất WMS và xóa mật khẩu WebView2 đã lưu. Lịch sử đóng gói và danh sách nhân sự được giữ nguyên."
        };
        actions.Controls.Add(_startWithWindows,0,0);
        actions.Controls.Add(clearBrowser,1,0);
        actions.Controls.Add(note,0,1);
        actions.SetColumnSpan(note,2);

        var sections=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,
            Padding=new Padding(0,8,0,0),Margin=new Padding(0)
        };
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        for(var i=0;i<3;i++)sections.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        static Control Section(string title,string body)
        {
            var group=new GroupBox
            {
                Text=title,Dock=DockStyle.Fill,BackColor=Color.White,
                Font=new Font("Segoe UI",11,FontStyle.Bold),
                Padding=new Padding(10,6,10,8),Margin=new Padding(5)
            };
            var text=new TextBox
            {
                Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=true,
                ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,
                Font=new Font("Segoe UI",10.5f,FontStyle.Regular),
                BackColor=Color.White,TabStop=false,Text=body
            };
            group.Controls.Add(text);
            return group;
        }
        sections.Controls.Add(Section("MỤC ĐÍCH SỬ DỤNG",
            "Ứng dụng hỗ trợ thao tác đóng gói WMS: nhập Tote, cập nhật số kiện, yêu cầu in nhãn và xác nhận hoàn tất. "+
            "Đồng thời ghi nhận sản lượng cho người lao động. Đây là công cụ độc lập, không phải phần mềm chính thức của nhà cung cấp WMS."),0,0);
        sections.Controls.Add(Section("LỊCH SỬ NGHIỆP VỤ",
            "Lưu trên máy tính của tài khoản Windows hiện tại: thời gian, nhân sự, mã SO/DO, kết quả đóng gói và các chỉ số có nguồn xác định. "+
            "Cho phép xuất Excel theo yêu cầu. Không tự động gửi lịch sử lên mạng."),1,0);
        sections.Controls.Add(Section("NHẬT KÝ KỸ THUẬT",
            "Chỉ ghi phiên bản, bước thao tác, thời gian và mã lỗi kỹ thuật. "+
            "Không đưa Tote, SO/DO, người thực hiện, nội dung trang WMS, mật khẩu, cookie hoặc token vào log. "+
            "Chỉ xuất log khi người dùng chủ động yêu cầu."),0,1);
        sections.Controls.Add(Section("ĐĂNG NHẬP VÀ MẬT KHẨU",
            "WebView2 có hồ sơ riêng, cố định theo tài khoản Windows. Khi người dùng đồng ý, trình duyệt có thể lưu mật khẩu, cookie và phiên đăng nhập. "+
            "Ứng dụng không tự đọc hoặc trích xuất chúng. Nút Xóa dữ liệu trình duyệt xóa dữ liệu duyệt web trong hồ sơ này."),1,1);
        sections.Controls.Add(Section("GIỚI HẠN VÀ BẢO MẬT",
            "Chỉ hỗ trợ quy trình đóng gói WMS đã xác định. Không có chức năng sửa quyền, cấu hình quản trị, danh mục hàng hóa hoặc dữ liệu ngoài quy trình. "+
            "Không theo dõi ứng dụng khác, không thu thập nội dung HTTP request/response."),0,2);
        sections.Controls.Add(Section("CẬP NHẬT VÀ KHỞI ĐỘNG",
            "Cập nhật phần mềm bằng cách cài bản mới thủ công, không tự kiểm tra hoặc tải bản cập nhật. "+
            "Tự khởi động chỉ áp dụng cho tài khoản Windows hiện tại và không cần quyền quản trị. "+
            "Phần mềm hoạt động trong phạm vi được doanh nghiệp cho phép."),1,2);
        root.Controls.Add(heading,0,0);
        root.Controls.Add(actions,0,1);
        root.Controls.Add(sections,0,2);
        return root;
    }

    private async Task ResetBrowserAsync(Button resetButton)
    {
        if(_busy || _temChanPanel?.IsBusy==true)
        {
            MessageBox.Show(this,"Không thể xóa dữ liệu trình duyệt khi đang xử lý Tote.",
                "Đang xử lý",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }
        if(!_browser.Ready)
        {
            MessageBox.Show(this,"Trình duyệt chưa sẵn sàng. Hãy mở lại ứng dụng và thử lại.",
                "Chưa sẵn sàng",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }
        if(MessageBox.Show(this,
            "Xóa toàn bộ dữ liệu duyệt web trong hồ sơ WebView2 của ứng dụng này, bao gồm:"+
            Environment.NewLine+"- Mật khẩu đã lưu và dữ liệu tự điền."+
            Environment.NewLine+"- Cookie, phiên đăng nhập, bộ nhớ trang và dữ liệu tạm."+
            Environment.NewLine+Environment.NewLine+
            "Sau thao tác phải đăng nhập lại và chọn quầy/máy in. "+
            "Lịch sử đóng gói, danh sách nhân sự và log kỹ thuật không bị xóa."+
            Environment.NewLine+Environment.NewLine+"Anh có chắc chắn muốn tiếp tục?",
            "XÁC NHẬN XÓA DỮ LIỆU TRÌNH DUYỆT",
            MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        _resettingBrowser=true;
        _setupRunning=false;
        _setupDone=false;
        _printerVerified=false;
        _needsRecovery=false;
        resetButton.Enabled=false;
        UpdateControls();
        Status("INIT","Đang xóa dữ liệu WebView2...");
        try
        {
            // Stop the in-flight automatic setup before touching the browser profile.
            // No operation or business record is removed.
            if(_setupTask!=null)await _setupTask;
            await _browser.ClearBrowsingDataAsync();
            _browser.Reveal();
            Status("INIT","Đã xóa dữ liệu trình duyệt. Hãy đăng nhập WMS để thiết lập lại.");
            MessageBox.Show(this,"Đã xóa dữ liệu duyệt web của ứng dụng. Lịch sử và nhân sự được giữ nguyên.",
                "Đã xóa dữ liệu trình duyệt",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch
        {
            _store.Trace("WEB","BROWSING_DATA_RESET_FAILED","ERROR");
            Status("FAULT","Không xóa được toàn bộ dữ liệu trình duyệt. Không khẳng định đã xóa thành công.");
            MessageBox.Show(this,"Không thể xác nhận việc xóa dữ liệu trình duyệt. Hãy đóng phần mềm và kiểm tra lại.",
                "Xóa dữ liệu thất bại",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally
        {
            _resettingBrowser=false;
            resetButton.Enabled=true;
            if(!IsDisposed)
            {
                UpdateControls();
                if(_browser.Ready&&!_setupDone&&!_temChanMode)
                    _setupTask=RunInitialSetupAsync();
            }
        }
    }

    private async Task RunInitialSetupAsync()
    {
        if(_temChanMode||_setupRunning||_setupDone||IsDisposed||_resettingBrowser||_busy)return;
        _setupRunning=true;
        var accountOpenAttempts=0;
        var boothOpenAttempts=0;
        var boothSubmissionAttempts=0;
        var boothInputWaits=0;
        var stationReadbackWaits=0;
        var printerExpandAttempts=0;
        var readyWaits=0;
        bool printerSelectionNeedsRecheck=false;
        try
        {
            // A new login is not "ready" until the displayed station code and
            // currently selected printer have both been verified from the WMS UI.
            void Manual(string reason)
            {
                if(IsDisposed)return;
                _setupRunning=false;
                _printerVerified=false;
                _store.Trace("SETUP","MANUAL_REQUIRED","BLOCKED",0,
                    Regex.IsMatch(reason,"^[A-Z0-9_]{1,60}$")?reason:"UNKNOWN");
                Status("FAULT","Không xác minh được quầy SDG.22 / máy in D29 ("+
                    reason+"). Chuyển sang Trình duyệt để xử lý bằng tay.");
                ShowBrowser();
                UpdateControls();
            }
            async Task<bool> Step(string code)
            {
                if(_resettingBrowser||!_setupRunning)return false;
                var result=await _browser.SetupActionAsync(code);
                var reason=Regex.IsMatch(result.reason??"","^[A-Z0-9_]{1,60}$")
                    ?result.reason:"UNSPECIFIED";
                _store.Trace("SETUP","ACTION_"+code,result.ok?"DONE":"BLOCKED",0,
                    result.ok?"":reason);
                if(result.ok)return true;
                Manual(reason);
                return false;
            }

            while(_setupRunning&&!_setupDone&&!IsDisposed&&!_resettingBrowser&&!_busy)
            {
                await Task.Delay(1050);
                if(!_setupRunning||IsDisposed||_resettingBrowser)break;
                _browser.TryNavigatePackingAfterLogin();
                var p=await _browser.SetupPeekAsync();
                if(!_setupRunning||_resettingBrowser)break;
                if(p?.ok!=true)continue; // WMS is still on the login page.

                if(p.boothDialog)
                {
                    // Case 1: the station modal appeared on its own at page load.
                    // Case 2: it opened after Enter on the blank Tote field.
                    // Either way, never send another Tote Enter while this modal exists.
                    _store.Trace("SETUP","BOOTH_MODAL_DETECTED","DONE",0,
                        p.boothInputReady?"INPUT_READY":"INPUT_MISSING");
                    if(boothSubmissionAttempts>0)
                    {
                        if(++stationReadbackWaits>=3){Manual("BOOTH_MODAL_NOT_DISMISSED");break;}
                        continue; // WMS may be validating an earlier submission.
                    }
                    if(!p.boothInputReady)
                    {
                        if(++boothInputWaits>=5){Manual("BOOTH_INPUT_NOT_READY");break;}
                        continue; // Angular may create the dialog shell before the input.
                    }
                    boothSubmissionAttempts++;
                    if(!await Step("BOOTH_SET"))break;
                    // Give Angular form validation time to enable its confirm action.
                    await Task.Delay(350);
                    var filled=await _browser.SetupPeekAsync();
                    if(!_setupRunning||_resettingBrowser)break;
                    if(filled?.boothCorrect==true&&!filled.boothDialog)continue;
                    if(filled?.boothDialog!=true||!filled.boothValueCorrect)
                    {
                        Manual("BOOTH_FILL_NOT_CONFIRMED");break;
                    }
                    if(filled.boothConfirmEnabled)
                    {
                        if(!await Step("BOOTH_CONFIRM"))break;
                        _store.Trace("SETUP","BOOTH_SUBMIT_BUTTON","DONE");
                    }
                    else
                    {
                        // Recorded WMS also accepts a real Enter in the station input.
                        if(!await Step("BOOTH_FOCUS"))break;
                        try
                        {
                            await _browser.EnterAsync();
                            _store.Trace("SETUP","BOOTH_SUBMIT_ENTER","DONE");
                        }
                        catch {Manual("BOOTH_SUBMIT_ENTER_FAILED");break;}
                    }
                    // Do not assume a successful click/Enter means the station was set.
                    await Task.Delay(550);
                    continue; // Require the rendered [SDG.22] header on next peek.
                }

                if(!p.boothCorrect)
                {
                    // After sending the station code, wait briefly for readback.
                    // Never reopen/resubmit automatically if its result is unknown.
                    if(boothSubmissionAttempts>0)
                    {
                        if(++stationReadbackWaits>=4){Manual("BOOTH_READBACK_NOT_SDG22");break;}
                        continue;
                    }
                    if(!p.boothHeaderKnown)
                    {
                        if(++readyWaits>8){Manual("BOOTH_HEADER_UNKNOWN");break;}
                        continue;
                    }
                    if(!p.packingReady)
                    {
                        if(++readyWaits>8){Manual("BOOTH_INPUT_NOT_READY");break;}
                        continue;
                    }
                    if(++boothOpenAttempts>1){Manual("BOOTH_NOT_CONFIRMED");break;}
                    if(!await Step("BOOTH_PREPARE"))break;
                    try
                    {
                        // Send actual Enter through the WebView2 browser input route,
                        // never type a fake Tote or click by screen coordinates.
                        await _browser.EnterAsync();
                        _store.Trace("SETUP","BOOTH_OPEN_ENTER","DONE");
                    }
                    catch {Manual("BOOTH_ENTER_FAILED");break;}
                    continue;
                }

                readyWaits=0;
                if(p.printerDialog)
                {
                    accountOpenAttempts=0;
                    if(p.printerCorrect)
                    {
                        if(!await Step("PRINTER_CLOSE"))break;
                        _printerVerified=true;
                        printerSelectionNeedsRecheck=false;
                        continue;
                    }
                    if(p.printerOptionVisible)
                    {
                        if(!await Step("PRINTER_PICK"))break;
                        await Task.Delay(350);
                        if(!await Step("PRINTER_CONFIRM"))break;
                        printerSelectionNeedsRecheck=true;
                        // Do not mark ready until the dialog is opened again
                        // and the exact selected D29 label is read back.
                        continue;
                    }
                    if(p.printerEmpty||p.printerWrong)
                    {
                        if(++printerExpandAttempts>2){Manual("PRINTER_OPTION_NOT_VISIBLE");break;}
                        if(!await Step("PRINTER_EXPAND"))break;
                        continue;
                    }
                    Manual("PRINTER_FIELD_UNKNOWN");break;
                }

                if(p.packingReady&&_printerVerified&&!printerSelectionNeedsRecheck)
                {
                    _setupDone=true;
                    _browser.StopStartupRouting();
                    _store.Trace("SETUP","STATION_D29_VERIFIED","DONE");
                    Status("READY","Đã xác minh quầy SDG.22 và máy in 21-1129-D29 (Online). Sẵn sàng quét Tote.");
                    UpdateControls();
                    HideBrowser();
                    FocusToteIfIdle();
                    break;
                }
                if(p.settingsVisible)
                {
                    if(!await Step("PRINTER_MENU"))break;
                    continue;
                }
                if(!p.packingReady)
                {
                    if(++readyWaits>8){Manual("PACKING_NOT_READY");break;}
                    continue;
                }
                if(p.accountTriggerCount!=1||accountOpenAttempts>=3)
                {
                    Manual("PRINTER_MENU_NOT_UNIQUE");
                    break;
                }
                accountOpenAttempts++;
                if(!await Step("ACCOUNT_OPEN"))break;
            }
        }
        catch
        {
            if(!IsDisposed)
            {
                _store.Trace("SETUP","STARTUP_EXCEPTION","ERROR");
                Status("FAULT","Không tự xác minh được quầy hoặc máy in. Kiểm tra thủ công trong tab Trình duyệt.");
                ShowBrowser();
            }
        }
        finally
        {
            _setupRunning=false;
            if(!IsDisposed)UpdateControls();
        }
    }

    private async Task StartTraceAsync()
    {
        _traceStart.Enabled=false;
        try
        {
            await _browser.BeginRecordingAsync();
            _traceStop.Enabled=true;
            _traceStatus.Text="ĐANG GHI · thao tác quầy / máy in · Kết thúc để lưu ZIP Desktop";
            _traceStatus.ForeColor=Color.DarkRed;
        }
        catch(Exception e)
        {
            _traceStart.Enabled=true;
            _traceStatus.Text="KHÔNG BẮT ĐẦU: "+e.Message;
            _store.Trace("RECORDER","UI_START_FAILED","ERROR");
            MessageBox.Show(this,e.Message,"Ghi thao tác WMS",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    private async Task StopTraceAsync()
    {
        _traceStop.Enabled=false;
        _traceStatus.Text="Đang đóng gói log ra Desktop...";
        try
        {
            var path=await _browser.EndRecordingAsync();
            _traceStatus.Text="ĐÃ XUẤT: "+Path.GetFileName(path);
            _traceStatus.ForeColor=Color.DarkGreen;
            _traceStart.Enabled=true;
            MessageBox.Show(this,"Đã ghi và xuất log thao tác WMS ra Desktop:\n"+path,
                "Đã xuất log thao tác",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception e)
        {
            _traceStatus.Text="LỖI XUẤT LOG: "+e.Message;
            _traceStart.Enabled=!_browser.Recording;
            _traceStop.Enabled=_browser.Recording;
            _store.Trace("RECORDER","UI_EXPORT_FAILED","ERROR");
            MessageBox.Show(this,e.Message+"\nNếu xuất không thành công, dữ liệu ghi tạm vẫn nằm tại "+
                "thư mục recordings trong dữ liệu ứng dụng.","Không xuất được log",
                MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void UpdateBrowserToggle()
    {
        if(IsDisposed)return;
        var shown=_tabs.TabPages.Contains(_webTab);
        _show.Text=shown?"ẨN TRÌNH DUYỆT":"HIỆN TRÌNH DUYỆT";
        if(_temChanPanel!=null)
        {
            _temChanPanel.BrowserIsShown=shown;
            _temChanPanel.UpdateBrowserButton();
        }
    }
    private void ShowBrowser()
    {
        if(IsDisposed)return;
        if(!_tabs.TabPages.Contains(_webTab))
            _tabs.TabPages.Insert(1,_webTab);
        _tabs.SelectedTab=_webTab;
        UpdateBrowserToggle();
    }
    private void HideBrowser()
    {
        if(IsDisposed)return;
        if(!_tabs.TabPages.Contains(_webTab))return;
        if(_tabs.SelectedTab==_webTab)
            _tabs.SelectedTab=_temChanMode?_temChanTab:_operationTab;
        _tabs.TabPages.Remove(_webTab);
        UpdateBrowserToggle();
    }
    private sealed class GridWidths
    {
        public int[] History {get;set;}=[];
        public int[] Production {get;set;}=[];
    }
    private string GridWidthFile=>Path.Combine(_store.Root,"grid-widths.json");
    private void RestoreColumnWidths()
    {
        try
        {
            if(!File.Exists(GridWidthFile))return;
            var saved=JsonSerializer.Deserialize<GridWidths>(File.ReadAllText(GridWidthFile));
            if(saved is null)return;
            for(var i=0;i<Math.Min(_history.Columns.Count,saved.History.Length);i++)
                if(saved.History[i]>=55&&saved.History[i]<=1400)
                    _history.Columns[i].Width=saved.History[i];
            for(var i=0;i<Math.Min(_productionGrid.Columns.Count,saved.Production.Length);i++)
                if(saved.Production[i]>=55&&saved.Production[i]<=1400)
                    _productionGrid.Columns[i].Width=saved.Production[i];
        }
        catch { _store.Trace("UI","RESTORE_COLUMN_WIDTHS","ERROR"); }
    }
    private void QueueColumnWidthSave()
    {
        if(IsDisposed)return;
        _saveWidthsTimer.Stop();
        _saveWidthsTimer.Start();
    }
    private void SaveColumnWidths()
    {
        try
        {
            Directory.CreateDirectory(_store.Root);
            var data=new GridWidths{
                History=_history.Columns.Cast<DataGridViewColumn>().Select(c=>c.Width).ToArray(),
                Production=_productionGrid.Columns.Cast<DataGridViewColumn>().Select(c=>c.Width).ToArray()
            };
            var temp=GridWidthFile+".tmp";
            File.WriteAllText(temp,JsonSerializer.Serialize(data));
            File.Move(temp,GridWidthFile,true);
        }
        catch { _store.Trace("UI","SAVE_COLUMN_WIDTHS","ERROR"); }
    }

    private void UpdateControls()
    {
        var allow=!_busy&&!_needsRecovery&&_setupDone;
        _ok.Enabled=allow;_tote.Enabled=allow;_qty.Enabled=allow;
        _peopleButton.Enabled=!_busy;
        _recover.Enabled=!_busy&&(_needsRecovery||!_setupDone);
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
        // Counts are non-sensitive; exported logs now show whether records were loaded
        // and selected by the daily view, without exposing business data.
        _store.Trace("HISTORY","DISPLAY_COUNTS","DONE",0,
            "TODAY="+today.Count+";READ_ERROR="+(_store.HistoryReadFailed?1:0));
        foreach(var r in today.Reverse().Take(500))
            _history.Rows.Add(r.Date,r.Start,r.End,r.Seconds,r.Person,r.SO,r.DO,
                r.SkuCount?.ToString()??"—",r.ParcelCount?.ToString()??"—",
                r.Pcs?.ToString()??"—",LocalStore.ResultLabel(r.Result),r.Points?.ToString("0.##")??"—");
        RefreshProduction(today);
        _historyHeading.Text="LỊCH SỬ HÔM NAY  |  "+_displayDate.ToString("dd/MM/yyyy")+"  |  "+today.Count+" lượt";
        if(_store.HistoryReadFailed)
        {
            _historyHeading.Text+="  |  CẢNH BÁO ĐỌC LỊCH SỬ";
            _historyHeading.ForeColor=Color.DarkRed;
        }
        else _historyHeading.ForeColor=_navy;
    }
    private void FocusToteIfIdle()
    {
        if(IsDisposed||!IsHandleCreated||_busy||_needsRecovery)return;
        BeginInvoke(new Action(()=>
        {
            if(IsDisposed||_busy||_needsRecovery||!_tote.Enabled)return;
            if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
            Activate();
            _tabs.SelectedTab=_operationTab;
            _tote.Focus();
            _tote.SelectAll();
        }));
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
        if(_busy||_needsRecovery||!_setupDone)return;
        var tote=_tote.Text.Trim().ToUpperInvariant();
        if(!ToteFormat.IsMatch(tote)){MessageBox.Show("Mã Tote không hợp lệ.");return;}
        if(!int.TryParse(_qty.Text.Trim(),out var count)||count<1||count>9999)
        {MessageBox.Show("Số kiện phải từ 1 đến 9999.");return;}
        var ready=await _browser.PeekAsync();
        if(ready?.ready!=true)
        {_browser.Reveal();MessageBox.Show("WMS chưa ở màn hình quét Tote. Vui lòng đăng nhập và chọn máy in.");return;}
        _people=_store.LoadPeople();
        if(_people.Count==0){ManagePeople();return;}
        // Selecting a person and pressing Enter is the sole confirmation.
        var person=ChoosePerson();
        if(person==null)return;
        _busy=true;_cancel=new CancellationTokenSource();_watch.Restart();UpdateControls();
        var started=DateTime.Now;
        var attemptId=Guid.NewGuid().ToString("N").ToUpperInvariant();
        string so="",doCode="";var success=false;var resultCode="UNCERTAIN_CHECK_WMS";
        HistoryRow Snapshot(string phase,string result) => new HistoryRow(
            LocalStore.FormatHistoryDate(started),started.ToString("HH:mm:ss"),
            DateTime.Now.ToString("HH:mm:ss"),(long)Math.Round(_watch.Elapsed.TotalSeconds),
            person,so,doCode,result){AttemptId=attemptId,Phase=phase,ParcelCount=count};
        void Checkpoint(string phase)
        {
            if(!_store.TryAppendHistory(Snapshot(phase,"IN_PROGRESS"),phase))
                throw new WorkflowHalt("Không thể lưu lịch sử vào máy. Đã dừng để tránh mất dữ liệu.");
            RefreshHistory();
        }
        _store.Trace("TOTE","JOB_STARTED","OK");
        try
        {
            // Persist BEFORE the first WMS action. Further checkpoints update the same attempt.
            Checkpoint("START");
            var ct=_cancel.Token;
            Status("TOTE","Nhập Tote và Enter...");
            await ActionAsync("TOTE",tote,ct);
            await _browser.EnterAsync();
            var p=await WaitAsync(x=>x.packCount==1,"PACK_BUTTON",30,ct);
            so=p.so;doCode=p.Do;
            Checkpoint("WMS_INFO");
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
            Checkpoint("PRINT_READY");
            Status("PRINT","Mở hộp thoại in nhãn...");
            await ActionAsync("PRINT",null,ct);
            Checkpoint("PRINT_REQUEST");
            await WaitAsync(x=>x.printLabelCount==1&&x.dialogCount==1,"PRINT_DIALOG",20,ct);
            await ActionAsync("PRINT_LABEL",null,ct);
            Checkpoint("LABEL_REQUEST");
            await WaitAsync(x=>x.printLabelCount==0&&x.dialogCount==0&&x.finishCount==1,"PRINT_CLOSED",25,ct);
            Status("FINISH","Hoàn thành đóng gói...");
            await ActionAsync("FINISH",null,ct);
            await WaitAsync(x=>x.yesCount==1&&x.dialogCount==1,"FINISH_CONFIRM",20,ct);
            await ActionAsync("YES",null,ct);
            await WaitAsync(x=>x.noCount==1&&x.dialogCount==1&&x.outboundSlipPrompt,"NO_INSPECTION_DIALOG",20,ct);
            await ActionAsync("NO",null,ct);
            Checkpoint("FINISH_CONFIRMED");
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
            // Final row supersedes the latest checkpoint; do not lose prior snapshots on failure.
            var finalRow=Snapshot("FINAL",success?"UI_WORKFLOW_COMPLETE":resultCode) with
            {
                End=ended.ToString("HH:mm:ss")
            };
            var saved=_store.TryAppendHistory(finalRow,"FINAL");
            RefreshHistory();
            if(!saved)
            {
                _needsRecovery=true;
                Status("FAULT","LƯU LỊCH SỬ THẤT BẠI. Kiểm tra ổ đĩa/quyền ghi trước khi xử lý tiếp.");
                MessageBox.Show(this,
                    "Không ghi được kết quả cuối vào lịch sử nội bộ. Lượt đang xử lý đã có các mốc lưu trước đó nếu ghi đĩa vẫn hoạt động. Hãy kiểm tra quyền ghi hoặc dung lượng ổ đĩa.",
                    "Lỗi lưu lịch sử",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
            _tote.Clear();_qty.Clear();
            _busy=false;_cancel?.Dispose();_cancel=null;
            UpdateControls();FocusToteIfIdle();
        }
    }
    private string? ChoosePerson()
    {
        using var f=new Form
        {
            Text="CHỌN NHÂN SỰ",Width=620,Height=550,MinimumSize=new Size(470,410),
            StartPosition=FormStartPosition.CenterParent,Font=new Font("Segoe UI",11),
            BackColor=Color.FromArgb(247,249,252)
        };
        var hint=new Label
        {
            Text="Chọn nhân sự bằng chuột hoặc phím mũi tên, nhấn ENTER để xác nhận",
            Dock=DockStyle.Top,Height=54,Padding=new Padding(14,6,10,0),
            TextAlign=ContentAlignment.MiddleLeft
        };
        var list=new ListBox
        {
            Dock=DockStyle.Fill,Margin=new Padding(12),
            Font=new Font("Segoe UI",12,FontStyle.Regular),
            DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=50,IntegralHeight=false,
            BorderStyle=BorderStyle.None,BackColor=Color.White
        };
        using var selectedFont=new Font(list.Font.FontFamily,list.Font.Size*1.2f,FontStyle.Bold);
        list.Items.AddRange(_people.Cast<object>().ToArray());
        list.SelectedIndex=list.Items.Count>0?0:-1; // Preselect first; arrows still change it.
        list.DrawItem+=(_,e)=>
        {
            if(e.Index<0||e.Index>=list.Items.Count)return;
            var isSelected=(e.State&DrawItemState.Selected)==DrawItemState.Selected;
            using var background=new SolidBrush(isSelected?Color.FromArgb(30,102,175):Color.White);
            e.Graphics.FillRectangle(background,e.Bounds);
            var color=isSelected?Color.White:Color.FromArgb(35,48,65);
            var bounds=new Rectangle(e.Bounds.X+14,e.Bounds.Y,e.Bounds.Width-24,e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics,list.Items[e.Index]?.ToString()??"",
                isSelected?selectedFont:list.Font,bounds,color,
                TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        list.SelectedIndexChanged+=(_,_)=>list.Invalidate();
        var footer=new Panel{Dock=DockStyle.Bottom,Height=69,Padding=new Padding(12)};
        var confirm=MakeButton("XÁC NHẬN (ENTER)",210);
        confirm.Dock=DockStyle.Right;
        confirm.Enabled=list.SelectedIndex>=0;
        list.SelectedIndexChanged+=(_,_)=>confirm.Enabled=list.SelectedIndex>=0;
        void AcceptSelection()
        {
            if(list.SelectedIndex<0)return;
            f.DialogResult=DialogResult.OK;
            f.Close();
        }
        confirm.Click+=(_,_)=>AcceptSelection();
        list.KeyDown+=(_,e)=>
        {
            if(e.KeyCode==Keys.Enter)
            {
                e.SuppressKeyPress=true;
                AcceptSelection();
            }
        };
        list.MouseDoubleClick+=(_,_)=>AcceptSelection();
        f.AcceptButton=confirm;
        footer.Controls.Add(confirm);
        f.Controls.Add(list);
        f.Controls.Add(footer);
        f.Controls.Add(hint);
        f.Shown+=(_,_)=>list.Focus();
        return f.ShowDialog(this)==DialogResult.OK && list.SelectedIndex>=0
            ?_people[list.SelectedIndex]:null;
    }
    private void ManagePeople()
    {
        if(_busy)return;
        using var f=new Form{Text="Nhân sự dùng chung 2 ca",Width=610,Height=560,StartPosition=FormStartPosition.CenterParent};
        var list=new ListBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",11)};
        void Reload(){_people=_store.LoadPeople();list.Items.Clear();list.Items.AddRange(_people.Cast<object>().ToArray());}
        Reload();
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=100,AutoScroll=true,WrapContents=true};
        var add=MakeButton("THÊM NHÂN SỰ (A, B, C)",270);
        var edit=MakeButton("SỬA",82);var delete=MakeButton("XÓA",82);var clear=MakeButton("XÓA TOÀN BỘ",145);
        add.Click+=(_,_)=>{
            var s=Prompt("Nhập nhân sự (A, B, C hoặc A,B,C)","");
            if(s==null)return;
            var parts=s.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(parts.Length<1||parts.Length>200||parts.Any(x=>!LocalStore.ValidName(x)))
            {MessageBox.Show("Danh sách phải có 1–200 tên hợp lệ, mỗi tên tối đa 70 ký tự.");return;}
            _people.AddRange(parts);_store.SavePeople(_people);Reload();
        };
        edit.Click+=(_,_)=>{if(list.SelectedIndex<0)return;
            var i=list.SelectedIndex;var s=Prompt("Sửa nhân sự",_people[i]);
            if(s!=null){if(!LocalStore.ValidName(s)){MessageBox.Show("Chỉ sửa một tên nhân sự tại một thời điểm.");return;} _people[i]=s;_store.SavePeople(_people);Reload();}};
        delete.Click+=(_,_)=>{if(list.SelectedIndex<0)return;
            if(MessageBox.Show("Xóa người được chọn?", "Xác nhận",MessageBoxButtons.YesNo)==DialogResult.Yes)
            {_people.RemoveAt(list.SelectedIndex);_store.SavePeople(_people);Reload();}};
        clear.Click+=(_,_)=>{
            if(MessageBox.Show("XÓA TOÀN BỘ DANH SÁCH NHÂN SỰ? Lịch sử đã ghi vẫn được giữ.",
                "Xác nhận xóa toàn bộ",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes)
            {_people.Clear();_store.SavePeople(_people);Reload();}
        };
        buttons.Controls.AddRange([add,edit,delete,clear]);
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
            var rows=_store.FilterHistory(start,end,filter).Count;
            _store.ExportHistoryXlsx(dialog.FileName,start,end,filter);
            _store.Trace("EXPORT","HISTORY_XLSX","DONE",0,"ROWS="+rows);
            MessageBox.Show(this,rows>0
                ?"Đã xuất "+rows+" dòng lịch sử Excel theo bộ lọc đã chọn."
                :"File Excel không có dòng dữ liệu phù hợp. Kiểm tra ngày và bộ lọc đã chọn. Nếu đã có lượt xử lý, hãy xuất log để kiểm tra.",
                rows>0?"Xuất lịch sử thành công":"Chưa tìm thấy dữ liệu lịch sử",
                MessageBoxButtons.OK,rows>0?MessageBoxIcon.Information:MessageBoxIcon.Warning);
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
