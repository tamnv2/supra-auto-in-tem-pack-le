namespace SupraAutoPackLe;

/// <summary>
/// Shared semantic WMS printer configuration on saleorder/list and saleorder/packing.
/// Returns only UI printer labels, no auth information or unrelated DOM data.
/// </summary>
internal static class WmsPrinterSetupJs
{
    internal const string Script = """
    (() => {
      'use strict';
      if (window.__supraPrinterV1) return;
      const clean = v => String(v ?? '').replace(/\s+/g,' ').trim();
      const label = el => clean(el?.getAttribute?.('aria-label') ||
        el?.getAttribute?.('title') || el?.innerText || el?.textContent || '');
      const validRoute = () => location.protocol === 'https:' &&
        location.hostname === 'wms-supra.winmart.vn' &&
        ['/sft3/app/saleorder/list','/sft3/app/saleorder/packing']
          .includes(location.pathname.replace(/\/+$/,''));
      const visible = el => !!el && el.isConnected !== false &&
        el.getClientRects().length > 0 &&
        getComputedStyle(el).display !== 'none' &&
        getComputedStyle(el).visibility !== 'hidden';
      const enabled = el => visible(el) && !el.disabled &&
        el.getAttribute('aria-disabled') !== 'true';
      const unique = list => list.length === 1 ? list[0] : null;
      const buttons = root => [...root.querySelectorAll('button,[role="button"]')].filter(visible);
      const dialogs = () => [...document.querySelectorAll('mat-dialog-container,[role="dialog"]')]
        .filter(visible)
        .filter(el => !el.parentElement?.closest('mat-dialog-container,[role="dialog"]'));
      const dialog = () => unique(dialogs().filter(el =>
        clean(el.innerText).toLocaleUpperCase('vi').includes('CHỌN MÁY IN')));
      const accountTriggers = () => [...document.querySelectorAll(
        'mat-toolbar#app-header app-header > button.mat-menu-trigger.m-r-5')].filter(enabled);
      const menuItems = () => [...document.querySelectorAll(
        '[role="menu"] button,.mat-menu-content button,[role="menuitem"]')].filter(enabled)
        .filter(el => label(el).replace(/^printer\s+/i,'').trim()==='Thiết lập máy in');
      const field = () => {
        const dlg = dialog();
        if (!dlg) return null;
        return unique([...dlg.querySelectorAll(
          'app-printer-modal input:not([type="password"]),app-printer-modal mat-select,'+
          'app-printer-modal select,app-printer-modal [role="combobox"]')].filter(visible));
      };
      const fieldValue = el => {
        if (!el) return '';
        if (el.tagName === 'INPUT') return clean(el.value);
        if (el.tagName === 'SELECT') return clean(el.selectedOptions?.[0]?.textContent);
        return clean(el.querySelector('.mat-select-value-text,.mat-mdc-select-value-text')?.textContent ||
          el.querySelector('.mat-select-trigger')?.textContent || el.innerText || el.textContent);
      };
      const isPrinter = value => {
        const s = clean(value);
        return s.length >= 4 && s.length <= 125 &&
          !/^(chọn|--|select|không có|no options)/i.test(s) &&
          !/password|token|cookie|authorization|secret/i.test(s);
      };
      const options = () => {
        const dlg = dialog();
        if (!dlg) return [];
        const native = field();
        if (native?.tagName === 'SELECT') return [...native.querySelectorAll('option')]
          .filter(el=>!el.disabled && isPrinter(label(el)));
        return [...document.querySelectorAll(
          '.cdk-overlay-container mat-option,.cdk-overlay-container [role="option"],'+
          '.mat-select-panel mat-option,.mat-mdc-select-panel mat-option')]
          .filter(enabled).filter(el => isPrinter(label(el)));
      };
      let cache = new Set();
      let currentDialog = null;
      const syncCache = () => {
        const d = dialog();
        if (d !== currentDialog) {cache = new Set();currentDialog = d;}
        for (const option of options()) cache.add(label(option));
      };
      const scroller = () => [...document.querySelectorAll(
        '.cdk-overlay-container .cdk-virtual-scroll-viewport,'+
        '.cdk-overlay-container .mat-select-panel,'+
        '.cdk-overlay-container .mat-mdc-select-panel')]
        .filter(visible).find(el=>el.scrollHeight>el.clientHeight+2);
      const expanded = () => {
        const f=field();
        return !!f && (f.getAttribute('aria-expanded') === 'true' ||
          !!document.querySelector('.cdk-overlay-container mat-option,'+
          '.cdk-overlay-container [role="option"]') ||
          (f.tagName === 'SELECT'));
      };
      const confirm = () => {
        const d=dialog();
        return d?unique(buttons(d).filter(el=>label(el)==='Xác nhận')):null;
      };
      const close = () => {
        const d=dialog();
        return d?unique(buttons(d).filter(el=>['Đóng','Thoát'].includes(label(el)))):null;
      };
      const peek = (target='') => {
        if (!validRoute()) return {ok:false,reason:'NOT_AUTHENTICATED_WMS_ROUTE'};
        const d=dialog(),f=field();
        syncCache();
        const scroll=scroller();
        const hasMore=!!scroll && scroll.scrollTop+scroll.clientHeight<scroll.scrollHeight-3;
        const all=[...cache].sort((a,b)=>a.localeCompare(b,'vi'));
        return {
          ok:true,reason:'OK',printerDialog:!!d,fieldReady:!!f,
          currentPrinter:isPrinter(fieldValue(f))?fieldValue(f):'',
          accountTriggerCount:accountTriggers().length,
          settingsVisible:menuItems().length===1,optionsExpanded:expanded(),
          options:all.slice(0,300),optionCount:all.length,
          choiceVisible:!!target && options().filter(o=>label(o)===target).length===1,
          scanDone:!!d && !!f && expanded() && !hasMore,
          confirmEnabled:!!confirm() && enabled(confirm()),
          closeEnabled:!!close() && enabled(close())
        };
      };
      const perform = (action,selected='') => {
        if (!validRoute()) return {ok:false,reason:'NOT_AUTHENTICATED_WMS_ROUTE'};
        if (action==='ACCOUNT_OPEN') {
          if (dialogs().length)return {ok:false,reason:'ANOTHER_DIALOG_OPEN'};
          const trigger=unique(accountTriggers());
          if (!trigger)return {ok:false,reason:'ACCOUNT_TRIGGER_NOT_UNIQUE'};
          trigger.click();return {ok:true};
        }
        if (action==='PRINTER_MENU') {
          if (dialogs().length)return {ok:false,reason:'ANOTHER_DIALOG_OPEN'};
          const m=unique(menuItems());
          if (!m)return {ok:false,reason:'PRINTER_MENU_NOT_UNIQUE'};
          m.click();return {ok:true};
        }
        if (!dialog()) return {ok:false,reason:'PRINTER_DIALOG_NOT_OPEN'};
        if (action==='EXPAND') {
          const f=field();
          if (!enabled(f))return {ok:false,reason:'PRINTER_FIELD_NOT_READY'};
          if (f.tagName !== 'SELECT') {f.click();}
          syncCache();
          return {ok:true};
        }
        if (action==='SCAN_RESET') {
          const s=scroller();
          if(s) {s.scrollTop=0;s.dispatchEvent(new Event('scroll',{bubbles:true}));}
          return {ok:true};
        }
        if (action==='SCAN_NEXT') {
          syncCache();
          const s=scroller();
          if (!s || s.scrollTop+s.clientHeight>=s.scrollHeight-3)return {ok:true};
          const old=s.scrollTop;
          s.scrollTop=Math.min(s.scrollHeight-s.clientHeight,old+Math.max(100,s.clientHeight-24));
          s.dispatchEvent(new Event('scroll',{bubbles:true}));
          return {ok:true};
        }
        if (action==='PICK') {
          if (!isPrinter(selected) || !cache.has(selected))
            return {ok:false,reason:'PRINTER_NOT_IN_SCANNED_OPTIONS'};
          const f=field();
          if (!f)return {ok:false,reason:'PRINTER_FIELD_NOT_READY'};
          if (f.tagName === 'SELECT') {
            const targets=[...f.querySelectorAll('option')].filter(o=>label(o)===selected);
            if (targets.length!==1)return {ok:false,reason:'PRINTER_OPTION_NOT_UNIQUE'};
            f.value=targets[0].value;
            f.dispatchEvent(new Event('change',{bubbles:true}));
            return {ok:true};
          }
          const matches=options().filter(o=>label(o)===selected);
          if (matches.length!==1)return {ok:false,reason:'PRINTER_OPTION_NOT_VISIBLE'};
          matches[0].click();return {ok:true};
        }
        if (action==='CONFIRM') {
          const f=field();
          if (!f || fieldValue(f)!==selected || !isPrinter(selected))
            return {ok:false,reason:'PRINTER_READBACK_MISMATCH'};
          const btn=confirm();
          if (!enabled(btn))return {ok:false,reason:'PRINTER_CONFIRM_DISABLED'};
          btn.click();return {ok:true};
        }
        if (action==='CLOSE') {
          const f=field();
          if (!f || fieldValue(f)!==selected || !isPrinter(selected))
            return {ok:false,reason:'PRINTER_READBACK_MISMATCH'};
          const btn=close();
          if (!enabled(btn))return {ok:false,reason:'PRINTER_CLOSE_NOT_AVAILABLE'};
          btn.click();return {ok:true};
        }
        return {ok:false,reason:'UNKNOWN_PRINTER_ACTION'};
      };
      window.__supraPrinterV1={peek,perform};
    })();
    """;
}
