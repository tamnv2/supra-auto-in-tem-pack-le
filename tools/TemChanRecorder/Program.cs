using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SupraTemChanRecorder;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new RecorderForm());
    }
}

internal sealed class RecorderForm : Form
{
    private const string StartUrl = "https://wms-supra.winmart.vn/sft3/app/saleorder/list";
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Button _start = new() { Text = "BẮT ĐẦU GHI", Width = 162, Height = 40 };
    private readonly Button _stop = new() { Text = "KẾT THÚC · LƯU DESKTOP", Width = 230, Height = 40, Enabled = false };
    private readonly Label _status = new() { AutoSize = true, Text = "Đang khởi tạo trình duyệt WMS..." };
    private readonly Label _count = new() { AutoSize = true, Text = "0 sự kiện" };
    private readonly System.Windows.Forms.Timer _ticker = new() { Interval = 600 };
    private readonly ActionLog _log = new();
    private readonly string _profilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SupraTemChanRecorder", "WebView2-Profile");
    private bool _ready;
    private bool _busy;
    private bool _initialRoute = true;
    private int _routeAttempts;
    private string _script = "";
    private CoreWebView2? Core => _browser.CoreWebView2;

    public RecorderForm()
    {
        Text = "SUPRA | Ghi thao tác IN TEM CHẴN — BẢN TEST";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1320;
        Height = 850;
        MinimumSize = new Size(840, 540);
        Font = new Font("Segoe UI", 10);
        var top = new Panel { Height = 100, Dock = DockStyle.Top, Padding = new Padding(14, 9, 14, 8) };
        var title = new Label {
            Text = "GHI THAO TÁC IN TEM CHẴN · WEBVIEW2",
            Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };
        var controls = new FlowLayoutPanel {
            Dock = DockStyle.Top, Height = 50, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, AutoScroll = true
        };
        _start.Margin = new Padding(0, 3, 10, 3);
        _stop.Margin = new Padding(0, 3, 12, 3);
        _status.Margin = new Padding(0, 14, 12, 0);
        _count.Margin = new Padding(0, 14, 0, 0);
        controls.Controls.AddRange(new Control[] { _start, _stop, _status, _count });
        top.Controls.Add(controls);
        top.Controls.Add(title);
        Controls.Add(_browser);
        Controls.Add(top);

        _start.Click += async (_, _) => await StartRecordingAsync();
        _stop.Click += async (_, _) => await StopRecordingAsync();
        _ticker.Tick += (_, _) => _count.Text = _log.Count + " sự kiện";
        _ticker.Start();
        Shown += async (_, _) => await InitializeBrowserAsync();
        FormClosing += (_, e) => {
            if (_log.Active || _busy) { e.Cancel = true; MessageBox.Show("Bấm KẾT THÚC để lưu log trước khi đóng.", "Đang ghi thao tác", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            using var input = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SupraTemChanRecorder.dom-recorder.js")
                ?? throw new InvalidOperationException("Thiếu bộ nhận diện thao tác HTML.");
            using var reader = new StreamReader(input, Encoding.UTF8);
            _script = await reader.ReadToEndAsync();
            Directory.CreateDirectory(_profilePath);
            var env = await CoreWebView2Environment.CreateAsync(null, _profilePath);
            await _browser.EnsureCoreWebView2Async(env);
            var core = Core ?? throw new InvalidOperationException("Không tạo được WebView2.");
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            await core.AddScriptToExecuteOnDocumentCreatedAsync(_script);
            core.WebMessageReceived += (_, args) => {
                if (!_log.Active || !ActionLog.IsRecordable(args.Source)) return;
                try { _log.AppendWebEvent(args.TryGetWebMessageAsString(), args.Source); }
                catch { /* Never echo raw messages or HTML to UI/logs. */ }
            };
            core.NavigationStarting += (_, args) => {
                if (!ActionLog.IsSafeNavigation(args.Uri))
                {
                    args.Cancel = true;
                    _status.Text = "Đã chặn địa chỉ ngoài WMS/SSO.";
                }
                else if (_log.Active)
                {
                    _log.AppendNavigation("navigation_start", args.Uri);
                }
            };
            core.NavigationCompleted += async (_, args) => {
                if (_log.Active) _log.AppendNavigation(
                    args.IsSuccess ? "navigation_done" : "navigation_error", core.Source);
                if (args.IsSuccess)
                {
                    await ActivateOnPageAsync();
                    if (!_log.Active) TryReturnToList(core.Source);
                }
            };
            core.NewWindowRequested += (_, args) => {
                // Authenticated WMS/SSO popups are routed inside the recorder's single view.
                // No uninstrumented second browser or external site is opened.
                args.Handled = true;
                if (ActionLog.IsSafeNavigation(args.Uri))
                    core.Navigate(args.Uri);
            };
            core.ProcessFailed += (_, _) => {
                _ready = false;
                _status.Text = "Trình duyệt gặp lỗi; giữ log hiện có.";
            };
            _ready = true;
            _status.Text = "Đăng nhập WMS, mở danh sách SO rồi bấm Bắt đầu.";
            core.Navigate(StartUrl);
        }
        catch (Exception ex)
        {
            _status.Text = "Không khởi động được WebView2.";
            MessageBox.Show("Không thể mở WMS. Kiểm tra WebView2 Runtime và quyền máy tính.\n"
                + ex.Message, "Lỗi khởi tạo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TryReturnToList(string? address)
    {
        if (!_initialRoute || _routeAttempts >= 3 || !ActionLog.IsRecordable(address)) return;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return;
        if (uri.AbsolutePath.TrimEnd('/').Equals(
            "/sft3/app/saleorder/list", StringComparison.OrdinalIgnoreCase))
        {
            _initialRoute = false;
            return;
        }
        _routeAttempts++;
        Core?.Navigate(StartUrl);
    }

    private async Task ActivateOnPageAsync()
    {
        if (!_log.Active || Core is null || !ActionLog.IsRecordable(Core.Source)) return;
        try
        {
            // The script is also installed for same-document SPA navigation.
            await Core.ExecuteScriptAsync(_script);
            await Core.ExecuteScriptAsync("window.__supraTemChanRecorder?.setActive(true)");
        }
        catch { _status.Text = "Không gắn được recorder ở trang mới."; }
    }

    private async Task StartRecordingAsync()
    {
        if (!_ready || _busy || Core is null) return;
        if (!ActionLog.IsRecordable(Core.Source))
        {
            MessageBox.Show("Đăng nhập và vào trang WMS trước khi bấm Bắt đầu. "
                + "Không ghi nội dung đăng nhập.", "Chưa vào WMS", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        _busy = true;
        _start.Enabled = false;
        try
        {
            // Do not capture anything before the explicit user action.
            _initialRoute = false;
            await Core.ExecuteScriptAsync(_script);
            if (await Core.ExecuteScriptAsync("Boolean(window.__supraTemChanRecorder?.setActive)") != "true")
                throw new InvalidOperationException("Không cài được trình ghi sự kiện DOM.");
            _log.Start(Core.Source);
            await Core.ExecuteScriptAsync("window.__supraTemChanRecorder.setActive(true)");
            _stop.Enabled = true;
            _status.Text = "ĐANG GHI · thao tác trực tiếp trên WMS";
        }
        catch (Exception ex)
        {
            _log.AbortEmptySession();
            _start.Enabled = true;
            _status.Text = "Lỗi bắt đầu ghi.";
            MessageBox.Show(ex.Message, "Không thể ghi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _busy = false; }
    }

    private async Task StopRecordingAsync()
    {
        if (_busy || !_log.Active) return;
        _busy = true;
        _stop.Enabled = false;
        try
        {
            if (Core is not null && ActionLog.IsRecordable(Core.Source))
            {
                // Allow WebMessageReceived to deliver the final DOM snapshot first.
                await Core.ExecuteScriptAsync("window.__supraTemChanRecorder?.snapshot('finish')");
                await Task.Delay(180);
                await Core.ExecuteScriptAsync("window.__supraTemChanRecorder?.setActive(false)");
            }
            var count = _log.Count;
            var zip = _log.StopAndSaveDesktop();
            _status.Text = "Đã lưu " + count + " sự kiện tại Desktop.";
            _start.Enabled = true;
            MessageBox.Show("Đã xuất log:\n" + zip + "\n\nKiểm tra ZIP trước khi gửi.",
                "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Xuất log thất bại; dữ liệu tạm còn trong AppData.";
            MessageBox.Show("Lỗi lưu Desktop: " + ex.Message +
                "\nBản ghi tạm vẫn nằm trong %LOCALAPPDATA%\\SupraTemChanRecorder\\recordings.",
                "Lỗi xuất log", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _start.Enabled = !_log.Active;
            _stop.Enabled = _log.Active;
        }
        finally { _busy = false; }
    }
}

internal sealed class ActionLog
{
    private static readonly JsonSerializerOptions Json = new()
        { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
        { "click", "pointerdown", "keydown", "focus", "change", "submit",
          "snapshot", "dialog", "route", "beforeprint", "afterprint" };
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
        { "Enter", "Tab", "Escape", "ArrowUp", "ArrowDown", "Space" };
    private static readonly string[] Fields =
        { "tag", "role", "type", "id", "name", "title", "ariaLabel",
          "placeholder", "formControlName", "testId", "text", "label",
          "selector", "dialog", "reason", "cellHeader" };
    private StreamWriter? _writer;
    private string? _pendingPath;
    private DateTimeOffset _started;
    private int _count;
    public int Count => _count;
    public bool Active => _writer is not null;

    public static bool IsSafeNavigation(string? input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps) return false;
        return uri.Host.Equals("wms-supra.winmart.vn", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("auth-supra.winmart.vn", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRecordable(string? input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps) return false;
        return uri.Host.Equals("wms-supra.winmart.vn", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/sft3/app/", StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static string Page(string? input)
    {
        if (!IsRecordable(input) || !Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return "(excluded)";
        return Regex.Replace(uri.AbsolutePath, @"[A-Za-z0-9_-]{24,}", ":id");
    }

    private static string Safe(string? input, int limit = 140)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = Regex.Replace(input, @"\s+", " ").Trim();
        if (Regex.IsMatch(s, @"(?i)password|passwd|bearer|token|cookie|authorization|secret|session|otp|mật khẩu|đăng nhập"))
            return "[SENSITIVE]";
        s = Regex.Replace(s, @"(?i)[\w.+-]+@[\w.-]+\.[a-z]{2,}", "[EMAIL]");
        s = Regex.Replace(s, @"(?<!\d)(?:\+?84|0)[\d.\s-]{8,13}(?!\d)", "[PHONE]");
        s = Regex.Replace(s, @"\b\d{7,}\b", "[NUMBER]");
        s = Regex.Replace(s, @"\b[A-Za-z0-9_-]{28,}\b", "[IDENTIFIER]");
        s = Regex.Replace(s, @"(?i)\beyJ[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+){1,2}", "[TOKEN]");
        return s.Length <= limit ? s : s[..limit];
    }

    private void Write(string kind, object data)
    {
        if (_writer is null || _count >= 20000) return;
        _writer.WriteLine(JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.Now.ToString("O"), seq = ++_count, kind, data
        }, Json));
        _writer.Flush();
    }

    public void Start(string? uri)
    {
        if (Active || !IsRecordable(uri))
            throw new InvalidOperationException("Chỉ ghi trên trang WMS đã đăng nhập.");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupraTemChanRecorder", "recordings");
        Directory.CreateDirectory(folder);
        _pendingPath = Path.Combine(folder,
            "pending_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".jsonl");
        _writer = new StreamWriter(new FileStream(_pendingPath, FileMode.CreateNew,
            FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        _count = 0;
        _started = DateTimeOffset.Now;
        Write("record_started", new { page = Page(uri), recorder = "TemChan-0.1",
            privacy = "DOM metadata only, no typed values, password, cookies, tokens, HTTP content or coordinates" });
    }

    public void AbortEmptySession()
    {
        if (_writer is null) return;
        _writer.Dispose();
        _writer = null;
        if (_count <= 1 && _pendingPath is not null && File.Exists(_pendingPath))
            File.Delete(_pendingPath);
    }

    public void AppendNavigation(string kind, string? uri)
    {
        if (Active) Write(kind, new { page = Page(uri) });
    }

    public void AppendWebEvent(string raw, string? source)
    {
        if (!Active || !IsRecordable(source) || raw.Length > 24000) return;
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("kind", out var k) || k.ValueKind != JsonValueKind.String)
            return;
        var kind = k.GetString() ?? "";
        if (!Kinds.Contains(kind)) return;
        var data = new Dictionary<string, object?> { ["page"] = Page(source) };
        foreach (var field in Fields)
        {
            if (root.TryGetProperty(field, out var property) &&
                property.ValueKind == JsonValueKind.String)
                data[field] = Safe(property.GetString(), field == "selector" ? 350 : 160);
        }
        if (root.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String)
        {
            var value = key.GetString() ?? "";
            if (Keys.Contains(value)) data["key"] = value;
        }
        foreach (var field in new[] { "unique", "disabled", "visible" })
        {
            if (root.TryGetProperty(field, out var prop) &&
                prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
                data[field] = prop.GetBoolean();
        }
        if (root.TryGetProperty("controls", out var controls) &&
            controls.ValueKind == JsonValueKind.Array)
        {
            var sanitized = new List<object>();
            foreach (var item in controls.EnumerateArray().Take(30))
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var record = new Dictionary<string, string>();
                foreach (var field in Fields)
                {
                    if (item.TryGetProperty(field, out var prop) &&
                        prop.ValueKind == JsonValueKind.String)
                        record[field] = Safe(prop.GetString());
                }
                sanitized.Add(record);
            }
            data["controls"] = sanitized;
        }
        Write("dom_" + kind, data);
    }

    public string StopAndSaveDesktop()
    {
        if (_writer is null || _pendingPath is null)
            throw new InvalidOperationException("Không có phiên ghi.");
        Write("record_finished", new {
            count = _count, seconds = Math.Round((DateTimeOffset.Now - _started).TotalSeconds, 1)
        });
        _writer.Dispose();
        _writer = null;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            throw new IOException("Không tìm thấy Desktop Windows.");
        var target = Path.Combine(desktop,
            "SUPRA_InTemChan_DOM_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".zip");
        using (var zip = ZipFile.Open(target, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(_pendingPath, "events.jsonl", CompressionLevel.Optimal);
            var metadata = zip.CreateEntry("README.txt", CompressionLevel.Optimal);
            using var writer = new StreamWriter(metadata.Open(), new UTF8Encoding(false));
            writer.Write("SUPRA in tem chan - DOM record TEST\n" +
                "Only browser UI action metadata. No typed field values, passwords, tokens, " +
                "cookies, request/response bodies, screenshots, browser profile or coordinates.\n" +
                "Review the archive before sending; real Windows/WMS field verification is required.\n");
        }
        // Minimize local retention after a successful ZIP export.
        File.Delete(_pendingPath);
        _pendingPath = null;
        return target;
    }
}
