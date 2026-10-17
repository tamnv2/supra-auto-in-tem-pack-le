using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using ClosedXML.Excel;

namespace SupraAutoPackLe;

internal sealed record HistoryRow(string Date, string Start, string End, long Seconds, string Person, string SO, string DO, string Result)
{
    // An attempt is written at multiple checkpoints; older JSONL entries omit both fields.
    public string AttemptId { get; init; } = "";
    public string Phase { get; init; } = "";
    // Nullable because earlier releases and future WMS-derived fields lack this data.
    public int? SkuCount { get; init; }
    public int? ParcelCount { get; init; }
    public int? Pcs { get; init; }
    public decimal? Points { get; init; }
}
internal sealed record DiagRow(string At, string Version, string Stage, string Event, string Outcome, long ElapsedMs, string Detail);

internal sealed class LocalStore
{
    public readonly string Root;
    private string PeoplePath => Path.Combine(Root,"people.json");
    private string HistoryPath => Path.Combine(Root,"history.jsonl");
    private string Logs => Path.Combine(Root,"logs");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private static readonly string[] AcceptedHistoryDates =
    [
        "dd'/'MM'/'yyyy", "dd'-'MM'-'yyyy", "dd'.'MM'.'yyyy",
        "yyyy'-'MM'-'dd", "yyyy'/'MM'/'dd", "yyyy'.'MM'.'dd"
    ];
    // '/' in a .NET date format is a locale-dependent separator unless quoted.
    public static string FormatHistoryDate(DateTime date) =>
        date.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);
    public static bool TryParseHistoryDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value, AcceptedHistoryDates, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    private bool _historyReadWarning;
    public bool HistoryReadFailed { get; private set; }
    public LocalStore(string? root=null) { Root=root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupraAutoPackLe"); Directory.CreateDirectory(Root); Directory.CreateDirectory(Logs); }

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
        // Keep the latest checkpoint of each attempt while preserving legacy history.
        var rows=new List<HistoryRow>();
        var byAttempt=new Dictionary<string,int>(StringComparer.Ordinal);
        HistoryReadFailed=false;
        var invalid=0;
        var invalidDates=0;
        try
        {
            if(File.Exists(HistoryPath))
            {
                foreach(var line in File.ReadLines(HistoryPath))
                {
                    if(string.IsNullOrWhiteSpace(line))continue;
                    try
                    {
                        var r=JsonSerializer.Deserialize<HistoryRow>(line);
                        if(r==null){invalid++;continue;}
                        // Keep existing records in their original append-only file, but
                        // normalize dates in memory so both on-screen and XLSX filters work.
                        if(TryParseHistoryDate(r.Date,out var recordedDay))
                        {
                            var normal=FormatHistoryDate(recordedDay);
                            if(!string.Equals(r.Date,normal,StringComparison.Ordinal))
                                r=r with {Date=normal};
                        }
                        else invalidDates++;
                        if(!string.IsNullOrEmpty(r.AttemptId))
                        {
                            if(byAttempt.TryGetValue(r.AttemptId,out var at))rows[at]=r;
                            else {byAttempt.Add(r.AttemptId,rows.Count);rows.Add(r);}
                        }
                        else rows.Add(r);
                    }
                    catch(JsonException){invalid++;}
                }
            }
        }
        catch(IOException){HistoryReadFailed=true;}
        catch(UnauthorizedAccessException){HistoryReadFailed=true;}
        if(invalid>0 || invalidDates>0)HistoryReadFailed=true;
        if(HistoryReadFailed && !_historyReadWarning)
        {
            _historyReadWarning=true;
            Trace("HISTORY","READ_FAILED","ERROR",0,
                invalid>0?"INVALID_ROWS":invalidDates>0?"INVALID_DATE":"IO_ERROR");
        }
        if(!HistoryReadFailed)_historyReadWarning=false;
        return rows;
    }
    public void AppendHistory(HistoryRow row)
    {
        var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row,Json)+"\n");
        using var file=new FileStream(HistoryPath,FileMode.Append,FileAccess.Write,FileShare.Read);
        file.Write(bytes);
        file.Flush(flushToDisk:true);
    }
    public bool TryAppendHistory(HistoryRow row,string phase)
    {
        try
        {
            AppendHistory(row);
            // Verify the same read path used by UI/XLSX, not merely FileStream.Write().
            // Restrict full readback to the first/final journal checkpoints to keep
            // intermediate WMS actions responsive on long-running installations.
            if(phase=="START" || phase=="FINAL")
            {
                var saved=History().FirstOrDefault(r=>
                    !string.IsNullOrEmpty(row.AttemptId) &&
                    r.AttemptId==row.AttemptId &&
                    r.Phase==row.Phase);
                if(saved==null ||
                   !string.Equals(saved.Date,row.Date,StringComparison.Ordinal) ||
                   !string.Equals(saved.SO,row.SO,StringComparison.Ordinal) ||
                   !string.Equals(saved.DO,row.DO,StringComparison.Ordinal) ||
                   !string.Equals(saved.Result,row.Result,StringComparison.Ordinal))
                {
                    Trace("HISTORY","READBACK_"+phase,"ERROR",0,"ROW_NOT_VISIBLE");
                    return false;
                }
                Trace("HISTORY","READBACK_"+phase,"DONE");
            }
            // Diagnostic log includes only a stage code; business data stays in history.jsonl.
            Trace("HISTORY","SAVE_"+phase,"DONE");
            return true;
        }
        catch(UnauthorizedAccessException)
        {
            Trace("HISTORY","SAVE_"+phase,"ERROR",0,"ACCESS_DENIED");
            return false;
        }
        catch(IOException)
        {
            Trace("HISTORY","SAVE_"+phase,"ERROR",0,"IO_ERROR");
            return false;
        }
        catch(Exception)
        {
            Trace("HISTORY","SAVE_"+phase,"ERROR",0,"UNKNOWN_ERROR");
            return false;
        }
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
    public static string ResultLabel(string? result) => result switch
    {
        "UI_WORKFLOW_COMPLETE" => "Đã hoàn tất quy trình trên WMS",
        "UNCERTAIN_CHECK_WMS" => "Chưa hoàn tất - cần kiểm tra WMS",
        "OPERATOR_CANCELLED" => "Đã dừng thủ công",
        "PROCESSING" => "Đang xử lý",
        "IN_PROGRESS" => "Đang xử lý - chờ hoàn tất",
        _ => string.IsNullOrWhiteSpace(result) ? "Chưa xác định" : result
    };

    public IReadOnlyList<HistoryRow> FilterHistory(DateTime? from=null,DateTime? to=null,string resultFilter="ALL")
    {
        if(from.HasValue && to.HasValue && from.Value.Date>to.Value.Date)
            throw new ArgumentException("Khoảng ngày không hợp lệ.");
        return History().Where(r=>
        {
            if(!TryParseHistoryDate(r.Date,out var date))
            {
                // An unrecognized old date must still be recoverable with "Tất cả".
                if(from.HasValue||to.HasValue)return false;
            }
            else
            {
                if(from.HasValue&&date.Date<from.Value.Date)return false;
                if(to.HasValue&&date.Date>to.Value.Date)return false;
            }
            return resultFilter switch
            {
                "COMPLETED" => r.Result=="UI_WORKFLOW_COMPLETE",
                "INCOMPLETE" => r.Result!="UI_WORKFLOW_COMPLETE",
                _ => true
            };
        }).ToList();
    }

    public void ExportHistoryXlsx(string filename,DateTime? from=null,DateTime? to=null,string resultFilter="ALL")
    {
        using var wb=new XLWorkbook();
        var ws=wb.AddWorksheet("Lịch sử in tem");
        string[] header=["Ngày","Bắt đầu","Kết thúc","Thời gian xử lí (giây)","Nhân sự","Mã SO","Mã DO","Số SKU","Số kiện","Pcs","Kết quả","Điểm"];
        for(var i=0;i<header.Length;i++)ws.Cell(1,i+1).Value=header[i];
        var index=2;
        foreach(var row in FilterHistory(from,to,resultFilter))
        {
            ws.Cell(index,1).Value=SafeExcel(row.Date);
            ws.Cell(index,2).Value=SafeExcel(row.Start);
            ws.Cell(index,3).Value=SafeExcel(row.End);
            ws.Cell(index,4).Value=row.Seconds;
            ws.Cell(index,5).Value=SafeExcel(row.Person);
            ws.Cell(index,6).Value=SafeExcel(row.SO);
            ws.Cell(index,7).Value=SafeExcel(row.DO);
            ws.Cell(index,8).Value=row.SkuCount.HasValue ? row.SkuCount.Value.ToString(CultureInfo.InvariantCulture) : "Chưa có dữ liệu";
            ws.Cell(index,9).Value=row.ParcelCount.HasValue ? row.ParcelCount.Value.ToString(CultureInfo.InvariantCulture) : "Chưa có dữ liệu";
            ws.Cell(index,10).Value=row.Pcs.HasValue ? row.Pcs.Value.ToString(CultureInfo.InvariantCulture) : "Chưa có dữ liệu";
            ws.Cell(index,11).Value=SafeExcel(ResultLabel(row.Result));
            ws.Cell(index,12).Value=row.Points.HasValue ? row.Points.Value.ToString(CultureInfo.InvariantCulture) : "Chưa có dữ liệu";
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
