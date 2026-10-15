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
                   sheet.Cell(2,8).GetString()!="Đã hoàn tất quy trình trên WMS")
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
            store.Trace("TEST","STARTUP_SMOKE","DONE",9,"TARGETS=1");
            var zip=Path.Combine(tmp,"testlogs.zip");
            store.ExportLogs(zip,new DateTime(2026,1,1),new DateTime(2026,12,31));
            using(var archive=ZipFile.OpenRead(zip))
                if(!archive.Entries.Any(e=>e.FullName.StartsWith("logs/")))
                    throw new Exception("DIAGNOSTIC_ZIP_EMPTY");
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
