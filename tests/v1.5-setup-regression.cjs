'use strict';
const fs=require('node:fs');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const src=fs.readFileSync('WmsWorkflowJs.cs','utf8');
const script=src.slice(src.indexOf('"""')+3,src.lastIndexOf('"""'));
const main=fs.readFileSync('MainForm.cs','utf8');
const clicks={booth:0,account:0,menu:0,expand:0,pick:0,confirm:0};
const document={activeElement:null,body:{innerText:'QUẦY ĐÓNG GÓI\nTIẾP THEO'}};
const el=(label,click=()=>{},isMenu=false)=>({
 innerText:label,disabled:false,getClientRects:()=>[1],
 getAttribute:()=>null,click,closest:sel=>(isMenu && sel.includes('mat-menu-content'))?{}:null
});
class Input {
 constructor(placeholder=''){this._value='';this.disabled=false;this.type='text';this.placeholder=placeholder;}
 get value(){return this._value;}
 set value(v){this._value=v;}
 getClientRects(){return [1];}
 getAttribute(k){return k==='placeholder'?this.placeholder:null;}
 focus(){document.activeElement=this;}
 select(){} blur(){} dispatchEvent(){}
 click(){}
}
class Select{}
const tote=new Input('Quét mã PTVC để đóng gói');
const boothInput=new Input('');
const printerInput=new Input('');
printerInput.value='21-1129-D28 (Online)'; // Wrong machine should be repaired only after exact readback.
let modal='none',accountOpened=false,optionsShown=false;
const boothConfirm=el('Xác nhận',()=>{
 assert.equal(boothInput.value,'SDG.22');
 clicks.booth++;
 document.body.innerText='QUẦY ĐÓNG GÓI [SDG.22]\nTIẾP THEO';
 modal='none';
});
const printerConfirm=el('Xác nhận',()=>{clicks.confirm++;modal='none';});
const printerOption=el('21-1129-D29 (Online)',()=>{
 clicks.pick++;printerInput.value='21-1129-D29 (Online)';optionsShown=false;
});
printerInput.click=()=>{clicks.expand++;optionsShown=true;};
const booth={
 innerText:'NHẬP MÃ QUẦY ĐÓNG GÓI',parentElement:null,getClientRects:()=>[1],
 closest:()=>null,
 querySelectorAll:sel=>sel.includes('input')?[boothInput]:sel.includes('button')?[boothConfirm]:[]
};
const printer={
 innerText:'CHỌN MÁY IN',parentElement:null,getClientRects:()=>[1],
 querySelectorAll:sel=>sel.includes('app-printer-modal input')?[printerInput]:
  sel.includes('button')?[printerConfirm]:[]
};
const account=el('',()=>{clicks.account++;accountOpened=true;});
const menu=el('printer Thiết lập máy in',()=>{clicks.menu++;modal='printer';accountOpened=false;},true);
document.querySelectorAll=sel=>{
 if(sel.includes('mat-dialog-container'))return modal==='booth'?[booth]:modal==='printer'?[printer]:[];
 if(sel==='app-station-modal')return modal==='booth-component'?[booth]:[];
 if(sel.startsWith('mat-toolbar#app-header'))return [account];
 if(sel.includes('mat-option'))return optionsShown?[printerOption]:[];
 if(sel.includes('button,[role="button"],a'))return accountOpened?[menu]:[];
 if(sel.includes('input'))return [tote];
 return [];
};
const ctx={window:{},document,HTMLInputElement:Input,HTMLSelectElement:Select,
 Event:class Event{constructor(type,props){this.type=type;this.bubbles=props?.bubbles}},
 location:{protocol:'https:',hostname:'wms-supra.winmart.vn',pathname:'/sft3/app/saleorder/packing'},
 getComputedStyle:()=>({display:'block',visibility:'visible'})};
vm.runInNewContext(script,ctx,{timeout:3000});
const api=ctx.window.__supraPackLeV1;
assert(api && typeof api.setupPeek==='function');
assert.equal(api.setupPeek().boothCorrect,false);
assert.equal(api.setupPeek().boothHeaderKnown,true);
assert.equal(api.setupPerform('ACCOUNT_OPEN').ok,false,'Printer menu must be gated on correct station');
assert.equal(api.setupPerform('BOOTH_PREPARE').ok,true);
assert.equal(document.activeElement,tote);
modal='booth'; // WebView2.EnterAsync triggers dialog on the real WMS.
assert.equal(api.setupPeek().boothDialog,true);
assert.equal(api.setupPeek().boothInputReady,true);
assert.equal(api.setupPeek().boothValueCorrect,false);
boothConfirm.disabled=true;
assert.equal(api.setupPeek().boothConfirmEnabled,false,
  'Disabled confirmation should permit the recorded Enter fallback');
