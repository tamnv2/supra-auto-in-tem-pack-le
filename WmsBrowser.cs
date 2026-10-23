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

internal sealed class TemChanPeek
{
    public bool ok {get;set;}
    public string reason {get;set;}="";
    public bool statusKnown {get;set;}
    public bool statusValid {get;set;}
    public bool tabReady {get;set;}
    public bool tabSelected {get;set;}
    public int printCount {get;set;}
    public bool dialogOpen {get;set;}
    public bool rangeValid {get;set;}
    public string dialogReason {get;set;}="";
    public int total {get;set;}
    public int begin {get;set;}
    public int finish {get;set;}
}

internal sealed class WmsBrowser : UserControl
{
    private const string Target="https://wms-supra.winmart.vn/sft3/app/saleorder/packing";
    private const string TemChanList="https://wms-supra.winmart.vn/sft3/app/saleorder/list";
    private bool _temChanMode;
    public void SetOperationMode(bool temChan)
    {
        _temChanMode=temChan;
        _startupRouting=!temChan;
        _startupRedirects=0;
        if(Ready && _view.CoreWebView2 is not null)
            _view.CoreWebView2.Navigate(temChan ? TemChanList : Target);
    }
    public bool NavigateTemChan(string so)
    {
        if(!_temChanMode||!Ready||_view.CoreWebView2 is null ||
           !System.Text.RegularExpressions.Regex.IsMatch(so,@"^[A-Z0-9_-]{5,50}$"))return false;
        _view.CoreWebView2.Navigate(
            "https://wms-supra.winmart.vn/sft3/app/saleorder/details/"+
            Uri.EscapeDataString(so)+"?PickType=PickOrder");
        return true;
    }
    public async Task<TemChanPeek?> TemChanPeekAsync(string so)
    {
        if(!_temChanMode||!Ready||_view.CoreWebView2 is null)return null;
        try
        {
            var core=_view.CoreWebView2;
            await core.ExecuteScriptAsync(WmsTemChanJs.Script);
            var result=await core.ExecuteScriptAsync(
                "window.__supraTemChanV1?.peek("+JsonSerializer.Serialize(so)+
                ") || {ok:false,reason:'SCRIPT_MISSING'}");
            return JsonSerializer.Deserialize<TemChanPeek>(result);
        }
        catch{return null;}
    }
    public async Task<BrowserResult> TemChanPerformAsync(string action,string so)
    {
        if(!_temChanMode||!Ready||_view.CoreWebView2 is null)
            return new(){reason="WMS_NOT_READY"};
        try
        {
            var core=_view.CoreWebView2;
            await core.ExecuteScriptAsync(WmsTemChanJs.Script);
            var result=await core.ExecuteScriptAsync(
                "window.__supraTemChanV1?.perform("+JsonSerializer.Serialize(action)+","+
                JsonSerializer.Serialize(so)+") || {ok:false,reason:'SCRIPT_MISSING'}");
            return JsonSerializer.Deserialize<BrowserResult>(result)??new(){reason="INVALID_RESULT"};
        }
        catch{return new(){reason="ACTION_DISPATCH_UNCERTAIN"};}
    }
    private readonly WebView2 _view=new(){Dock=DockStyle.Fill};
    private readonly LocalStore _storage;
    private readonly string _profile;
    private readonly Action _openBrowserTab;
    private readonly WmsActionRecorder _recorder;
    public bool Recording=>_recorder.Active;
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
        _recorder=new WmsActionRecorder(storage.Root);
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
            // The separate, user-started recorder listens to filtered DOM events.
            // No HTTP payloads, cookie reads or DevTools network capture.
            core.Settings.IsWebMessageEnabled=true;
            core.Settings.AreDevToolsEnabled=false;
            core.Settings.AreDefaultContextMenusEnabled=false;
            core.Settings.IsPasswordAutosaveEnabled=true;
            // Password manager is separate from saving names, addresses and other form data.
            core.Settings.IsGeneralAutofillEnabled=false;
            await core.AddScriptToExecuteOnDocumentCreatedAsync(WmsWorkflowJs.Script);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(WmsRecorderJs.Script);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(WmsTemChanJs.Script);
            core.WebMessageReceived+=(_,e)=>
            {
                if(!_recorder.Active)return;
                try{_recorder.Accept(e.TryGetWebMessageAsString(),e.Source);}
                catch{_storage.Trace("RECORDER","INVALID_MESSAGE","ERROR");}
            };
            core.NavigationStarting+=(_,e)=>{
                if(!AllowedUrl(e.Uri))
                {
                    e.Cancel=true;
                    _storage.Trace("WEB","URL_BLOCKED","BLOCKED");
                }
            };
            core.NavigationCompleted+=async (_,e)=>
            {
                _storage.Trace("WEB",e.IsSuccess?"NAVIGATION_DONE":"NAVIGATION_ERROR",
                    e.IsSuccess?"OK":"ERROR");
                if(_recorder.Active)
                {
                    _recorder.Navigation(core.Source,e.IsSuccess?"LOADED":"ERROR");
                    if(e.IsSuccess)
                    {
                        try
                        {
                            await core.ExecuteScriptAsync(WmsRecorderJs.Script);
                            await core.ExecuteScriptAsync(
                                "window.__supraActionsV17?.setActive(true)");
                        }
                        catch{_storage.Trace("RECORDER","NAV_REATTACH_FAILED","ERROR");}
                    }
                }
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
            // Clear site sessions from both WMS and auth origins in THIS application's
            // WebView2 profile before first network navigation. This intentionally
            // preserves PasswordAutosave, GeneralAutofill, browser history and settings.
            // Fail closed: never open WMS using an existing session if clearing fails.
            await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite);
            _storage.Trace("WEB","STARTUP_SITE_SESSION_CLEARED","DONE");
            Ready=true;
            core.Navigate(_temChanMode ? TemChanList : Target);
        }
        catch(Exception e)
        {
            _storage.Trace("WEB","INIT_FAILED","ERROR");
            throw new InvalidOperationException("Không khởi tạo WMS hoặc không xóa được phiên cũ. "+
                "Trình duyệt chưa mở để tránh dùng lại phiên đăng nhập. "+e.Message,e);
        }
    }
    public async Task BeginRecordingAsync()
    {
        if(!Ready||_view.CoreWebView2==null)
            throw new InvalidOperationException("Trình duyệt WMS chưa sẵn sàng.");
        var core=_view.CoreWebView2;
        if(!WmsActionRecorder.AllowedSource(core.Source))
            throw new InvalidOperationException("Đăng nhập WMS trước khi bật ghi thao tác.");
        await _recorder.StartAsync(core.Source);
        try
        {
            await core.ExecuteScriptAsync(WmsRecorderJs.Script);
            var result=await core.ExecuteScriptAsync("!!window.__supraActionsV17");
            if(result!="true")throw new InvalidOperationException("DOM recorder chưa được cài vào trang.");
            await core.ExecuteScriptAsync("window.__supraActionsV17.setActive(true)");
            _storage.Trace("RECORDER","START","DONE");
        }
        catch
        {
            // Preserve partial recording, stop and export whenever possible.
            try{await _recorder.StopAndExportAsync();}catch{}
            _storage.Trace("RECORDER","START","ERROR");
            throw;
        }
    }
    public async Task<string> EndRecordingAsync()
    {
        if(!_recorder.Active)
            throw new InvalidOperationException("Chưa bắt đầu ghi thao tác.");
        var core=_view.CoreWebView2;
        if(core is not null)
        {
            try
            {
                await core.ExecuteScriptAsync("window.__supraActionsV17?.snapshot('Finish recording')");
                await Task.Delay(130);
                await core.ExecuteScriptAsync("window.__supraActionsV17?.setActive(false)");
            }
            catch{_storage.Trace("RECORDER","FINAL_SNAPSHOT_UNAVAILABLE","ERROR");}
        }
        var file=await _recorder.StopAndExportAsync();
        _storage.Trace("RECORDER","EXPORTED","DONE",0,"EVENTS="+_recorder.Count);
        return file;
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
        if(_temChanMode||!_startupRouting||_startupRedirects>=3)return;
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
        _startupRouting=!_temChanMode;
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
