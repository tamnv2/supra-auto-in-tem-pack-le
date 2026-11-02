namespace SupraAutoPackLe;

/// <summary>
/// WMS SO handover correction: evidence-based semantic DOM interactions only.
/// No arbitrary scripts, HTTP calls, credentials, storage access or coordinate clicks.
/// </summary>
internal static class WmsHandoverJs
{
    internal const string Script = """
    (() => {
      'use strict';
      if(window.__supraHandoverV1) return;
      const text = v => String(v ?? '').normalize('NFC').replace(/\s+/g,' ').trim();
      const norm = v => text(v).toLocaleLowerCase('vi');
      const visible = el => !!el && el.isConnected && el.getClientRects().length>0 &&
        getComputedStyle(el).visibility!=='hidden' && getComputedStyle(el).display!=='none';
      const enabled = el => visible(el) && !el.disabled && el.getAttribute('aria-disabled')!=='true';
      const one = list => list.length===1 ? list[0]:null;
      const validSo = so => typeof so==='string' && /^[A-Z0-9_-]{5,50}$/.test(so);
      const validCount = count => Number.isSafeInteger(count) && count>=1 && count<=999999;
      const pathOk = () => location.protocol==='https:' &&
        location.hostname==='wms-supra.winmart.vn' &&
        location.pathname.replace(/\/$/,'')==='/sft3/app/manager/sohandover';
      const dialogContainers = () => [...document.querySelectorAll('mat-dialog-container,[role="dialog"]')]
        .filter(visible).filter(el => !el.closest('mat-dialog-container') ||
          el.matches('mat-dialog-container'));
      const dialogs = selector => dialogContainers()
        .filter(el => !!el.querySelector(selector));
      const buttonText = el => norm(el.innerText||el.textContent||'')
        .replace(/^(?:search|grid_view|edit)\s*/,'');
      const labelled = (el,fragment) => {
        const own = norm([el.getAttribute('aria-label'),el.getAttribute('placeholder'),
          el.getAttribute('name')].filter(Boolean).join(' '));
        const field = el.closest('mat-form-field,.mat-form-field,[class*="form-field"]');
        return own.includes(fragment) || !!field && norm(field.innerText||field.textContent).includes(fragment);
      };
      const searchInputs = () => [...document.querySelectorAll('input')].filter(visible)
        .filter(el => !el.closest('mat-dialog-container,[role="dialog"]'))
        .filter(el => labelled(el,'mã vị trí') || labelled(el,'tote/ so') ||
          labelled(el,'mã vị trí/ tote/ so'));
      const searchButtons = () => [...document.querySelectorAll('button')].filter(enabled)
        .filter(el=>!el.closest('mat-dialog-container,[role="dialog"]'))
        .filter(el=>buttonText(el)==='tìm kiếm');
      const containsSo = (line,so) => text(line).toUpperCase().split(/[^A-Z0-9_-]+/).includes(so);
      const resultRows = so => validSo(so) ? [...document.querySelectorAll('mat-row,tr,[role="row"]')]
        .filter(visible).filter(el=>containsSo(el.innerText||el.textContent,so)) : [];
      const rowActions = row => row ? [...row.querySelectorAll('button')].filter(enabled)
        .filter(el=>norm(el.querySelector('mat-icon,.mat-icon')?.textContent||el.innerText||el.textContent)==='grid_view') : [];
      const updateDialogs = () => dialogs('app-confirm-update');
      const quantityInputs = dlg => dlg ? [...dlg.querySelectorAll('input')].filter(visible)
        .filter(el=>el.type==='number' && labelled(el,'số lượng')) : [];
      const updateButtons = dlg => dlg ? [...dlg.querySelectorAll('button')].filter(enabled)
        .filter(el=>buttonText(el)==='cập nhật') : [];
      const yesDialogs = () => dialogs('app-notification').filter(el =>
        [...el.querySelectorAll('button')].some(b=>enabled(b)&&buttonText(b)==='có'));
      const yesButtons = dlg => dlg ? [...dlg.querySelectorAll('button')].filter(enabled)
        .filter(el=>buttonText(el)==='có') : [];
      const evidence = (so,count) => {
        if(!pathOk() || !validSo(so))
          return {ok:false,reason:'WRONG_PAGE_OR_SO'};
        const input=one(searchInputs());
        const row=one(resultRows(so));
        const dlg=one(updateDialogs());
        const notify=one(yesDialogs());
        const qty=one(quantityInputs(dlg));
        return {
          ok:true,reason:'OK',
          searchInputCount:searchInputs().length,
          searchButtonCount:searchButtons().length,
          searchValueMatches:!!input && text(input.value).toUpperCase()===so,
          rowCount:resultRows(so).length,
          rowActionCount:rowActions(row).length,
          updateDialogCount:updateDialogs().length,
          quantityInputCount:quantityInputs(dlg).length,
          updateButtonCount:updateButtons(dlg).length,
          quantityMatches:!!qty && validCount(count) && Number(qty.value)===count,
          confirmDialogCount:yesDialogs().length,
          confirmYesCount:yesButtons(notify).length
        };
      };
      const perform=(action,so,count)=>{
        const p=evidence(so,count);
        if(!p.ok) return {ok:false,reason:p.reason};
        if(action==='SEARCH_INPUT') {
          const input=one(searchInputs());
          if(!input || updateDialogs().length || yesDialogs().length)
            return {ok:false,reason:'SEARCH_INPUT_AMBIGUOUS'};
          const setter=Object.getOwnPropertyDescriptor(
            HTMLInputElement.prototype,'value')?.set;
          if(!setter)return {ok:false,reason:'NATIVE_SETTER_UNAVAILABLE'};
          setter.call(input,so);
          input.dispatchEvent(new Event('input',{bubbles:true}));
          input.dispatchEvent(new Event('change',{bubbles:true}));
          return {ok:text(input.value).toUpperCase()===so,
            reason:text(input.value).toUpperCase()===so?'SO_SEARCH_SET':'SO_SEARCH_READBACK_FAILED'};
        }
        if(action==='SEARCH_CLICK') {
          const button=one(searchButtons());
          if(!button || !p.searchValueMatches || updateDialogs().length || yesDialogs().length)
            return {ok:false,reason:'SEARCH_NOT_READY'};
          button.click();return {ok:true,reason:'SEARCH_REQUESTED'};
        }
        if(action==='OPEN_UPDATE') {
          const row=one(resultRows(so));
          const button=one(rowActions(row));
          if(!row || !button || updateDialogs().length || yesDialogs().length)
            return {ok:false,reason:'SO_ROW_OR_ACTION_AMBIGUOUS'};
          button.click();return {ok:true,reason:'SO_UPDATE_DIALOG_REQUESTED'};
        }
        if(action==='SET_QUANTITY') {
          if(!validCount(count))return {ok:false,reason:'QUANTITY_INVALID'};
          const input=one(quantityInputs(one(updateDialogs())));
          if(!input || yesDialogs().length)
            return {ok:false,reason:'QUANTITY_FIELD_AMBIGUOUS'};
          const setter=Object.getOwnPropertyDescriptor(
            HTMLInputElement.prototype,'value')?.set;
          if(!setter)return {ok:false,reason:'NATIVE_SETTER_UNAVAILABLE'};
          setter.call(input,String(count));
          input.dispatchEvent(new Event('input',{bubbles:true}));
          input.dispatchEvent(new Event('change',{bubbles:true}));
          input.dispatchEvent(new Event('blur',{bubbles:true}));
          return {ok:Number(input.value)===count,reason:Number(input.value)===count?
            'QUANTITY_READBACK_OK':'QUANTITY_READBACK_MISMATCH'};
        }
        if(action==='UPDATE') {
          const dlg=one(updateDialogs());
          const input=one(quantityInputs(dlg));
          const button=one(updateButtons(dlg));
          if(!validCount(count) || !dlg || !input || Number(input.value)!==count || !button ||
             yesDialogs().length)
            return {ok:false,reason:'UPDATE_PRECONDITION_FAILED'};
          button.click();return {ok:true,reason:'UPDATE_SUBMITTED'};
        }
        if(action==='CONFIRM_YES') {
          const dlg=one(yesDialogs()), button=one(yesButtons(dlg));
          if(!validCount(count) || !dlg || !button || updateDialogs().length)
            return {ok:false,reason:'YES_DIALOG_NOT_UNIQUE'};
          // Non-idempotent: the caller MUST NOT retry after dispatch/timeout.
          button.click();return {ok:true,reason:'YES_SUBMITTED'};
        }
        return {ok:false,reason:'UNKNOWN_ACTION'};
      };
      window.__supraHandoverV1={peek:evidence,perform};
    })();
    """;
}
