using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SupraAutoPackLe;

internal static class UpdateClient
{
    private const string Repo="tamnv2/supra-auto-in-tem-pack-le";
    private const string Api="https://api.github.com/repos/"+Repo+"/releases/latest";
    private const string Asset="SupraAutoPackLe-win-x64.zip";
    private static readonly HttpClient Http=new(new HttpClientHandler{AllowAutoRedirect=true})
    {
        Timeout=TimeSpan.FromSeconds(45)
    };
    static UpdateClient()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SUPRA-Auto-Pack-Le/1.0");
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }
    public static async Task CheckAndInstallAsync(Form owner,LocalStore store)
    {
        store.Trace("UPDATE","CHECK_START","OK");
        try
        {
            using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(50));
            var response=await Http.GetAsync(Api,cancellation.Token);
            response.EnsureSuccessStatusCode();
            using var data=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation.Token));
            var release=data.RootElement;
            var tag=release.GetProperty("tag_name").GetString()??"";
            if(!tag.StartsWith('v')||!Version.TryParse(tag[1..],out var remote))
                throw new InvalidOperationException("INVALID_VERSION");
            var installed=typeof(UpdateClient).Assembly.GetName().Version??new Version(1,0,0);
            if(remote<=installed)
            {
                store.Trace("UPDATE","ALREADY_CURRENT","DONE");
                MessageBox.Show(owner,"Đang sử dụng bản mới nhất: "+installed,"Cập nhật");
                return;
            }
            var assets=release.GetProperty("assets").EnumerateArray().ToArray();
            static string UrlFor(JsonElement[] arr,string name)
            {
                foreach(var asset in arr)
                {
                    if(asset.GetProperty("name").GetString()==name)
                        return asset.GetProperty("browser_download_url").GetString()??"";
                }
                return "";
            }
            var package=UrlFor(assets,Asset);
            var sumUrl=UrlFor(assets,Asset+".sha256");
            if(!SafeAssetUrl(package,tag)||!SafeAssetUrl(sumUrl,tag))
                throw new InvalidOperationException("UNTRUSTED_RELEASE_ASSET");
            if(MessageBox.Show(owner,"Có bản "+remote+". Tải và cập nhật khi ứng dụng đang rảnh?\n\n"+
                "Quá trình này đóng ứng dụng và khởi động lại, dữ liệu local vẫn được giữ.",
                "Xác nhận cập nhật",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            store.Trace("UPDATE","DOWNLOAD_START","OK");
            var checksum=(await Http.GetStringAsync(sumUrl,cancellation.Token)).Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries)[0];
            if(!Regex.IsMatch(checksum,"^[a-fA-F0-9]{64}$"))
                throw new InvalidOperationException("INVALID_HASH_FILE");
            var folder=Path.Combine(store.Root,"update-cache",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var zip=Path.Combine(folder,Asset);
            using(var result=await Http.GetAsync(package,HttpCompletionOption.ResponseHeadersRead,cancellation.Token))
            {
                result.EnsureSuccessStatusCode();
                if(result.Content.Headers.ContentLength>150_000_000)
                    throw new InvalidOperationException("PACKAGE_TOO_LARGE");
                await using var stream=await result.Content.ReadAsStreamAsync(cancellation.Token);
                await using var dest=File.Create(zip);
                var buffer=new byte[65536];long bytes=0;int n;
                while((n=await stream.ReadAsync(buffer,cancellation.Token))>0)
                {
                    bytes+=n;if(bytes>150_000_000)throw new InvalidOperationException("PACKAGE_TOO_LARGE");
                    await dest.WriteAsync(buffer.AsMemory(0,n),cancellation.Token);
                }
            }
            var actual=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(zip,cancellation.Token)));
            if(!actual.Equals(checksum,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("CHECKSUM_MISMATCH");
            var extract=Path.Combine(folder,"extracted");
            ZipFile.ExtractToDirectory(zip,extract);
            var exe=Path.Combine(extract,"SupraAutoPackLe.exe");
            if(!File.Exists(exe))throw new InvalidOperationException("EXPECTED_EXE_MISSING");
            store.Trace("UPDATE","CHECKSUM_PASS","DONE");
            var appExe=Environment.ProcessPath;
            var installDir=Path.GetDirectoryName(appExe);
            if(appExe==null||installDir==null)throw new InvalidOperationException("INSTALL_DIR_UNKNOWN");
            var script=Path.Combine(folder,"apply-update.ps1");
            static string Escape(string s)=>s.Replace("'","''");
            // Runs only after app exit. Never touches profile/session or LocalStore directories.
            var body="$ErrorActionPreference = 'Stop'\n"+
               "$source = '"+Escape(extract)+"'\n"+
               "$target = '"+Escape(installDir)+"'\n"+
               "$exe = '"+Escape(appExe)+"'\n"+
               "$pidToWait = "+Environment.ProcessId+"\n"+
               "try { Wait-Process -Id $pidToWait -Timeout 40 -ErrorAction SilentlyContinue } catch {}\n"+
               "Start-Sleep -Seconds 1\n"+
               "try {\n"+
               "  Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force -ErrorAction Stop\n"+
               "  Start-Process -FilePath $exe\n"+
               "} catch {\n"+
               "  Add-Type -AssemblyName PresentationFramework\n"+
               "  [System.Windows.MessageBox]::Show('Không thể cài bản cập nhật. Hãy tải ZIP thủ công từ Releases.')\n"+
               "}\n";
            File.WriteAllText(script,body);
            var psi=new ProcessStartInfo("powershell.exe"){
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(script);
            using var process=Process.Start(psi);
            if(process==null)throw new InvalidOperationException("UPDATER_PROCESS_FAILED");
            store.Trace("UPDATE","APPLY_STARTED","DONE");
            Application.Exit();
        }
        catch
        {
            store.Trace("UPDATE","UPDATE_UNAVAILABLE","ERROR");
            MessageBox.Show(owner,"Chưa thể cập nhật. Có thể mạng Office không truy cập được GitHub, "+
              "hoặc gói phát hành không hợp lệ. Bản đang chạy không bị thay đổi.\n"+
              "Có thể tải bản mới thủ công tại GitHub Releases.","Cập nhật",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    private static bool SafeAssetUrl(string text,string tag)
    {
        if(!Uri.TryCreate(text,UriKind.Absolute,out var uri)||uri.Scheme!="https"||
           !uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase))return false;
        return uri.AbsolutePath.StartsWith("/"+Repo+"/releases/download/"+tag+"/",
            StringComparison.Ordinal);
    }
}
