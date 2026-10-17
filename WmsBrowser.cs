using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SupraAutoPackLe;

internal sealed class BrowserPeek
{
    public bool ok {get;set;}
    public string reason {get;set;}="";
    public bool ready {get;set;}
    public int toteCount {get;set;}
    public int packCount {get;set;}
    public int qtyCount {get;set;}
    public int printCount {get;set;}
    public int finishCount {get;set;}
    public int updateCount {get;set;}
    public int yesCount {get;set;}
    public int noCount {get;set;}
    public bool outboundSlipPrompt {get;set;}
    public int printLabelCount {get;set;}
    public int dialogCount {get;set;}
    public string so {get;set;}="";
    [JsonPropertyName("do")] public string Do {get;set;}="";
}
internal sealed class BrowserResult
{
    public bool ok {get;set;}
    public string reason {get;set;}="";
}

internal sealed class WmsBrowser : UserControl
{
    private const string Target="https://wms-supra.winmart.vn/sft3/app/saleorder/packing";
    private readonly WebView2 _view=new(){Dock=DockStyle.Fill};
    private readonly LocalStore _storage;
    private readonly string _profile;
    private readonly Action _openBrowserTab;
    public bool Ready {get;private set;}
    public WmsBrowser(LocalStore storage,Action openBrowserTab)
    {
        _storage=storage;
        _openBrowserTab=openBrowserTab;
        _profile=Path.Combine(storage.Root,"webview2-profile");
        Dock=DockStyle.Fill;
        Controls.Add(_view);
    }
    public void Reveal()
    {
        if(!IsDisposed)_openBrowserTab();
    }
    public void Terminate()
    {
        Ready=false;
        // A persistent browser profile retains site sessions and passwords by user choice.
        // This application never reads, logs or exports the profile contents.
        _view.Dispose();
    }
    public async Task InitializeAsync()
    {
        if(Ready)return;
        try
        {
            Directory.CreateDirectory(_profile);
            var env=await CoreWebView2Environment.CreateAsync(null,_profile);
            var settings=env.CreateCoreWebView2ControllerOptions();
            settings.IsInPrivateModeEnabled=false;
            await _view.EnsureCoreWebView2Async(env,settings);
            var core=_view.CoreWebView2;
            core.Settings.IsWebMessageEnabled=false;
            core.Settings.AreDevToolsEnabled=false;
            core.Settings.AreDefaultContextMenusEnabled=false;
            core.Settings.IsPasswordAutosaveEnabled=true;
            // Password manager is separate from saving names, addresses and other form data.
            core.Settings.IsGeneralAutofillEnabled=false;
            await core.AddScriptToExecuteOnDocumentCreatedAsync(WmsWorkflowJs.Script);
            core.NavigationStarting+=(_,e)=>{
                if(!AllowedUrl(e.Uri))
                {
                    e.Cancel=true;
                    _storage.Trace("WEB","URL_BLOCKED","BLOCKED");
                }
            };
            core.NavigationCompleted+=(_,e)=>_storage.Trace("WEB",
                e.IsSuccess?"NAVIGATION_DONE":"NAVIGATION_ERROR",e.IsSuccess?"OK":"ERROR");
            core.ProcessFailed+=(_,_)=>{Ready=false;_storage.Trace("WEB","PROCESS_FAILED","ERROR");};
            core.NewWindowRequested+=(_,e)=>{
                // Do not allow uncontrolled popup navigation to external endpoints.
                e.Handled=true;
                _storage.Trace("WEB","POPUP_BLOCKED","NEEDS_HUMAN");
            };
            Ready=true;
            core.Navigate(Target);
        }
        catch
        {
            _storage.Trace("WEB","INIT_FAILED","ERROR");
            throw new InvalidOperationException("WebView2 không khởi tạo được. Kiểm tra WebView2 Runtime.");
        }
    }
    private static bool AllowedUrl(string text)
    {
        if(!Uri.TryCreate(text,UriKind.Absolute,out var uri))return false;
        if(uri.Scheme!="https")return false;
        return uri.Host.Equals("wms-supra.winmart.vn",StringComparison.OrdinalIgnoreCase)||
               uri.Host.Equals("auth-supra.winmart.vn",StringComparison.OrdinalIgnoreCase);
    }
    public async Task<BrowserPeek?> PeekAsync()
    {
        if(!Ready||_view.CoreWebView2==null)return null;
        try
        {
            var raw=await _view.CoreWebView2.ExecuteScriptAsync(
                "window.__supraPackLeV1?.peek() || {ok:false,reason:'NO_SCRIPT'}");
            return JsonSerializer.Deserialize<BrowserPeek>(raw);
        }
        catch{return null;}
    }
    public async Task<BrowserResult> PerformAsync(string action,string? value=null)
    {
        if(!Ready||_view.CoreWebView2==null)return new(){reason="WEB_NOT_READY"};
        try
        {
            var script="window.__supraPackLeV1?.perform("+
                JsonSerializer.Serialize(action)+","+JsonSerializer.Serialize(value??"")+
                ") || {ok:false,reason:'NO_SCRIPT'}";
            var raw=await _view.CoreWebView2.ExecuteScriptAsync(script);
            return JsonSerializer.Deserialize<BrowserResult>(raw)??new(){reason="INVALID_SCRIPT_RESULT"};
        }
        catch{return new(){reason="SCRIPT_CALL_FAILED"};}
    }
    public async Task EnterAsync()
    {
        if(!Ready||_view.CoreWebView2==null)throw new InvalidOperationException("WMS chưa sẵn sàng.");
        // CDP Input only to model a real scanner's Enter key, never any CDP Network operations.
        var core=_view.CoreWebView2;
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
            "{\"type\":\"rawKeyDown\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13}");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
            "{\"type\":\"char\",\"text\":\"\\r\",\"windowsVirtualKeyCode\":13}");
        await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
            "{\"type\":\"keyUp\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13}");
    }
}
