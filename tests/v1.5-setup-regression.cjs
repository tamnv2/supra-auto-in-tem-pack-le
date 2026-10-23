'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const src=fs.readFileSync('WmsWorkflowJs.cs','utf8');
const script=src.split('"""')[1];
const main=fs.readFileSync('MainForm.cs','utf8');
const document={activeElement:null,body:{innerText:'QUẦY ĐÓNG GÓI\nTIẾP THEO'}};
const make=(text,click=()=>{})=>({
  innerText:text,textContent:text,disabled:false,isConnected:true,
  getClientRects:()=>[1],getAttribute:()=>null,click,
  closest:()=>null,querySelectorAll:()=>[]
});
class Input{
  constructor(placeholder=''){this._value='';this.disabled=false;this.placeholder=placeholder;}
  get value(){return this._value;}
  set value(value){this._value=value;}
  getClientRects(){return [1];}
  getAttribute(k){return k==='placeholder'?this.placeholder:null;}
  focus(){document.activeElement=this;}
  select(){}blur(){}dispatchEvent(){}click(){}
}
let mode='none';
const tote=new Input('Quét mã PTVC để đóng gói'),boothInput=new Input();
let confirms=0;
const confirm=make('Xác nhận',()=>{
  assert.equal(boothInput.value,'SDG.22');
  document.body.innerText='QUẦY ĐÓNG GÓI [SDG.22]\nTIẾP THEO';
  confirms++;mode='none';
});
const booth={
  isConnected:true,innerText:'NHẬP MÃ QUẦY ĐÓNG GÓI',parentElement:null,
  getClientRects:()=>[1],closest:()=>null,
  querySelectorAll:sel=>sel.includes('input')?[boothInput]:sel.includes('button')?[confirm]:[]
};
document.querySelectorAll=sel=>{
  if(sel.includes('mat-dialog-container'))return mode==='booth'?[booth]:[];
  if(sel==='app-station-modal')return mode==='component'?[booth]:[];
  if(sel.includes('input'))return [tote];
  return [];
};
const ctx={window:{},document,HTMLInputElement:Input,
 Event:class Event{constructor(type){this.type=type}},
 location:{protocol:'https:',hostname:'wms-supra.winmart.vn',pathname:'/sft3/app/saleorder/packing'},
 getComputedStyle:()=>({display:'block',visibility:'visible'})};
vm.runInNewContext(script,ctx);
const api=ctx.window.__supraPackLeV1;
assert(api && typeof api.setupPeek==='function');
assert.equal(api.setupPeek().boothCorrect,false);
assert.equal(api.setupPerform('BOOTH_PREPARE').ok,true);
assert.equal(document.activeElement,tote);
mode='booth';
assert.equal(api.setupPeek().boothDialog,true);
assert.equal(api.setupPeek().boothInputReady,true);
assert.equal(api.setupPerform('BOOTH_CONFIRM').ok,false);
assert.equal(api.setupPerform('BOOTH_SET').ok,true);
assert.equal(api.setupPeek().boothValueCorrect,true);
assert.equal(api.setupPerform('BOOTH_FOCUS').ok,true);
assert.equal(document.activeElement,boothInput);
assert.equal(api.setupPerform('BOOTH_CONFIRM').ok,true);
assert.equal(confirms,1);
assert.equal(api.setupPeek().boothCorrect,true);
assert.equal(api.setupPerform('BOOTH_PREPARE').ok,false);
mode='component';
assert.equal(api.setupPeek().boothDialog,true);
ctx.location.pathname='/sft3/app/unrelated';
assert.equal(api.setupPerform('BOOTH_SET').ok,false);
assert.doesNotMatch(src,/21-1129-D29|TARGET_PRINTER|PRINTER_PICK/);
assert.match(main,/await _browser\.EnterAsync\(\)/);
assert.match(main,/WmsPrinterCoordinator\.SelectAsync/);
assert.match(main,/if\(!_printerVerified\)/);
assert.match(main,/BOOTH_MODAL_DETECTED/);
assert.match(main,/BOOTH_SUBMIT_ENTER/);
assert.match(main,/BOOTH_READBACK_NOT_SDG22/);
console.log('WMS_STATION_SDG22_WITH_SHARED_PRINTER_FLOW_PASS');
