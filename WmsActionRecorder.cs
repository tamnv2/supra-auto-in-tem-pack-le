using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace SupraAutoPackLe;

/// <summary>
/// Local, opt-in WMS DOM interaction recorder. Records diagnostic labels, selectors
/// and approved station/printer selections, never credentials, input values or traffic.
/// </summary>
internal sealed class WmsActionRecorder
{
    private static readonly JsonSerializerOptions Json=new()
    {
        Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly HashSet<string> Kinds=new(StringComparer.Ordinal)
    {
        "click","pointerdown","keydown","change","focus","snapshot","print_start","print_finish"
    };
    private readonly string _root;
    private Channel<string>? _channel;
    private Task? _writer;
    private string? _file;
    private DateTimeOffset _started;
    private int _count;
    private bool _active;
    private bool _exporting;
    public bool Active=>_active;
    public int Count=>_count;
    public WmsActionRecorder(string baseFolder)
    {
        _root=Path.Combine(baseFolder,"recordings");
    }
    private static string SafeText(string? text,int maximum=220)
    {
        if(string.IsNullOrWhiteSpace(text))return "";
        var s=Regex.Replace(text,@"\s+"," ").Trim();
        s=Regex.Replace(s,@"(?i)(password|passwd|bearer|token|cookie|authorization|secret|otp|mật khẩu)\s*[:=]?\s*\S+","[REDACTED]");
        s=Regex.Replace(s,@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b","[EMAIL]");
        s=Regex.Replace(s,@"\b(?:\+?84|0)[0-9.\s-]{8,13}\b","[PHONE]");
        s=Regex.Replace(s,@"\b(?:[A-Za-z0-9_-]{32,})\b","[IDENTIFIER]");
        // Form values are never copied to this field by the browser script.
        if(s.Length>maximum)s=s[..maximum];
        return s;
    }
    private static string SitePath(string? uri)
    {
        if(!Uri.TryCreate(uri,UriKind.Absolute,out var parsed))return "(unknown)";
        if(parsed.Scheme!="https" || !parsed.Host.Equals("wms-supra.winmart.vn",StringComparison.OrdinalIgnoreCase))
            return "(excluded)";
        // Never persist query, fragment or authorization-related URLs.
        var path=parsed.AbsolutePath;
        path=Regex.Replace(path,@"[A-Za-z0-9_-]{25,}",":id");
        return path.Length>180?path[..180]:path;
    }
    public static bool AllowedSource(string? uri)
    {
        if(!Uri.TryCreate(uri,UriKind.Absolute,out var parsed))return false;
        return parsed.Scheme=="https" &&
               parsed.Host.Equals("wms-supra.winmart.vn",StringComparison.OrdinalIgnoreCase) &&
               parsed.AbsolutePath.StartsWith("/sft3/app/",StringComparison.OrdinalIgnoreCase) &&
               !parsed.AbsolutePath.Contains("/login",StringComparison.OrdinalIgnoreCase);
    }
    private void Enqueue(string kind,object payload)
    {
        if(!_active || _channel is null)return;
        if(_count>=15000)return; // Bounded file size and CPU usage.
        var line=JsonSerializer.Serialize(new
        {
            time=DateTimeOffset.Now.ToString("O"),
            seq=++_count,kind,data=payload
        },Json);
        _channel.Writer.TryWrite(line);
    }
    public async Task StartAsync(string? currentUri)
    {
        if(_active||_exporting)throw new InvalidOperationException("Đang ghi hoặc xuất log.");
        if(!AllowedSource(currentUri))throw new InvalidOperationException(
            "Chỉ ghi thao tác trên WMS sau khi đăng nhập, không ghi màn hình đăng nhập.");
        Directory.CreateDirectory(_root);
        _file=Path.Combine(_root,"pending_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".jsonl");
        var stream=new FileStream(_file,FileMode.CreateNew,FileAccess.Write,FileShare.Read,32768,useAsync:true);
        _channel=Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader=true,SingleWriter=false
        });
        var channel=_channel;
        _writer=Task.Run(async()=>
        {
            await using var ownedStream=stream;
            await using var writer=new StreamWriter(ownedStream,new UTF8Encoding(false)) {AutoFlush=true};
            await foreach(var item in channel.Reader.ReadAllAsync())
                await writer.WriteLineAsync(item);
            await writer.FlushAsync();
        });
        _count=0;
        _started=DateTimeOffset.Now;
        _active=true;
        Enqueue("record_started",new {version=Application.ProductVersion,page=SitePath(currentUri),
            scope="WMS UI only; no passwords, tokens, cookies, HTTP bodies or free-text form values"});
        await Task.CompletedTask;
    }
    public void Navigation(string? currentUri,string status)
    {
        if(!_active)return;
        Enqueue("navigation",new {page=SitePath(currentUri),status=SafeText(status,32)});
    }
    public void Accept(string raw,string? source)
    {
        if(!_active||!AllowedSource(source))return;
        try
        {
            using var doc=JsonDocument.Parse(raw);
            if(doc.RootElement.ValueKind!=JsonValueKind.Object)return;
            var root=doc.RootElement;
            static string Get(JsonElement e,string key,int max=300)
            {
                if(!e.TryGetProperty(key,out var val)||val.ValueKind!=JsonValueKind.String)return "";
                var s=val.GetString()??"";
                return s.Length>max?s[..max]:s;
            }
            var kind=Get(root,"kind",32);
            if(!Kinds.Contains(kind))return;
            var selection=Get(root,"selection",90);
            if(!Regex.IsMatch(selection,@"^(?:SDG\.[0-9]{1,4}|[0-9]{2}-[0-9]{3,6}-[A-Z0-9]{2,8}\s*\((?:Online|Offline)\))$",RegexOptions.IgnoreCase))
                selection="";
            // Scrub again on the host: never trust JavaScript messages as already filtered.
            var text=SafeText(Get(root,"text"));
            var label=SafeText(Get(root,"label",150),150);
            var selector=SafeText(Get(root,"selector"),220);
            var snapshot=SafeText(Get(root,"snapshot",1900),1900);
            var key=Get(root,"key",32);
            if(key is not ("" or "Enter" or "Escape" or "Tab" or "ArrowDown" or "ArrowUp"))key="";
            Enqueue("web_"+kind,new
            {
                page=SitePath(source),tag=Get(root,"tag",30),role=Get(root,"role",40),
                elementType=Get(root,"type",35),selector,text,label,key,selection,snapshot
            });
        }
        catch(JsonException){ /* Do not dump untrusted messages. */ }
    }
    public async Task<string> StopAndExportAsync()
    {
        if(!_active||_channel is null||_file is null||_exporting)
            throw new InvalidOperationException("Không có phiên ghi đang chạy.");
        _exporting=true;
        try
        {
            Enqueue("record_finished",new {events=_count,durationSeconds=Math.Round((DateTimeOffset.Now-_started).TotalSeconds,1)});
            _active=false;
            _channel.Writer.TryComplete();
            if(_writer is not null) await _writer;
            var input=_file;
            var count=_count;
            var start=_started;
            return await Task.Run(()=>
            {
                var desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if(string.IsNullOrWhiteSpace(desktop))throw new IOException("Không tìm thấy Desktop.");
                Directory.CreateDirectory(desktop);
                var output=Path.Combine(desktop,"SUPRA_WMS_Actions_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".zip");
                using var zip=ZipFile.Open(output,ZipArchiveMode.Create);
                zip.CreateEntryFromFile(input,"events.jsonl",CompressionLevel.Optimal);
                var metadata=zip.CreateEntry("session.json",CompressionLevel.Optimal);
                using(var writer=new StreamWriter(metadata.Open(),new UTF8Encoding(false)))
                {
                    writer.Write(JsonSerializer.Serialize(new
                    {
                        version=Application.ProductVersion,started=start.ToString("O"),
                        finished=DateTimeOffset.Now.ToString("O"),events=count,
                        note="DOM actions on authenticated WMS only; review before sharing"
                    },Json));
                }
                return output;
            });
        }
        finally
        {
            _exporting=false;
            _channel=null;
            _writer=null;
            // Retain pending jsonl so failed exports can be recovered.
        }
    }
}
