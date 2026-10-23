using System.Diagnostics;

namespace SupraAutoPackLe;

/// <summary>Operator-approved WMS printer setup shared by both workflows.
/// Never guesses printer IDs or retries a non-idempotent print command.</summary>
internal static class WmsPrinterCoordinator
{
    private static void EnsureActive(Func<bool> stillActive)
    {
        if(!stillActive())throw new OperationCanceledException("Nghiệp vụ đã thay đổi.");
    }
    private static async Task<PrinterSetupPeek> WaitAsync(
        WmsBrowser browser,Func<bool> active,Func<PrinterSetupPeek,bool> accept,
        string stage,int timeoutSeconds,string target="")
    {
        var timer=Stopwatch.StartNew();
        PrinterSetupPeek? last=null;
        while(timer.Elapsed<TimeSpan.FromSeconds(timeoutSeconds))
        {
            EnsureActive(active);
            last=await browser.PrinterPeekAsync(target);
            if(last?.ok==true && accept(last))return last;
            await Task.Delay(450);
        }
        // Surface bounded UI diagnostics, not printer names, credentials or HTML.
        throw new InvalidOperationException(stage+" ("+(last?.reason??"WMS_NOT_READY")+
            "; dialog="+(last?.printerDialog==true?1:0)+
            "; input="+(last?.fieldReady==true?1:0)+
            "; expanded="+(last?.optionsExpanded==true?1:0)+
            "; options="+(last?.optionCount??0)+").");
    }
    private static async Task ActionAsync(WmsBrowser browser,Func<bool> active,
        string action,string selected="")
    {
        EnsureActive(active);
        var result=await browser.PrinterActionAsync(action,selected);
        EnsureActive(active);
        if(!result.ok)throw new InvalidOperationException(action+": "+result.reason);
    }
    public static async Task<string> SelectAsync(
        Form owner,WmsBrowser browser,Func<bool> active,Action<string> progress)
    {
        progress("Chờ đăng nhập WMS và mở trang nghiệp vụ.");
        // Login has no fixed timeout: employees complete SSO themselves.
        while(true)
        {
            EnsureActive(active);
            browser.TryNavigateTemChanListAfterLogin();
            var p=await browser.PrinterPeekAsync();
            // A valid URL alone does not establish login: WMS may show the SSO
            // UI or be hydrating Angular on the same route. Wait for the actual
            // authenticated account menu, without any credential inspection.
            if(p?.ok==true && p.accountTriggerCount==1)break;
            await Task.Delay(1100);
        }
        progress("WMS đã sẵn sàng. Mở Thiết lập máy in.");
        var initial=await WaitAsync(browser,active,
            p=>p.accountTriggerCount==1 &&
                (p.printerDialog || p.blockingDialogCount==0),
            "WMS đang có hộp thoại khác chưa được đóng",25);
        // The operator may already have opened CHỌN MÁY IN. Never reopen
        // the account menu on top of an existing printer dialog.
        if(!initial.printerDialog)
        {
            await ActionAsync(browser,active,"ACCOUNT_OPEN");
            await WaitAsync(browser,active,p=>p.settingsVisible,
                "Không thấy lệnh Thiết lập máy in",12);
            await ActionAsync(browser,active,"PRINTER_MENU");
        }
        var opened=await WaitAsync(browser,active,
            p=>p.printerDialog&&p.fieldReady,
            "Hộp thoại chọn máy in chưa sẵn sàng",15);
        string previous=opened.currentPrinter;
        progress("Đang mở danh sách máy in từ ô chọn WMS.");
        await ActionAsync(browser,active,"EXPAND");
        await WaitAsync(browser,active,
            p=>p.printerDialog && p.optionsExpanded && p.optionCount>0,
            "Không đọc được danh sách máy in WMS",15);
        progress("Đang quét toàn bộ danh sách máy in.");
        PrinterSetupPeek? scanned=null;
        var scanDone=false;
        for(int i=0;i<90;i++)
        {
            EnsureActive(active);
            scanned=await browser.PrinterPeekAsync();
            if(scanned?.ok!=true||!scanned.printerDialog)
                throw new InvalidOperationException("Hộp thoại máy in bị đóng trong lúc quét.");
            if(scanned.scanDone){scanDone=true;break;}
            await ActionAsync(browser,active,"SCAN_NEXT");
            await Task.Delay(125);
        }
        if(!scanDone || scanned==null || scanned.options.Count==0 ||
           scanned.options.Count>300)
            throw new InvalidOperationException("Không quét được đầy đủ danh sách máy in WMS.");
        progress("Đã đọc "+scanned.options.Count+" máy in. Chờ người dùng lựa chọn.");
        EnsureActive(active);
        string choice;
        using(var picker=new PrinterSelectionForm(scanned.options,previous))
        {
            if(picker.ShowDialog(owner)!=DialogResult.OK)
                throw new OperationCanceledException("Chưa chọn máy in.");
            choice=picker.SelectedPrinter;
        }
        EnsureActive(active);
        if(!scanned.options.Contains(choice,StringComparer.Ordinal))
            throw new InvalidOperationException("Máy in không thuộc danh sách vừa quét.");
        progress("Đang chọn và xác minh: "+choice);
        // Showing the native WinForms chooser transfers focus away from WebView2.
        // Angular may close the autocomplete on blur; reopen it after the owner
        // chooses, even when WMS had displayed options before ShowDialog().
        await ActionAsync(browser,active,"EXPAND");
        await WaitAsync(browser,active,
            p=>p.printerDialog && p.optionsExpanded && p.optionCount>0,
            "Không mở lại được danh sách sau khi chọn máy in",12);
        // EXPAND clears only the Angular search text. Always select an actual
        // WMS option, including when the operator chooses the previous printer.
        // Otherwise the input remains empty and Close cannot verify readback.
        {
            await ActionAsync(browser,active,"SCAN_RESET");
            PrinterSetupPeek? visible=null;
            for(int i=0;i<90;i++)
            {
                EnsureActive(active);
                visible=await browser.PrinterPeekAsync(choice);
                if(visible?.choiceVisible==true)break;
                await ActionAsync(browser,active,"SCAN_NEXT");
                await Task.Delay(150);
            }
            if(visible?.choiceVisible!=true)
                throw new InvalidOperationException("Không tìm lại được máy in trong danh sách WMS.");
            await ActionAsync(browser,active,"PICK",choice);
            await WaitAsync(browser,active,p=>p.printerDialog&&p.currentPrinter==choice,
                "WMS chưa nhận giá trị máy in vừa chọn",10);
        }
        if(choice!=previous)
        {
            await WaitAsync(browser,active,p=>p.confirmEnabled,
                "Nút Xác nhận chưa cho phép thao tác",8);
            await ActionAsync(browser,active,"CONFIRM",choice);
            await WaitAsync(browser,active,p=>!p.printerDialog,
                "WMS chưa đóng hộp thoại sau khi xác nhận",15);
            // Verify saved printer, not just the just-clicked value.
            await WaitAsync(browser,active,p=>p.accountTriggerCount==1,
                "WMS chưa sẵn sàng để kiểm tra lại máy in",15);
            await ActionAsync(browser,active,"ACCOUNT_OPEN");
            await WaitAsync(browser,active,p=>p.settingsVisible,
                "Menu thiết lập máy in chưa sẵn sàng",10);
            await ActionAsync(browser,active,"PRINTER_MENU");
            var check=await WaitAsync(browser,active,p=>p.printerDialog&&p.fieldReady,
                "Không đọc lại được máy in sau khi xác nhận",12);
            if(check.currentPrinter!=choice)
                throw new InvalidOperationException("Máy in WMS chưa lưu đúng lựa chọn.");
        }
        // When unchanged, WMS disables Confirm: use Close, never pretend a
        // disabled button was successfully activated.
        var last=await WaitAsync(browser,active,
            p=>p.printerDialog && p.currentPrinter==choice && p.closeEnabled,
            "Nút Đóng của hộp thoại máy in không khả dụng",12);
        await ActionAsync(browser,active,"CLOSE",choice);
        await WaitAsync(browser,active,p=>!p.printerDialog,
            "Hộp thoại máy in chưa được đóng",12);
        progress("Đã xác minh máy in: "+choice);
        return choice;
    }
}
