using ClosedXML.Excel;
using System.IO.Compression;
using System.Globalization;

namespace SupraAutoPackLe;

internal static class StartupSmoke
{
    public static int Run()
    {
        var tmp=Path.Combine(Path.GetTempPath(),"supra-pack-selftest-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new LocalStore(tmp);
            store.SavePeople(["Người thử A","Người thử B","Người thử A"]);
            if(store.LoadPeople().Count!=2)throw new Exception("PEOPLE_DEDUP_FAILED");
            store.AppendHistory(new HistoryRow("15/10/2026","08:00:00","08:00:09",9,
                "Người thử A","SO12345678","DO12345678","UI_WORKFLOW_COMPLETE"));
            store.AppendHistory(new HistoryRow("14/10/2026","08:01:00","08:01:05",5,
                "Người thử B","SO12345679","DO12345679","UNCERTAIN_CHECK_WMS"));
            store.AppendHistory(new HistoryRow("15/10/2026","08:03:00","08:03:02",2,
                "Người thử B","","","OPERATOR_CANCELLED"));
            if(store.FilterHistory(new DateTime(2026,10,15),new DateTime(2026,10,15)).Count!=2)
                throw new Exception("TODAY_FILTER_FAILED");
            if(store.FilterHistory(new DateTime(2026,10,14),new DateTime(2026,10,14)).Count!=1)
                throw new Exception("PREVIOUS_DAY_FILTER_FAILED");
            if(store.FilterHistory(resultFilter:"INCOMPLETE").Count!=2)
                throw new Exception("INCOMPLETE_FILTER_FAILED");
            if(LocalStore.ResultLabel("UNCERTAIN_CHECK_WMS")!="Chưa hoàn tất - cần kiểm tra WMS")
                throw new Exception("VI_RESULT_FAILED");
            var file=Path.Combine(tmp,"test.xlsx");
            store.ExportHistoryXlsx(file);
            using(var book=new XLWorkbook(file))
            {
                var sheet=book.Worksheet("Lịch sử in tem");
                if(sheet.Cell(2,5).GetString()!="Người thử A" ||
                   sheet.Cell(2,6).GetString()!="SO12345678" ||
                   sheet.Cell(2,7).GetString()!="DO12345678")
                    throw new Exception("XLSX_VALUES_FAILED");
            }
            var checkpointId=Guid.NewGuid().ToString("N");
            var checkpoint=new HistoryRow("15/10/2026","09:00:00","09:00:03",3,
                "Người thử A","","","IN_PROGRESS"){AttemptId=checkpointId,Phase="START"};
            if(!store.TryAppendHistory(checkpoint,"START"))throw new Exception("SAVE_CHECKPOINT_FAILED");
            if(!store.TryAppendHistory(checkpoint with {Phase="FINAL",Result="UNCERTAIN_CHECK_WMS"},"FINAL"))
                throw new Exception("SAVE_FINAL_FAILED");
            var afterRestart=new LocalStore(tmp).History().Where(r=>r.AttemptId==checkpointId).ToList();
            if(afterRestart.Count!=1||afterRestart[0].Result!="UNCERTAIN_CHECK_WMS")
                throw new Exception("CHECKPOINT_DEDUP_RESTART_FAILED");
            var filtered=Path.Combine(tmp,"filtered.xlsx");
            store.ExportHistoryXlsx(filtered,new DateTime(2026,10,15),new DateTime(2026,10,15),"COMPLETED");
            using(var book=new XLWorkbook(filtered))
            {
                var sheet=book.Worksheet("Lịch sử in tem");
                if(sheet.LastRowUsed()?.RowNumber()!=2 ||
                   sheet.Cell(2,11).GetString()!="Đã hoàn tất quy trình trên WMS")
                    throw new Exception("FILTERED_XLSX_FAILED");
            }
            // Regression: legacy Windows regional settings may save a hyphen or
            // dot even when ToString("dd/MM/yyyy") is used. Do not drop those rows.
            var oldCulture=CultureInfo.CurrentCulture;
            try
            {
                var region=(CultureInfo)CultureInfo.GetCultureInfo("de-DE").Clone();
                region.DateTimeFormat.DateSeparator=".";
                CultureInfo.CurrentCulture=region;
                if(LocalStore.FormatHistoryDate(new DateTime(2026,10,15))!="15/10/2026")
                    throw new Exception("HISTORY_FORMAT_DEPENDS_ON_WINDOWS_CULTURE");
                store.AppendHistory(new HistoryRow("15.10.2026","10:10:00","10:10:08",8,
                    "Người thử A","SO33333333","DO33333333","UI_WORKFLOW_COMPLETE"));
                store.AppendHistory(new HistoryRow("15-10-2026","10:20:00","10:20:08",8,
                    "Người thử B","SO44444444","DO44444444","UNCERTAIN_CHECK_WMS"));
                var recovered=new LocalStore(tmp).FilterHistory(
                    new DateTime(2026,10,15),new DateTime(2026,10,15));
                if(recovered.Count!=5 || recovered.Any(x=>x.Date!="15/10/2026") ||
                   !recovered.Any(x=>x.SO=="SO33333333") ||
                   !recovered.Any(x=>x.DO=="DO44444444"))
                    throw new Exception("LEGACY_WINDOWS_DATES_NOT_RECOVERED");
                var recoveredFile=Path.Combine(tmp,"recovered.xlsx");
                store.ExportHistoryXlsx(recoveredFile,new DateTime(2026,10,15),new DateTime(2026,10,15));
                using(var book=new XLWorkbook(recoveredFile))
                    if(book.Worksheet("Lịch sử in tem").LastRowUsed()?.RowNumber()!=6)
                        throw new Exception("RECOVERED_HISTORY_XLSX_MISSING_ROWS");
            }
            finally { CultureInfo.CurrentCulture=oldCulture; }
            // New nullable production metrics must not change or fabricate old records.
            var legacy=store.FilterHistory(new DateTime(2026,10,15),new DateTime(2026,10,15));
            if(legacy.Any(x=>x.SkuCount.HasValue||x.Pcs.HasValue||x.Points.HasValue))
                throw new Exception("LEGACY_METRICS_MUST_STAY_NULL");
            var current=new HistoryRow("16/10/2026","10:00:00","10:00:15",15,
                "Người thử A","SO87654321","DO87654321","UI_WORKFLOW_COMPLETE")
            {ParcelCount=3,SkuCount=null,Pcs=null,Points=null};
            store.AppendHistory(current);
            var today=store.FilterHistory(new DateTime(2026,10,16),new DateTime(2026,10,16));
            if(today.Count!=1 || today[0].ParcelCount!=3 || today[0].Points.HasValue)
                throw new Exception("NEW_FIELD_PERSISTENCE_FAILED");
            var daily=DailyProduction.From(today);
            if(daily.Completed!=1||daily.UniqueDo!=1||daily.Points.HasValue||
                daily.People.Single().MedianSeconds!=15)
                throw new Exception("DAILY_AGGREGATION_INVALID");
            // Duplicate DO completion cannot inflate unique daily DO count.
            var duplicate=current with {Start="11:00:00",End="11:00:20",Seconds=20};
            var dedup=DailyProduction.From([current,duplicate]);
            if(dedup.UniqueDo!=1||dedup.Completed!=2||dedup.Points.HasValue)
                throw new Exception("DUPLICATE_DO_OVERCOUNT");
            var scored=DailyProduction.From([current,current with {DO="DO88888888",Points=12.5m}]);
            if(scored.Points!=12.5m||scored.ScoredAttempts!=1)
                throw new Exception("SCORE_COVERAGE_INVALID");
            var metricsFile=Path.Combine(tmp,"metrics.xlsx");
            store.ExportHistoryXlsx(metricsFile,new DateTime(2026,10,16),new DateTime(2026,10,16));
            using(var book=new XLWorkbook(metricsFile))
            {
                var ws=book.Worksheet("Lịch sử in tem");
                if(ws.Cell(2,9).GetString()!="3"||
                   ws.Cell(2,8).GetString()!="Chưa có dữ liệu"||
                   ws.Cell(2,12).GetString()!="Chưa có dữ liệu")
                    throw new Exception("METRIC_XLSX_MISSING_PLACEHOLDERS");
            }
            store.Trace("TEST","STARTUP_SMOKE","DONE",9,"TARGETS=1");
            var zip=Path.Combine(tmp,"testlogs.zip");
            store.ExportLogs(zip,new DateTime(2026,1,1),new DateTime(2026,12,31));
            using(var archive=ZipFile.OpenRead(zip))
                if(!archive.Entries.Any(e=>e.FullName.StartsWith("logs/")))
                    throw new Exception("DIAGNOSTIC_ZIP_EMPTY");
            // Export+purge: verified ZIP on Desktop, delete only exported
            // technical JSONL. Never delete business history or staff.
            var safeDesktop=Path.Combine(tmp,"desktop");Directory.CreateDirectory(safeDesktop);
            store.Trace("TEST","BEFORE_PURGE","DONE",1,"COUNT=1");
            var report=store.ExportAllLogsToDesktopAndPurge(safeDesktop);
            if(report.ExportedFiles<1||report.ExportedFiles!=report.DeletedFiles||
                !File.Exists(report.Path)||
                Directory.GetFiles(Path.Combine(tmp,"logs"),"*.jsonl").Length!=0)
                throw new Exception("DIAGNOSTIC_VERIFIED_PURGE_FAILED");
            using(var saved=ZipFile.OpenRead(report.Path))
                if(!saved.Entries.Any(e=>e.FullName.StartsWith("logs/")) ||
                   !saved.Entries.Any(e=>e.FullName=="README.txt"))
                    throw new Exception("DIAGNOSTIC_EXPORT_UNREADABLE");
            if(!File.Exists(Path.Combine(tmp,"history.jsonl"))||
               !File.Exists(Path.Combine(tmp,"people.json")))
                throw new Exception("DIAGNOSTIC_PURGE_TOUCHED_BUSINESS_DATA");
            store.TraceException("TEST","EXCEPTION_TEST",
                new InvalidOperationException("password=SHOULD_NEVER_APPEAR"));
            var unsafeDest=Path.Combine(tmp,"not_a_directory.txt");
            File.WriteAllText(unsafeDest,"not a folder");
            try
            {
                store.ExportAllLogsToDesktopAndPurge(unsafeDest);
                throw new Exception("INVALID_DESKTOP_WAS_ACCEPTED");
            }
            catch(DirectoryNotFoundException) {}
            if(Directory.GetFiles(Path.Combine(tmp,"logs"),"*.jsonl").Length==0)
                throw new Exception("FAILED_EXPORT_ERASED_SOURCE");
            var protectedLog=File.ReadAllText(Directory.GetFiles(
                Path.Combine(tmp,"logs"),"*.jsonl").Single());
            if(protectedLog.Contains("SHOULD_NEVER_APPEAR",StringComparison.Ordinal))
                throw new Exception("EXCEPTION_MESSAGE_LEAKED");
            store.Trace("TEST","REDACTION_TEST","ERROR",0,"SESSION_SECRET");
            var newLog=string.Join("\n",Directory.GetFiles(Path.Combine(tmp,"logs"),"*.jsonl")
                .Select(File.ReadAllText));
            if(newLog.Contains("SESSION_SECRET",StringComparison.Ordinal) ||
               !newLog.Contains("REDACTED",StringComparison.Ordinal))
                throw new Exception("AUTH_SESSION_MARKER_LEAKED");
            if(!WmsWorkflowJs.Script.Contains("COUNT_BLUR_MISMATCH")||
               !WmsWorkflowJs.Script.Contains("NO")||
               !WmsWorkflowJs.Script.Contains("ĐÓNG GÓI HÀNG CHẴN"))
                throw new Exception("WMS_STATE_CONTRACT_MISSING");
            return 0;
        }
        catch{return 1;}
        finally{try{Directory.Delete(tmp,true);}catch{}}
    }
}
