using ClosedXML.Excel;
using System.IO.Compression;

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
            var filtered=Path.Combine(tmp,"filtered.xlsx");
            store.ExportHistoryXlsx(filtered,new DateTime(2026,10,15),new DateTime(2026,10,15),"COMPLETED");
            using(var book=new XLWorkbook(filtered))
            {
                var sheet=book.Worksheet("Lịch sử in tem");
                if(sheet.LastRowUsed()?.RowNumber()!=2 ||
                   sheet.Cell(2,8).GetString()!="Đã hoàn tất quy trình trên WMS")
                    throw new Exception("FILTERED_XLSX_FAILED");
            }
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
