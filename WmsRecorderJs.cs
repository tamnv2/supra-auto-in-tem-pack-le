namespace SupraAutoPackLe;

/// <summary>
/// User-initiated DOM event tracing on authenticated WMS pages only.
/// The script is dormant until Begin, and never reads free-text input,
/// password, authentication data, cookies, network payloads or browser storage.
/// </summary>
internal static class WmsRecorderJs
{
    internal const string Script = """
    (() => {
      'use strict';
      if(window.__supraActionsV17)return;
      let active=false;
      let debounce=null;
      const clean=x=>String(x||'').replace(/\s+/g,' ').trim();
      const redact=x=>clean(x)
        .replace(/[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}/g,'[EMAIL]')
        .replace(/\b(?:\+?84|0)[0-9.\s-]{8,13}\b/g,'[PHONE]')
        .replace(/\b[A-Za-z0-9_-]{32,}\b/g,'[IDENTIFIER]')
        .replace(/(password|passwd|cookie|token|bearer|authorization|secret|mật khẩu)\s*[:=]?\s*\S+/gi,'[REDACTED]');
      const restricted=t=>/password|passwd|token|secret|authorization|cookie|otp|one.time|mật.khẩu|đăng.nhập|login|auth/i.test(t||'');
      const inAuthenticatedWms=()=>{
        return location.protocol==='https:' && location.hostname==='wms-supra.winmart.vn' &&
          location.pathname.startsWith('/sft3/app/') && !/login/i.test(location.pathname);
      };
      const send=o=>{
        if(!active||!inAuthenticatedWms())return;
        try{window.chrome?.webview?.postMessage(JSON.stringify(o));}catch(_){}
      };
      const hidden=e=>!!(e && e.closest && e.closest(
        'input[type="password"],[autocomplete="current-password"],[autocomplete="one-time-code"],[data-sensitive],[type="hidden"]'));
      const elementPath=e=>{
        const names=[];
        for(let el=e,i=0;el&&el.tagName&&i<5;el=el.parentElement,i++){
          let s=el.tagName.toLowerCase();
          if(el.id&&/^[a-zA-Z][a-zA-Z0-9_-]{0,35}$/.test(el.id)&&
             !restricted(el.id)) { names.unshift(s+'#'+el.id);break; }
          const css=Array.from(el.classList||[]).filter(x=>
            /^[a-zA-Z][a-zA-Z0-9_-]{0,30}$/.test(x)&&!restricted(x)).slice(0,2);
          if(css.length)s+='.'+css.join('.');
          const siblings=el.parentElement&&Array.from(el.parentElement.children)
             .filter(x=>x.tagName===el.tagName);
          if(siblings&&siblings.length>1)s+=':nth-of-type('+(siblings.indexOf(el)+1)+')';
          names.unshift(s);
        }
        return names.join(' > ').slice(0,230);
      };
      const selectionValue=el=>{
        if(!el)return '';
        const raw=el.matches('input,select,textarea') ?
          String(el.value||'') : String(el.getAttribute('aria-label')||el.innerText||'');
        const value=clean(raw);
        if(/^SDG\.\d{1,4}$/i.test(value))return value;
        if(/^\d{2}-\d{3,6}-[a-zA-Z0-9]{2,8}\s*\((Online|Offline)\)$/i.test(value))return value;
        return ''; // No tote, order, account, free text or arbitrary field values.
      };
      const interestingCaption=s=>/quầy đóng gói|mã quầy|máy in|printer|xác nhận|đóng gói|tiếp theo|in nhãn|hoàn thành|hàng chẵn/i.test(s);
      const captionOf=el=>{
        if(el.matches('input,textarea'))return '';
        let s=redact(el.innerText||el.textContent||'').slice(0,155);
        if(restricted(s))return '[ACCOUNT/AUTH CONTROL]';
        if(el.matches('[role="option"],mat-option'))return interestingCaption(s)?s:'[option]';
        return s;
      };
      const describe=(target,kind,key='')=>{
        if(!target||!target.tagName||hidden(target))return;
        const el=target.closest('button,a,label,select,mat-option,[role="button"],[role="menuitem"],[role="option"],[role="combobox"],input')||target;
        if(hidden(el))return;
        const role=clean(el.getAttribute('role')).slice(0,38);
        const type=clean(el.getAttribute('type')).slice(0,30);
        const label=redact(el.getAttribute('aria-label')||el.getAttribute('placeholder')||el.getAttribute('title')).slice(0,140);
        send({kind,tag:el.tagName.toLowerCase(),role,type,
          selector:elementPath(el),text:captionOf(el),label:restricted(label)?'[SENSITIVE]':label,
          key,selection:selectionValue(el)});
      };
      function snapshot(reason){
        if(!active||!inAuthenticatedWms())return;
        const list=[];
        document.querySelectorAll('mat-dialog-container,[role="dialog"],h1,h2,h3,h4,button,[role="button"],label,mat-select,mat-option').forEach(el=>{
          if(list.length>=28||hidden(el)||el.getClientRects().length===0)return;
          const s=redact(el.matches('mat-dialog-container,[role="dialog"]')?
             (el.querySelector('h1,h2,h3,h4')?.textContent||''):(el.innerText||el.textContent||''));
          if(!interestingCaption(s)||restricted(s))return;
          const title=s.slice(0,75);
          const selector=elementPath(el).slice(0,95);
          list.push(title+' @ '+selector);
        });
        send({kind:'snapshot',snapshot:('reason='+reason+'; '+list.join(' | ')).slice(0,1750)});
      }
      document.addEventListener('click',e=>describe(e.target,'click'),true);
      document.addEventListener('pointerdown',e=>describe(e.target,'pointerdown'),true);
      document.addEventListener('focusin',e=>{
        if(e.target?.matches('input,select,button,mat-select,[role="combobox"]'))
          describe(e.target,'focus');
      },true);
      document.addEventListener('keydown',e=>{
        if(['Enter','Escape','Tab','ArrowUp','ArrowDown'].includes(e.key))
          describe(e.target,'keydown',e.key);
      },true);
      document.addEventListener('change',e=>{
        if(e.target?.matches('input,select,[role="combobox"],mat-select'))
          describe(e.target,'change');
      },true);
      window.addEventListener('beforeprint',()=>send({kind:'print_start'}));
      window.addEventListener('afterprint',()=>send({kind:'print_finish'}));
      const observer=new MutationObserver(()=>{
        if(!active)return;
        clearTimeout(debounce);
        debounce=setTimeout(()=>snapshot('DOM updated'),700);
      });
      const observe=()=>{
        if(document.documentElement)observer.observe(document.documentElement,{childList:true,subtree:true});
      };
      if(document.documentElement)observe();
      else document.addEventListener('DOMContentLoaded',observe,{once:true});
      window.__supraActionsV17={
        setActive:on=>{active=!!on;if(active)snapshot('Begin recording');},
        snapshot:reason=>snapshot(String(reason||'manual').slice(0,45))
      };
    })();
    """;
}