assert.equal(api.setupPerform('BOOTH_CONFIRM').ok,false,'Must never confirm empty booth');
assert.equal(api.setupPerform('BOOTH_SET').ok,true);
assert.equal(boothInput.value,'SDG.22');
assert.equal(api.setupPeek().boothValueCorrect,true);
assert.equal(api.setupPerform('BOOTH_FOCUS').ok,true);
assert.equal(document.activeElement,boothInput);
boothConfirm.disabled=false;
assert.equal(api.setupPeek().boothConfirmEnabled,true);
assert.equal(api.setupPerform('BOOTH_CONFIRM').ok,true);
assert.equal(api.setupPeek().boothCorrect,true);
assert.equal(clicks.booth,1);
assert.equal(api.setupPerform('BOOTH_PREPARE').ok,false,'Do not re-enter a correct station');
assert.equal(api.setupPeek().accountTriggerCount,1);
assert.equal(api.setupPerform('ACCOUNT_OPEN').ok,true);
assert.equal(api.setupPeek().settingsVisible,true);
assert.equal(api.setupPerform('PRINTER_MENU').ok,true);
assert.equal(api.setupPeek().printerWrong,true);
assert.equal(api.setupPeek().printerCorrect,false);
assert.equal(api.setupPerform('PRINTER_CLOSE').ok,false,'Wrong printer must never count as verified');
assert.equal(api.setupPerform('PRINTER_EXPAND').ok,true);
assert.equal(api.setupPeek().printerEmpty,true);
assert.equal(api.setupPeek().printerOptionVisible,true);
assert.equal(api.setupPerform('PRINTER_PICK').ok,true);
assert.equal(api.setupPeek().printerCorrect,true);
assert.equal(api.setupPerform('PRINTER_CONFIRM').ok,true);
assert.equal(clicks.confirm,1);
assert.equal(api.setupPerform('ACCOUNT_OPEN').ok,true);
assert.equal(api.setupPerform('PRINTER_MENU').ok,true);
assert.equal(api.setupPeek().printerCorrect,true,'D29 must still be selected on reopening modal');
assert.equal(api.setupPerform('PRINTER_CLOSE').ok,true,'Confirm-only dialog must close with verified D29');
assert.equal(clicks.confirm,2);
ctx.location.pathname='/sft3/app/unrelated';
assert.equal(api.setupPerform('ACCOUNT_OPEN').ok,false);
assert.equal(api.setupPerform('PRINTER_CONFIRM').ok,false);
assert.match(main,/if\(p\.packingReady&&_printerVerified&&!printerSelectionNeedsRecheck\)/);
assert.match(main,/HideBrowser\(\);\s*FocusToteIfIdle\(\);/);
assert.match(main,/private void ShowBrowser\(\)/);
assert.match(main,/private void HideBrowser\(\)/);
assert.match(main,/SaveColumnWidths\(\)/);
assert.match(main,/BOOTH_MODAL_DETECTED/);
assert.match(main,/BOOTH_SUBMIT_ENTER/);
assert.match(main,/if\(!p\.boothCorrect\)/);
assert.match(main,/BOOTH_READBACK_NOT_SDG22/);
assert.match(main,/await _browser\.EnterAsync\(\)/);
assert.doesNotMatch(src,/SDG\.24/);
assert.doesNotMatch(src,/(document\.cookie|localStorage|getCookies|authorization:)/i);
// Regression: station prompt is already visible on initial packing page load;
 // the caller must detect it without requiring Enter in the Tote field.
ctx.location.pathname='/sft3/app/saleorder/packing';
document.body.innerText='QUẦY ĐÓNG GÓI\nTIẾP THEO';
boothInput.value='';
modal='booth';
assert.equal(api.setupPeek().boothDialog,true);
assert.equal(api.setupPeek().boothInputReady,true);
assert.equal(api.setupPerform('BOOTH_SET').ok,true);
assert.equal(api.setupPeek().boothValueCorrect,true);
// With an Enter-only station confirmation, the native control must remain focused.
assert.equal(api.setupPerform('BOOTH_FOCUS').ok,true);
assert.equal(document.activeElement,boothInput);
modal='booth-component';
assert.equal(api.setupPeek().boothDialog,true,
  'Detect unique station component when WMS modal structure changes');
console.log('WMS_SETUP_SD22_D29_TWO_STATION_ENTRY_MODES_PASS');
