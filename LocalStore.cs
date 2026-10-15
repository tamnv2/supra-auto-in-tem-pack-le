using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace SupraAutoPackLe;

internal sealed record HistoryRow(string Date, string Start, string End, long Seconds, string Person, string SO, string DO, string Result);
internal sealed record DiagRow(string At, string Version, string Stage, string Event, string Outcome, long ElapsedMs, string Detail);

internal sealed class LocalStore
{
    public readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupraAutoPackLe");
    private string PeoplePath => Path.Combine(Root,"people.json");
    private string HistoryPath => Path.Combine(Root,"history.jsonl");
    private string Logs => Path.Combine(Root,"logs");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    public LocalStore() { Directory.CreateDirectory(Root); Directory.CreateDirectory(Logs); }

    public List<string> LoadPeople()
    {
        try { return File.Exists(PeoplePath)
          ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(PeoplePath)) ?? [])
             .Where(ValidName).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : []; }
        catch { return []; }
    }
    public static bool ValidName(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 70 &&
       !s.Contains('\r') && !s.Contains('\n') && s.All(c => !char.IsControl(c));
    public void SavePeople(IEnumerable<string> names)
    {
        var sanitized=names.Select(x=>x.Trim()).Where(ValidName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var tmp=PeoplePath+".tmp";
        File.WriteAllText(tmp,JsonSerializer.Serialize(sanitized,Json),new UTF8Encoding(false));
        File.Move(tmp,PeoplePath,true);
    }
    public IReadOnlyList<HistoryRow> History()
    {
        var rows=new List<HistoryRow>();
        try { if(File.Exists(HistoryPath)) foreach(var line in File.ReadLines(HistoryPath))
              try { var r=JsonSerializer.Deserialize<HistoryRow>(line);if(r!=null) rows.Add(r); } catch {} }
        catch {}
        return rows;
    }
    public void AppendHistory(HistoryRow row)
    {
        File.AppendAllText(HistoryPath,JsonSerializer.Serialize(row,Json)+"\n",new UTF8Encoding(false));
    }
    private static readonly Regex Code = new("^[A-Z0-9_]{1,60}$",RegexOptions.Compiled);
    private static readonly Regex DetailCode = new("^[A-Z0-9_=,;|.:-]{0,220}$",RegexOptions.Compiled);
    public void Trace(string stage,string evt,string outcome,long elapsed=0,string detail="")
    {
        stage=Code.IsMatch(stage)?stage:"UNSPECIFIED";
        evt=Code.IsMatch(evt)?evt:"UNSPECIFIED";
        outcome=Code.IsMatch(outcome)?outcome:"UNKNOWN";
        detail=DetailCode.IsMatch(detail)?detail:"REDACTED";
        try
        {
            var f=Path.Combine(Logs,DateTime.Now.ToString("yyyy-MM-dd")+".jsonl");
            if(File.Exists(f)&&new FileInfo(f).Length>20_000_000) return;
            File.AppendAllText(f,JsonSerializer.Serialize(new DiagRow(
                DateTimeOffset.Now.ToString("O"),Application.ProductVersion,stage,evt,outcome,
                Math.Max(0,elapsed),detail),Json)+"\n",new UTF8Encoding(false));
        }
        catch {}
    }
    public void ExportLogs(string zip,DateTime? from,DateTime? to)
    {
        using var output=ZipFile.Open(zip,ZipArchiveMode.Create);
        foreach(var file in Directory.GetFiles(Logs,"????-??-??.jsonl"))
        {
            var dateText=Path.GetFileNameWithoutExtension(file);
            if(!DateTime.TryParseExact(dateText,"yyyy-MM-dd",null,System.Globalization.DateTimeStyles.None,out var date))continue;
            if(from.HasValue && date.Date<from.Value.Date)continue;
            if(to.HasValue && date.Date>to.Value.Date)continue;
            output.CreateEntryFromFile(file,"logs/"+Path.GetFileName(file),CompressionLevel.Optimal);
        }
        using var writer=new StreamWriter(output.CreateEntry("README.txt").Open(),Encoding.UTF8);
        writer.Write("Chỉ log kỹ thuật allowlist. Không chứa Tote, SO/DO, nhân sự, mật khẩu, token, cookie hay HTTP header.");
    }
    public void ExportHistoryXlsx(string filename)
    {
        using var wb=new XLWorkbook();
        var ws=wb.AddWorksheet("Lịch sử in tem");
        string[] header=["Ngày","Bắt đầu","Kết thúc","Thời gian (giây)","Nhân sự","Mã SO","Mã DO","Kết quả"];
        for(var i=0;i<header.Length;i++)ws.Cell(1,i+1).Value=header[i];
        var index=2;
        foreach(var row in History())
        {
            ws.Cell(index,1).Value=SafeExcel(row.Date);
            ws.Cell(index,2).Value=SafeExcel(row.Start);
            ws.Cell(index,3).Value=SafeExcel(row.End);
            ws.Cell(index,4).Value=row.Seconds;
            ws.Cell(index,5).Value=SafeExcel(row.Person);
            ws.Cell(index,6).Value=SafeExcel(row.SO);
            ws.Cell(index,7).Value=SafeExcel(row.DO);
            ws.Cell(index,8).Value=SafeExcel(row.Result);
            index++;
        }
        ws.Range(1,1,1,header.Length).Style.Font.Bold=true;
        ws.Range(1,1,1,header.Length).Style.Fill.BackgroundColor=XLColor.LightBlue;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        if(index>2)ws.Range(1,1,index-1,header.Length).SetAutoFilter();
        wb.SaveAs(filename);
    }
    private static string SafeExcel(string? value)
    {
        value??="";
        // Prevent formula interpretation by Excel, even if data came from WMS.
        if(value.Length>0 && "=+-@\t\r".Contains(value[0])) value="'"+value;
        return new string(value.Where(c=>!char.IsControl(c)).Take(160).ToArray());
    }
}
