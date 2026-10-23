namespace SupraAutoPackLe;

/// <summary>Verified semantic DOM actions for the carton-label workflow.
/// No coordinates, network access, credentials or silent retries of printing.</summary>
internal static class WmsTemChanJs
{
    internal const string Script = """
    (() => {
      'use strict';
      if (window.__supraTemChanV1) return;
      const compact = s => String(s ?? '').replace(/\s+/g,' ').trim();
      const norm = s => compact(s).normalize('NFC').toLocaleLowerCase('vi');
      const visible = el => !!el && el.isConnected &&
        el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden' &&
        getComputedStyle(el).display !== 'none';
      const enabled = el => visible(el) && !el.disabled &&
        el.getAttribute('aria-disabled') !== 'true';
      const unique = els => els.length === 1 ? els[0] : null;
      const expectedStatus = 'đã lưu trữ hàng sau đóng gói';
      const onlyDigits = value => /^[1-9]\d*$/.test(String(value || '').trim());
      const positiveInt = value => {
        const s = String(value ?? '').trim();
        const n = Number(s);
        return onlyDigits(s) && Number.isSafeInteger(n) ? n : null;
      };
      const pathOk = so => /^[A-Z0-9_-]{5,50}$/.test(so) &&
        location.protocol === 'https:' && location.hostname === 'wms-supra.winmart.vn' &&
        location.pathname.replace(/\/$/,'') === '/sft3/app/saleorder/details/' + so &&
        location.search === '?PickType=PickOrder';
      // Read a status only when it is paired with the exact "Trạng thái" label.
      // WMS layouts have nested wrappers and decorative text nodes; assuming the
      // *first* following text node is the value produced false STATUS_NOT_READY.
      // Never accept a status word elsewhere on the page or guess from its color.
      const statusEvidence = () => {
        if (!document.body) return {known:false,valid:false};
        const walker = document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);
        const labels = [], values = [];
        let node;
        while ((node = walker.nextNode())) {
          const el = node.parentElement;
          if (!el || !visible(el) || ['SCRIPT','STYLE'].includes(el.tagName)) continue;
          const val=norm(node.textContent);
          if (/^trạng thái\s*:?$/.test(val)) labels.push(node);
        }
        if (labels.length !== 1) return {known:false,valid:false};
        const label = labels[0];
        // Most WMS revisions render the label and its value inside a common row.
        // Limit to local ancestors; the whole summary containing other fields
        // must not be treated as status evidence.
        for (let el=label.parentElement,depth=0;el && depth<4;el=el.parentElement,depth++) {
          const raw=compact(el.innerText || el.textContent);
          if (raw.length>0 && raw.length<=200 &&
              /trạng thái\s*:/i.test(raw) && norm(raw).includes(expectedStatus) &&
              !/loại đơn hàng xuất\s*:/i.test(raw))
            return {known:true,valid:true};
        }
        // Last resort: search a small sequence of *visible leaf text nodes*
        // following that label, stopping at the next labeled field.
        const scan=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);
        let inField=false,steps=0;
        while ((node=scan.nextNode())) {
          if (node===label) {inField=true;continue;}
          if (!inField)continue;
          const el=node.parentElement;
          if (!el || !visible(el))continue;
          const value=norm(node.textContent);
          if (!value)continue;
          if (/^[^:]{2,65}:$/.test(value))break;
          if (value===expectedStatus)return {known:true,valid:true};
          // A single explicit other status is evidence of not-ready; loose
          // captions or decoration are NOT business-status values.
          if (/^(?:đang|chưa|chờ|đã|hủy|huỷ|mới)\b/u.test(value) && value.length<=110)
            values.push(value);
          if (++steps>=9)break;
        }
        return values.length===1 ? {known:true,valid:false} :
          {known:false,valid:false};
      };
      const tabs = () => [...document.querySelectorAll('[role="tab"],.mat-tab-label')]
        .filter(el => visible(el) && norm(el.innerText) === 'thông tin kiện hàng');
      const selectedTab = () => tabs().length === 1 &&
        (tabs()[0].getAttribute('aria-selected') === 'true' ||
        tabs()[0].classList.contains('mat-tab-label-active') ||
        tabs()[0].getAttribute('aria-current') === 'page');
      const printButtons = () => [...document.querySelectorAll('button')]
        .filter(el => enabled(el) && !!el.closest('tr,[role="row"]') &&
          (!!el.querySelector('mat-icon, .mat-icon') ?
            norm(el.querySelector('mat-icon, .mat-icon').textContent) === 'print' :
            norm(el.innerText) === 'print') &&
          el.classList.contains('app-table-action-button'));
      const modal = () => unique([...document.querySelectorAll(
        'mat-dialog-container,[role="dialog"]')].filter(el =>
          visible(el) && norm(el.innerText).includes('tùy chọn in nhãn')));
      const modalInfo = (so) => {
        const dlg = modal();
        if (!dlg) return {dialogOpen:false, rangeValid:false, reason:'PRINT_DIALOG_ABSENT'};
        const inputs = [...dlg.querySelectorAll('input[name="startPackage"]')].filter(visible);
        const ends = [...dlg.querySelectorAll('input[name="endPackage"]')].filter(visible);
        if (inputs.length !== 1 || ends.length !== 1)
          return {dialogOpen:true, rangeValid:false, reason:'PACKAGE_INPUT_AMBIGUOUS'};
        const m = compact(dlg.innerText).match(/Tổng số thùng:\s*([^ ]+)/i);
        const total = positiveInt(m && m[1]);
        const start = positiveInt(inputs[0].value);
        const end = positiveInt(ends[0].value);
        const text = compact(dlg.innerText);
        const soMatch = text.includes(so);
        const valid = !!total && !!start && !!end && start <= end && end <= total && soMatch;
        return {
          dialogOpen:true, rangeValid:valid,
          reason: !soMatch?'MODAL_SO_MISMATCH':!total?'TOTAL_NOT_INTEGER':
                  !start?'START_NOT_INTEGER':!end?'END_NOT_INTEGER':
                  start>end || end>total?'RANGE_OUT_OF_BOUNDS':'OK',
          // Only aggregate counts, never SO/customer or credentials.
          total:total||0, begin:start||0, finish:end||0
        };
      };
      const detail = so => {
        if (!pathOk(so)) return {ok:false,reason:'WRONG_PAGE'};
        const evidence = statusEvidence();
        const known = evidence.known;
        const statusValid = evidence.valid;
        const hasTab = tabs().length === 1;
        return {ok:true,reason:!known?'STATUS_NOT_LOCATED':!statusValid?'STATUS_NOT_READY':
              !hasTab?'PACKAGE_TAB_AMBIGUOUS':'OK',
          statusKnown:known,statusValid,tabReady:hasTab,
          tabSelected:selectedTab(),printCount:printButtons().length,
          ...modalInfo(so)};
      };
      const action = (name,so) => {
        const p = detail(so);
        if (!p.ok || !p.statusValid || !p.tabReady)
          return {ok:false,reason:p.reason};
        if (name === 'TAB') {
          if (p.tabSelected) return {ok:true,reason:'ALREADY_ACTIVE'};
          const tab = unique(tabs());
          if (!enabled(tab)) return {ok:false,reason:'TAB_NOT_UNIQUE'};
          tab.click();
          return {ok:true,reason:'TAB_CLICKED'};
        }
        if (name === 'OPEN_PRINT') {
          if (!p.tabSelected || p.dialogOpen) return {ok:false,reason:'NOT_PACKAGE_TAB'};
          const button = unique(printButtons());
          if (!button) return {ok:false,reason:'PRINT_ROW_NOT_UNIQUE'};
          button.click();
          return {ok:true,reason:'PRINT_DIALOG_REQUESTED'};
        }
        if (name === 'PRINT_LABEL') {
          if (!p.dialogOpen || !p.rangeValid) return {ok:false,reason:p.reason};
          const dlg = modal();
          if (!dlg) return {ok:false,reason:'PRINT_DIALOG_ABSENT'};
          const buttons = [...dlg.querySelectorAll('button')]
            .filter(el=>enabled(el) && norm(el.innerText)==='in nhãn');
          const button=unique(buttons);
          if (!button) return {ok:false,reason:'PRINT_CONFIRM_AMBIGUOUS'};
          // Single, explicitly requested non-idempotent print command. Never retry.
          button.click();
          return {ok:true,reason:'PRINT_COMMAND_SENT'};
        }
        return {ok:false,reason:'UNKNOWN_ACTION'};
      };
      window.__supraTemChanV1 = {peek:detail,perform:action};
    })();
    """;
}
