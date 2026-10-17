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
internal sealed class BrowserSetupPeek
{
    public bool ok {get;set;}
    public string reason {get;set;}="";
    public bool boothDialog {get;set;}
    public bool boothInputReady {get;set;}
    public bool boothValueCorrect {get;set;}
    public bool boothConfirmEnabled {get;set;}
    public bool boothHeaderKnown {get;set;}
    public bool boothCorrect {get;set;}
    public bool printerDialog {get;set;}
    public bool packingReady {get;set;}
    public int accountTriggerCount {get;set;}
    public bool settingsVisible {get;set;}
    public bool printerCorrect {get;set;}
    public bool printerWrong {get;set;}
    public bool printerEmpty {get;set;}
    public bool printerOptionVisible {get;set;}
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
    private bool _startupRouting=true;
    private int _startupRedirects;
    private bool _clearingProfile;
    public event Action? LoginRequired;
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
        _startupRouting=false;
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
            core.NavigationCompleted+=(_,e)=>
            {
                _storage.Trace("WEB",e.IsSuccess?"NAVIGATION_DONE":"NAVIGATION_ERROR",
                    e.IsSuccess?"OK":"ERROR");
                if(!e.IsSuccess)return;
                if(!_clearingProfile && Uri.TryCreate(core.Source,UriKind.Absolute,out var destination) &&
                   (destination.Host.Equals("auth-supra.winmart.vn",StringComparison.OrdinalIgnoreCase) ||
                    (destination.Host.Equals("wms-supra.winmart.vn",StringComparison.OrdinalIgnoreCase) &&
                     destination.AbsolutePath.Contains("/login",StringComparison.OrdinalIgnoreCase))))
                    LoginRequired?.Invoke();
                TryNavigatePackingAfterLogin();
            };
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
    // Only redirect from a loaded, authenticated WMS application route, never from
    // the login host, password form or any non-WMS site. Bounded per startup.
    public void TryNavigatePackingAfterLogin()
    {
        if(!_startupRouting||_startupRedirects>=3)return;
        var core=_view.CoreWebView2;
        if(core==null || !Uri.TryCreate(core.Source,UriKind.Absolute,out var uri))return;
        if(!uri.Scheme.Equals("https",StringComparison.OrdinalIgnoreCase) ||
           !uri.Host.Equals("wms-supra.winmart.vn",StringComparison.OrdinalIgnoreCase) ||
           !uri.AbsolutePath.StartsWith("/sft3/app/",StringComparison.OrdinalIgnoreCase) ||
           uri.AbsolutePath.TrimEnd('/').Equals("/sft3/app/saleorder/packing",StringComparison.OrdinalIgnoreCase))
            return;
        _startupRedirects++;
        _storage.Trace("SETUP","RETURN_PACKING_ROUTE","OK");
        core.Navigate(Target);
    }
    /// <summary>
    /// Clears browser-managed site data including cookies, cache, stored passwords and
    /// autofill entries from this application's WebView2 profile only. Never touches
    /// operational history, employee records or diagnostic logs.
    /// </summary>
    public async Task ClearBrowsingDataAsync()
    {
        var core=_view.CoreWebView2;
        if(!Ready||core==null||_clearingProfile)
            throw new InvalidOperationException("WMS_BROWSER_NOT_READY");
        _clearingProfile=true;
        StopStartupRouting();
        try
        {
            // Supported by WebView2 v1.0.3351.48. No credential data is read by app code.
            await core.Profile.ClearBrowsingDataAsync();
            _storage.Trace("WEB","BROWSING_DATA_CLEARED","DONE");
            BeginStartupRouting();
            core.Navigate(Target);
        }
        catch
        {
            _storage.Trace("WEB","BROWSING_DATA_CLEAR_FAILED","ERROR");
            BeginStartupRouting();
            throw;
        }
        finally { _clearingProfile=false; }
    }
    public void StopStartupRouting()=>_startupRouting=false;
    public void BeginStartupRouting()
    {
        _startupRouting=true;
        _startupRedirects=0;
    }
    public async Task<BrowserSetupPeek?> SetupPeekAsync()
    {
        if(!Ready||_view.CoreWebView2==null)return null;
        try
        {
            var raw=await _view.CoreWebView2.ExecuteScriptAsync(
                "window.__supraPackLeV1?.setupPeek() || {ok:false,reason:'NO_SCRIPT'}");
            var result=JsonSerializer.Deserialize<BrowserSetupPeek>(raw);
            if(result?.reason=="NO_SCRIPT")
            {
                // Angular routing may reach packing before a new document's
                // script has finished registering. Install once on the live
                // packing document rather than leaving the operator waiting.
                if(Uri.TryCreate(_view.CoreWebView2.Source,UriKind.Absolute,out var uri) &&
                   uri.Host.Equals("wms-supra.winmart.vn",StringComparison.OrdinalIgnoreCase) &&
                   uri.AbsolutePath.TrimEnd('/').Equals("/sft3/app/saleorder/packing",
                       StringComparison.OrdinalIgnoreCase))
                {
                    await _view.CoreWebView2.ExecuteScriptAsync(WmsWorkflowJs.Script);
                    _storage.Trace("SETUP","SCRIPT_REINJECTED","DONE");
                    raw=await _view.CoreWebView2.ExecuteScriptAsync(
                        "window.__supraPackLeV1?.setupPeek() || {ok:false,reason:'NO_SCRIPT'}");
                    result=JsonSerializer.Deserialize<BrowserSetupPeek>(raw);
                }
            }
            return result;
        }
        catch
        {
            _storage.Trace("SETUP","SETUP_PEEK_FAILED","ERROR");
            return null;
        }
    }
    public async Task<BrowserResult> SetupActionAsync(string action)
    {
        if(!Ready||_view.CoreWebView2==null)return new(){reason="WEB_NOT_READY"};
        try
        {
            var script="window.__supraPackLeV1?.setupPerform("+
                JsonSerializer.Serialize(action)+") || {ok:false,reason:'NO_SCRIPT'}";
            var raw=await _view.CoreWebView2.ExecuteScriptAsync(script);
            return JsonSerializer.Deserialize<BrowserResult>(raw)??new(){reason="INVALID_SCRIPT_RESULT"};
        }
        catch { return new(){reason="SCRIPT_CALL_FAILED"}; }
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
