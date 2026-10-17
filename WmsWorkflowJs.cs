namespace SupraAutoPackLe;

internal static class WmsWorkflowJs
{
    // Operates only semantic DOM elements on the exact WMS packing page.
    // No cookies, tokens, HTTP request/response, CDP Network or DOM dumps.
    internal const string Script = """
    (() => {
      'use strict';
      if (window.__supraPackLeV1) return;
      const allowed = () => location.protocol === 'https:' &&
        location.hostname.toLowerCase() === 'wms-supra.winmart.vn' &&
        location.pathname.replace(/\/+$/,'') === '/sft3/app/saleorder/packing';
      const clean = v => String(v??'').replace(/\s+/g,' ').trim();
      const visible = e => !!e && e.getClientRects().length>0 &&
        getComputedStyle(e).display!=='none' && getComputedStyle(e).visibility!=='hidden';
      const enabled = e => visible(e) && !e.disabled && e.getAttribute('aria-disabled')!=='true';
      const unique = a => a.length===1?a[0]:null;
      const dialogs=()=>Array.from(document.querySelectorAll('mat-dialog-container,[role="dialog"]'))
        .filter(visible).filter(e=>!e.parentElement?.closest('mat-dialog-container,[role="dialog"]'));
      const dialog=()=>unique(dialogs());
      const name=e=>clean(e.getAttribute('aria-label')||e.innerText||e.textContent||e.getAttribute('title')||'');
      const buttons=root=>Array.from(root.querySelectorAll('button,[role="button"]')).filter(enabled);
      const byName=(n,exact=true)=>buttons(document).filter(e=>exact?name(e)===n:name(e).startsWith(n));
      const inDialog=n=>{const d=dialog();return d?buttons(d).filter(e=>name(e)===n):[]};
      // A confirmation dialog may be reused by WMS. Verify its actual meaning,
      // not whether its DOM element identity changed after the preceding "Co".
      // Only a boolean is returned to the application; no order or dialog text is logged.
      const outboundSlipPrompt=()=>{
        const d=dialog();
        return !!d && /bạn\s+có\s+muốn\s+in\s+phiếu\s+xuất\s+kho/i.test(clean(d.innerText));
      };
      const input=keyword=>Array.from(document.querySelectorAll('input')).filter(e=>enabled(e)&&
        clean(e.getAttribute('placeholder')).toLocaleLowerCase('vi').includes(keyword.toLocaleLowerCase('vi')));
      const click=list=>{const el=unique(list);if(!el)return {ok:false,reason:list.length?'AMBIGUOUS':'NOT_FOUND'};el.click();return {ok:true};};
      const printButtons=()=>buttons(document).filter(e=>(
        name(e).toLowerCase()==='print'||clean(e.getAttribute('title')).toLowerCase()==='print') &&
        (e.classList.contains('app-table-action-button') || e.closest('app-btn-row,app-so-packing-confirm')));
      const extract=caption=>{
        // Only two purpose-bound business values. Never export any other WMS text.
        const re=caption==='Mã SO'?/Mã\s*SO\s*:?\s*([A-Za-z0-9_-]{5,50})/i:/Mã\s*DO\s*:?\s*([A-Za-z0-9_-]{5,50})/i;
        const nodes=Array.from(document.querySelectorAll('label,span,p,td,small,strong'))
          .filter(e=>visible(e)&&clean(e.textContent).replace(/:$/,'')===caption);
        for(const el of nodes){
          for(let n=el.parentElement,depth=0;n&&depth<4;n=n.parentElement,depth++){
            const m=clean(n.innerText).slice(0,380).match(re);if(m)return m[1];
          }
          const sibling=clean(el.nextElementSibling?.innerText);
          if(/^[A-Za-z0-9_-]{5,50}$/.test(sibling))return sibling;
        }
        const body=clean(document.body?.innerText);
        const index=body.toUpperCase().indexOf('THÔNG TIN SO ĐANG ĐÓNG GÓI');
        if(index<0)return '';
        const m=body.slice(index,index+1100).match(re);return m?m[1]:'';
      };
      const setField=(el,value)=>{
        const setter=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value')?.set;
        if(!setter)return {ok:false,reason:'NO_NATIVE_SETTER'};
        el.focus();el.select?.();
        setter.call(el,'');el.dispatchEvent(new Event('input',{bubbles:true}));
        setter.call(el,value);el.dispatchEvent(new Event('input',{bubbles:true}));
        el.dispatchEvent(new Event('change',{bubbles:true}));
        return el.value===value?{ok:true}:{ok:false,reason:'COUNT_READBACK_MISMATCH'};
      };

      // Start-of-shift setup. All decisions are anchored to observed dialog captions;
      // the code never reads password, cookies, tokens or account identifiers.
      const setupDialog=caption=>dialogs().filter(d=>clean(d.innerText).toLocaleUpperCase('vi').includes(caption));
      const boothDialog=()=>unique(setupDialog('NHẬP MÃ QUẦY ĐÓNG GÓI'));
      const printerDialog=()=>unique(setupDialog('CHỌN MÁY IN'));
      const printerSelector=d=>d?unique(Array.from(d.querySelectorAll('mat-select,[role="combobox"],select'))
        .filter(enabled)) : null;
      const printerOption=()=>Array.from(document.querySelectorAll('mat-option,[role="option"]'))
        .filter(enabled).filter(e=>name(e)==='21-1129-D29 (Online)');
      const setupActions=()=>Array.from(document.querySelectorAll('button,[role="button"],a,[role="menuitem"]'))
        .filter(enabled);
      const printerMenuItem=()=>setupActions().filter(e=>name(e)==='Thiết lập máy in');
      const accountTriggers=()=>{
        // Never click an unlabelled icon or infer a user from an index.
        const elements=setupActions().filter(e=>
          /(?:^|\s)Kho\s+.+/i.test(name(e)) && /\s-\s/.test(name(e)));
        return elements;
      };
      const setupPeek=()=>{
        if(!allowed())return {ok:false,reason:'WRONG_WMS_ROUTE'};
        const b=boothDialog(),p=printerDialog(),select=printerSelector(p);
        const selected=select?clean(select.innerText||select.value||''):'';
        const empty=selected==='Chọn'||selected===''||selected==='-- Chọn --';
        const options=printerOption();
        const ready=input('Quét mã PTVC để đóng gói').length===1 && dialogs().length===0;
        return {
          ok:true,reason:'OK',boothDialog:!!b,printerDialog:!!p,
          packingReady:ready,accountTriggerCount:accountTriggers().length,
          settingsVisible:printerMenuItem().length===1,
          printerSelected:!!p&&!!select&&!empty,
          printerEmpty:!!p&&!!select&&empty,printerOptionVisible:options.length===1
        };
      };
      const setupPerform=(action)=>{
        if(!allowed())return {ok:false,reason:'WRONG_WMS_ROUTE'};
        const b=boothDialog(),p=printerDialog();
        if(action==='BOOTH_SET'){
          if(!b)return {ok:false,reason:'BOOTH_DIALOG_MISSING'};
          const inputs=Array.from(b.querySelectorAll('input')).filter(enabled)
            .filter(e=>e.type!=='password');
          const el=unique(inputs);if(!el)return {ok:false,reason:'BOOTH_INPUT_NOT_UNIQUE'};
          return setField(el,'SDG.24');
        }
        if(action==='BOOTH_CONFIRM'){
          if(!b)return {ok:false,reason:'BOOTH_DIALOG_MISSING'};
          const inputs=Array.from(b.querySelectorAll('input')).filter(enabled).filter(e=>e.type!=='password');
          if(inputs.length!==1||inputs[0].value!=='SDG.24')
            return {ok:false,reason:'BOOTH_VALUE_NOT_CONFIRMED'};
          return click(buttons(b).filter(e=>name(e)==='Xác nhận'));
        }
        if(action==='ACCOUNT_OPEN'){
          if(dialogs().length)return {ok:false,reason:'UNEXPECTED_DIALOG'};
          return click(accountTriggers());
        }
        if(action==='PRINTER_MENU'){
          if(dialogs().length)return {ok:false,reason:'UNEXPECTED_DIALOG'};
          return click(printerMenuItem());
        }
        if(action==='PRINTER_EXPAND'){
          if(!p)return {ok:false,reason:'PRINTER_DIALOG_MISSING'};
          const selector=printerSelector(p);
          if(!selector)return {ok:false,reason:'PRINTER_SELECTOR_NOT_UNIQUE'};
          const selected=clean(selector.innerText||selector.value||'');
          if(selected!==''&&selected!=='Chọn'&&selected!=='-- Chọn --')
            return {ok:false,reason:'PRINTER_ALREADY_SELECTED'};
          selector.click();return {ok:true};
        }
        if(action==='PRINTER_PICK'){
          if(!p)return {ok:false,reason:'PRINTER_DIALOG_MISSING'};
          return click(printerOption());
        }
        if(action==='PRINTER_CONFIRM'){
          if(!p)return {ok:false,reason:'PRINTER_DIALOG_MISSING'};
          const selector=printerSelector(p);
          if(!selector||!clean(selector.innerText||selector.value||'').includes('21-1129-D29'))
            return {ok:false,reason:'PRINTER_SELECTION_NOT_CONFIRMED'};
          return click(buttons(p).filter(e=>name(e)==='Xác nhận'));
        }
        if(action==='PRINTER_CLOSE'){
          if(!p)return {ok:false,reason:'PRINTER_DIALOG_MISSING'};
          const selector=printerSelector(p);
          const selected=clean(selector?.innerText||selector?.value||'');
          if(!selector||selected===''||selected==='Chọn'||selected==='-- Chọn --')
            return {ok:false,reason:'PRINTER_NOT_SELECTED'};
          return click(buttons(p).filter(e=>name(e)==='Đóng'));
        }
        return {ok:false,reason:'UNKNOWN_SETUP_ACTION'};
      };
      const peek=()=>{
        if(!allowed())return {ok:false,reason:'WRONG_WMS_ROUTE'};
        const d=dialog();
        const tote=input('Quét mã PTVC để đóng gói');
        const pack=byName('ĐÓNG GÓI HÀNG CHẴN');
        const qty=d?input('Số lượng').filter(e=>d.contains(e)):[];
        const finish=byName('HOÀN THÀNH ĐÓNG GÓI',false);
        const snap={
          ok:true,reason:'OK',
          ready:tote.length===1 && pack.length===0 && dialogs().length===0,
          toteCount:tote.length,packCount:pack.length,qtyCount:qty.length,
          printCount:printButtons().length,finishCount:finish.length,
          updateCount:inDialog('Cập Nhật').length,yesCount:inDialog('Có').length,
          noCount:inDialog('Không').length,printLabelCount:inDialog('In Nhãn').length,
          outboundSlipPrompt:outboundSlipPrompt(),
          dialogCount:dialogs().length,
          so:extract('Mã SO'),do:extract('Mã DO')
        };
        return snap;
      };
      const perform=(action,value)=>{
        if(!allowed())return {ok:false,reason:'WRONG_WMS_ROUTE'};
        switch(action){
          case 'TOTE': {
            if(dialogs().length)return {ok:false,reason:'UNEXPECTED_DIALOG'};
            const el=unique(input('Quét mã PTVC để đóng gói'));
            return el?setField(el,String(value)):{ok:false,reason:'TOTE_INPUT_NOT_UNIQUE'};
          }
          case 'PACK':return click(byName('ĐÓNG GÓI HÀNG CHẴN'));
          case 'COUNT': {
            const d=dialog();if(!d)return {ok:false,reason:'COUNT_DIALOG_MISSING'};
            const el=unique(input('Số lượng').filter(e=>d.contains(e)));
            if(!el)return {ok:false,reason:'COUNT_INPUT_NOT_UNIQUE'};
            const s=String(value);
            if(!/^[1-9][0-9]{0,3}$/.test(s))return {ok:false,reason:'INVALID_COUNT'};
            const r=setField(el,s);
            if(!r.ok)return r;
            el.blur();
            return el.value===s?{ok:true}:{ok:false,reason:'COUNT_BLUR_MISMATCH'};
          }
          case 'UPDATE':return click(inDialog('Cập Nhật'));
          case 'YES':return click(inDialog('Có'));
          case 'NO': {
            // The first Yes/No dialog and the outbound-slip question may share
            // the same DOM node. Only click No on the confirmed slip question.
            if(!outboundSlipPrompt())return {ok:false,reason:'OUTBOUND_SLIP_PROMPT_MISMATCH'};
            return click(inDialog('Không'));
          }
          case 'PRINT':return click(printButtons());
          case 'PRINT_LABEL':return click(inDialog('In Nhãn'));
          case 'FINISH':return click(byName('HOÀN THÀNH ĐÓNG GÓI',false));
          default:return {ok:false,reason:'UNKNOWN_ACTION'};
        }
      };
      window.__supraPackLeV1={peek,perform,setupPeek,setupPerform};
    })();
    """;
}
