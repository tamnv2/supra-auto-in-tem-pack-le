using System.Drawing;
using System.Windows.Forms;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SupraWmsRecorder;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_,e) => MessageBox.Show(e.Exception.Message,
            "Lỗi Recorder", MessageBoxButtons.OK, MessageBoxIcon.Error);
        bool smoke = args.Contains("--smoke");
        Application.Run(new RecorderForm(smoke));
    }
}

internal sealed class RecorderForm : Form
{
    private const string PackingUrl = "https://wms-supra.winmart.vn/sft3/app/saleorder/packing";
    private readonly WebView2 _web = new() { Dock=DockStyle.Fill };
    private readonly Label _status = new() { Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft };
    private readonly Button _start = new() { Dock=DockStyle.Fill, Text="BẮT ĐẦU GHI", Enabled=false };
    private readonly Button _stop = new() { Dock=DockStyle.Fill, Text="KẾT THÚC & XUẤT LOG", Enabled=false };
    private readonly bool _smoke;
    private bool _active;
    private bool _finishing;
    private string? _jsonl;
    private Channel<string>? _events;
    private Task? _writerTask;
    private int _eventCount;
    private DateTimeOffset _startedAt;
    private System.Windows.Forms.Timer? _smokeTimer;

    public RecorderForm(bool smoke)
    {
        _smoke=smoke;
        Text="SUPRA WMS | Ghi thao tác và kiểm tra máy in (WebView2)";
        WindowState=FormWindowState.Maximized;
        MinimumSize=new Size(900,500);
        Font=new Font("Segoe UI",10F);
        BackColor=Color.FromArgb(240,243,247);
        var shell=new TableLayoutPanel { Dock=DockStyle.Fill, RowCount=3, ColumnCount=1, Margin=Padding.Empty, Padding=Padding.Empty };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,32));

        var header=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=5, RowCount=1,
            BackColor=Color.FromArgb(24,44,75), Padding=new Padding(12,9,12,9) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,10));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,2));
        var title=new Label { Text="SUPRA WMS  |  GHI THAO TÁC", Dock=DockStyle.Fill,
            ForeColor=Color.White, Font=new Font("Segoe UI",11,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleLeft, AutoEllipsis=true };
        _start.BackColor=Color.FromArgb(21,119,79);
        _start.ForeColor=Color.White;
        _start.FlatStyle=FlatStyle.Flat;
        _stop.BackColor=Color.FromArgb(168,60,55);
        _stop.ForeColor=Color.White;
        _stop.FlatStyle=FlatStyle.Flat;
        _start.Font=new Font("Segoe UI",9F,FontStyle.Bold);
        _stop.Font=new Font("Segoe UI",9F,FontStyle.Bold);
        header.Controls.Add(title,0,0);
        header.Controls.Add(_start,1,0);
        header.Controls.Add(_stop,3,0);
        _status.Text="Đang khởi tạo trình duyệt WMS...";
        _status.Padding=new Padding(12,0,12,0);
        shell.Controls.Add(header,0,0);
        shell.Controls.Add(_web,0,1);
        shell.Controls.Add(_status,0,2);
        Controls.Add(shell);
        _start.Click += async (_,_) => await StartRecordingAsync();
        _stop.Click += async (_,_) => await StopRecordingAsync();
        Shown += async (_,_) => await InitializeAsync();
        FormClosing += (_,e) => {
            if(_active || _finishing) {
                e.Cancel=true;
                MessageBox.Show("Hãy bấm KẾT THÚC & XUẤT LOG trước khi đóng.",
                    "Đang ghi thao tác",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        };
    }

    private async Task InitializeAsync()
    {
        if(_smoke)
        {
            _smokeTimer=new System.Windows.Forms.Timer { Interval=28000 };
            _smokeTimer.Tick += (_,_) => SmokeFailed("Timeout initializing WebView2");
            _smokeTimer.Start();
        }
        try
        {
            string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupraWmsRecorderV5","WebView2");
            Directory.CreateDirectory(profile);
            var environment=await CoreWebView2Environment.CreateAsync(null,profile);
            var options=environment.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled=true;
            await _web.EnsureCoreWebView2Async(environment,options);
            var core=_web.CoreWebView2;
            core.Settings.IsWebMessageEnabled=true;
            core.Settings.AreDevToolsEnabled=false;
            core.Settings.IsGeneralAutofillEnabled=false;
            core.WebMessageReceived += (_,e) => Receive(e);
            core.NavigationStarting += (_,e) => { if(_active) Record("navigation_started",new { page=SafePath(e.Uri) }); };
            core.NavigationCompleted += async (_,e) => {
                _status.Text=e.IsSuccess ? (_active ? "ĐANG GHI · WMS sẵn sàng thao tác." :
                    "Đã tải trang. Đăng nhập, đến bước nhập quầy rồi bấm BẮT ĐẦU GHI.")
                    : "Không tải được trang WMS. Kiểm tra mạng và đăng nhập.";
                if(_active) {
                    Record("navigation_completed",new { success=e.IsSuccess, error=e.WebErrorStatus.ToString() });
                    await SnapshotAsync("navigation");
                }
                if(_smoke && e.IsSuccess) await RunSmokeAsync();
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(CaptureScript);
            _start.Enabled=true;
            if(_smoke)
                core.NavigateToString("<html><body><h2>QUẦY ĐÓNG GÓI</h2><button id='check-printer'>Thiết lập máy in</button></body></html>");
            else
                core.Navigate(PackingUrl);
        }
        catch(Exception e)
        {
            _status.Text="Không khởi tạo được WebView2: "+e.Message;
            if(_smoke) SmokeFailed(e.ToString());
            else MessageBox.Show("Không khởi tạo được trình duyệt WMS:\n"+e.Message+
                "\nCần cài Microsoft Edge WebView2 Runtime.", "Recorder",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private async Task StartRecordingAsync()
    {
        if(_active || _finishing || _web.CoreWebView2 is null) return;
        try
        {
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupraWmsRecorderV5","Pending");
            Directory.CreateDirectory(folder);
            _jsonl=Path.Combine(folder,"events_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".jsonl");
            var file=new FileStream(_jsonl,FileMode.CreateNew,FileAccess.Write,FileShare.Read,16384,useAsync:true);
            _events=Channel.CreateUnbounded<string>(new UnboundedChannelOptions
                { SingleReader=true,SingleWriter=false });
            var chan=_events;
            _writerTask=Task.Run(async () => {
                await using var stream=file;
                await using var output=new StreamWriter(stream,new UTF8Encoding(false));
                await foreach(string line in chan.Reader.ReadAllAsync())
                    await output.WriteLineAsync(line);
                await output.FlushAsync();
            });
            _eventCount=0;
            _startedAt=DateTimeOffset.Now;
            _active=true;
            _start.Enabled=false;
            _stop.Enabled=true;
            _status.Text="ĐANG GHI · thực hiện nhập quầy và kiểm tra máy in.";
            Record("session_started",new { version="5.0.0",browser="WebView2", mode="InPrivate",
                privacy="no passwords, cookies, tokens, raw text-field values or network bodies" });
            await SnapshotAsync("start");
        }
        catch(Exception e)
        {
            _active=false;
            _status.Text="Không bắt đầu được: "+e.Message;
        }
    }

    private void Record(string kind,object payload)
    {
        if(!_active || _events is null)return;
        string line=JsonSerializer.Serialize(new { time=DateTimeOffset.Now.ToString("O"),kind,data=payload });
        if(_events.Writer.TryWrite(line)) _eventCount++;
    }

    private void Receive(CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!_active) return;
        try
        {
            using var document=JsonDocument.Parse(e.TryGetWebMessageAsString());
            var obj=document.RootElement;
            string kind=Get(obj,"kind",35);
            if(!new[] { "click","pointerdown","keydown","change","focus","print_start","print_finish",
                         "snapshot","dialog" }.Contains(kind)) return;
            Record("web_"+kind,new {
                page=SafePath(e.Source),
                tag=Get(obj,"tag",35), role=Get(obj,"role",48),
                selector=Get(obj,"selector",220),text=Scrub(Get(obj,"text",220)),
                label=Scrub(Get(obj,"label",150)),type=Get(obj,"type",32),
                key=Get(obj,"key",24),station=GetStation(obj),snapshot=Scrub(Get(obj,"snapshot",2200))
            });
        }
        catch { /* ignore messages not matching recorder's strict event schema */ }
    }

    private static string Get(JsonElement json,string key,int maximum)
    {
        if(!json.TryGetProperty(key,out var value) || value.ValueKind!=JsonValueKind.String)return "";
        string s=value.GetString()??"";
        return s.Length>maximum ? s[..maximum] : s;
    }

    private static string GetStation(JsonElement json)
    {
        string s=Get(json,"station",24);
        return Regex.IsMatch(s,@"^SDG\.[0-9]{1,4}$",RegexOptions.IgnoreCase) ? s : "";
    }

    private static string Scrub(string s)
    {
        s=Regex.Replace(s,@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b","[email hidden]");
        s=Regex.Replace(s,@"\b(?:\+?84|0)[0-9 .-]{8,13}\b","[phone hidden]");
        s=Regex.Replace(s,@"\b[A-Za-z0-9_-]{40,}\b","[long-id hidden]");
        s=Regex.Replace(s,@"(?i)(password|passwd|token|bearer|cookie|authorization)\s*[:=]\s*\S+","[credential hidden]");
        return s;
    }

    private static string SafePath(string? raw)
    {
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri))return "(unknown)";
        if(uri.Scheme!="https" && uri.Scheme!="http")return "(local page)";
        string path=uri.AbsolutePath;
        path=Regex.Replace(path,@"[A-Za-z0-9_-]{18,}",":id");
        return uri.Host+path; // intentionally excludes query and fragment
    }

    private async Task SnapshotAsync(string reason)
    {
        try { await _web.ExecuteScriptAsync("window.__supraRecorderSnapshot&&window.__supraRecorderSnapshot("+JsonSerializer.Serialize(reason)+")"); }
        catch { /* login navigations can unload a document mid-script */ }
    }

    private async Task StopRecordingAsync()
    {
        if(!_active || _finishing || _events is null || _jsonl is null) return;
        _finishing=true;
        _stop.Enabled=false;
        _status.Text="Đang kết thúc và đóng gói log ra Desktop...";
        try
        {
            await SnapshotAsync("finish");
            await Task.Delay(200);
            Record("session_finished",new { elapsedMs=(DateTimeOffset.Now-_startedAt).TotalMilliseconds,
                events=_eventCount });
            _active=false;
            _events.Writer.TryComplete();
            if(_writerTask is not null) await _writerTask;
            string source=_jsonl;
            int count=_eventCount;
            DateTimeOffset started=_startedAt;
            string result=await Task.Run(() => Export(source,count,started));
            _status.Text="ĐÃ XUẤT: "+result;
            _start.Enabled=true;
            if(_smoke)
            {
                string content=File.ReadAllText(source); // Export retains source for smoke assertions
                if(!content.Contains("web_click") || !content.Contains("Thiết lập máy in"))
                    throw new InvalidOperationException("Smoke failed: browser DOM click missing in trace.");
                string? marker=Environment.GetEnvironmentVariable("SUPRA_SMOKE_RESULT");
                if(!string.IsNullOrWhiteSpace(marker))File.WriteAllText(marker,"PASS "+result);
                _smokeTimer?.Stop();
                _finishing=false;
                Close();
            }
        }
        catch(Exception e)
        {
            _status.Text="KHÔNG XUẤT ĐƯỢC LOG: "+e.Message+" | JSONL vẫn còn tại "+_jsonl;
            if(_smoke) SmokeFailed(e.ToString());
        }
        finally { _finishing=false; }
    }

    private static string Export(string input,int count,DateTimeOffset started)
    {
        string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if(string.IsNullOrWhiteSpace(desktop)) throw new IOException("Không xác định được thư mục Desktop.");
        Directory.CreateDirectory(desktop);
        string output=Path.Combine(desktop,"SUPRA_WMS_Trace_V5_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".zip");
        using(var archive=ZipFile.Open(output,ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(input,"events.jsonl",CompressionLevel.Optimal);
            var meta=archive.CreateEntry("session.json",CompressionLevel.Optimal);
            using var w=new StreamWriter(meta.Open(),Encoding.UTF8);
            w.Write(JsonSerializer.Serialize(new { version="5.0.0",started=started.ToString("O"),ended=DateTimeOffset.Now.ToString("O"),
                eventCount=count,format="jsonl",fields="DOM labels/selectors, page state, actions, station code only if SDG.*, no credentials" }));
        }
        return output;
    }

    private async Task RunSmokeAsync()
    {
        try
        {
            if(_active || _finishing)return;
            await StartRecordingAsync();
            await _web.ExecuteScriptAsync("document.getElementById('check-printer').click()");
            await Task.Delay(550);
            await StopRecordingAsync();
        }
        catch(Exception e) { SmokeFailed(e.ToString()); }
    }

    private void SmokeFailed(string error)
    {
        _smokeTimer?.Stop();
        string? marker=Environment.GetEnvironmentVariable("SUPRA_SMOKE_RESULT");
        if(!string.IsNullOrWhiteSpace(marker))File.WriteAllText(marker,"FAIL "+error);
        Environment.ExitCode=1;
        _active=false;
        _finishing=false;
        Close();
    }

    private const string CaptureScript = """
(() => {
  if (window.__supraRecorderReady) return;
  window.__supraRecorderReady = true;
  const safe = value => String(value || '').replace(/\s+/g,' ').trim().slice(0,220);
  const scrub = value => safe(value)
    .replace(/[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}/g,'[email hidden]')
    .replace(/\b(?:\+?84|0)[0-9 .-]{8,13}\b/g,'[phone hidden]')
    .replace(/\b[A-Za-z0-9_-]{40,}\b/g,'[long-id hidden]')
    .replace(/(password|passwd|token|bearer|cookie|authorization)\s*[:=]\s*\S+/gi,'[credential hidden]');
  const hidden = el => !el || el.closest('input[type=password], [autocomplete="current-password"], [autocomplete="one-time-code"]');
  function selector(el) {
    if (!el || !el.tagName) return '';
    const parts=[];
    for(let p=el, i=0; p && p.tagName && i<4; p=p.parentElement,i++) {
      let name=p.tagName.toLowerCase();
      if(p.id && /^[a-zA-Z][-\w]{0,39}$/.test(p.id) && !/auth|token|secret|password|session/i.test(p.id)) {
        name += '#'+p.id; parts.unshift(name); break;
      }
      const cls=Array.from(p.classList || []).filter(x=>/^[a-zA-Z][-\w]{0,29}$/.test(x) && !/auth|token|secret/i.test(x)).slice(0,2);
      if(cls.length) name+='.'+cls.join('.');
      const sib=p.parentElement && Array.from(p.parentElement.children).filter(x=>x.tagName===p.tagName);
      if(sib && sib.length>1)name+=':nth-of-type('+(sib.indexOf(p)+1)+')';
      parts.unshift(name);
    }
    return parts.join(' > ').slice(0,220);
  }
  function describe(el,kind,key='') {
    if(!el || !el.tagName || hidden(el)) return;
    const target=el.closest('button,a,label,select,input,[role="button"],[role="menuitem"],[role="option"],[role="dialog"]') || el;
    if(hidden(target))return;
    const input=target.matches('input,textarea');
    const station=input && /^SDG\.[0-9]{1,4}$/i.test(target.value || '') ? target.value : '';
    const result={kind,tag:target.tagName.toLowerCase(),
      role:safe(target.getAttribute('role')),selector:selector(target),
      type:safe(target.getAttribute('type')),label:scrub(target.getAttribute('aria-label')||target.getAttribute('title')||target.getAttribute('placeholder')),
      text:input ? '' : scrub(target.innerText || target.textContent), key,station};
    send(result);
  }
  function send(data) {
    try { window.chrome?.webview?.postMessage(JSON.stringify(data)); } catch(_){}
  }
  const isInteresting=t=>/quầy đóng gói|mã quầy|thiết lập máy in|máy in|printer|xác nhận|đóng gói|tiếp theo|kiểm đếm/i.test(t);
  window.__supraRecorderSnapshot=function(reason) {
    const lines=[];
    document.querySelectorAll('h1,h2,h3,[role="dialog"],[role="alert"],button,[role="button"],label')
      .forEach(el=>{
        if(lines.length>=35 || hidden(el))return;
        const value=scrub(el.innerText||el.textContent);
        if(value && isInteresting(value)) lines.push(selector(el)+' :: '+value.slice(0,100));
      });
    send({kind:'snapshot',snapshot:('reason='+reason+' | '+lines.join(' | ')).slice(0,2200)});
  };
  document.addEventListener('pointerdown',e=>describe(e.target,'pointerdown'),true);
  document.addEventListener('click',e=>describe(e.target,'click'),true);
  document.addEventListener('keydown',e=>{
    if(['Enter','Escape','Tab','ArrowUp','ArrowDown'].includes(e.key))
      describe(e.target,'keydown',e.key);
  },true);
  document.addEventListener('change',e=>{
    if(e.target?.matches('select,input[type=radio],input[type=checkbox],input[type=text]'))
      describe(e.target,'change');
  },true);
  document.addEventListener('focusin',e=>{
    if(e.target?.matches('input,select,button'))describe(e.target,'focus');
  },true);
  addEventListener('beforeprint',()=>send({kind:'print_start'}));
  addEventListener('afterprint',()=>send({kind:'print_finish'}));
  let timeout;
  const observer=new MutationObserver(()=>{
    clearTimeout(timeout);
    timeout=setTimeout(()=>window.__supraRecorderSnapshot('DOM update'),750);
  });
  if(document.documentElement)observer.observe(document.documentElement,{childList:true,subtree:true});
  else document.addEventListener('DOMContentLoaded',()=>{
    observer.observe(document.documentElement,{childList:true,subtree:true});
  },{once:true});
  document.addEventListener('DOMContentLoaded',()=>window.__supraRecorderSnapshot('DOMContentLoaded'),{once:true});
})();
""";
}