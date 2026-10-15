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
      window.__supraPackLeV1={peek,perform};
    })();
    """;
}
