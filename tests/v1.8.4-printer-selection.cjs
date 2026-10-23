'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const src=fs.readFileSync('WmsPrinterSetupJs.cs','utf8');
const script=src.split('"""')[1];
const main=fs.readFileSync('MainForm.cs','utf8');
const even=fs.readFileSync('TemChanPanel.cs','utf8');
const coordinator=fs.readFileSync('WmsPrinterCoordinator.cs','utf8');
assert.ok(script);
assert.doesNotMatch(script,/21-1129-D29|TARGET_PRINTER/);
assert.doesNotMatch(script,/document\.cookie|localStorage|sessionStorage|password\.value/);
assert.match(main,/RunTemChanPrinterSetupAsync/);
assert.match(main,/WmsPrinterCoordinator\.SelectAsync/);
assert.match(even,/SetPrinterReady\(bool ready/);
assert.match(even,/_so\.Enabled=false/);
assert.match(coordinator,/PrinterSelectionForm/);
assert.match(coordinator,/choice!=previous/);
assert.match(coordinator,/ActionAsync\(browser,active,"CLOSE",choice\)/);

function fixture(path='/sft3/app/saleorder/list',original='21-1129-D28 (Online)'){
  const names=['21-1129-D28 (Online)','21-1129-D29 (Online)','21-1130-D31 (Offline)'];
  let current=original,saved=original,account=false,modal=false,expanded=false,confirms=0,closes=0;
  const make=(name,click=()=>{})=>({
    tagName:'BUTTON',innerText:name,textContent:name,disabled:false,isConnected:true,
    getClientRects:()=>[1],getAttribute:()=>null,click,
    closest:()=>null,querySelectorAll:()=>[]
  });
  const input=make('');
  input.tagName='INPUT';input.value=original;
  input.click=()=>{expanded=true;};
  const options=names.map(name=>{
    const x=make(name,()=>{current=name;input.value=name;expanded=false;});
    x.tagName='MAT-OPTION';return x;
  });
  const confirm=make('Xác nhận',()=>{assert.notEqual(current,saved);saved=current;confirms++;modal=false;});
  Object.defineProperty(confirm,'disabled',{get:()=>current===saved});
  const close=make('Đóng',()=>{closes++;modal=false;});
  const dialog={
    innerText:'CHỌN MÁY IN',parentElement:null,isConnected:true,
    getClientRects:()=>[1],closest:()=>null,
    querySelectorAll:q=>q.includes('app-printer-modal')?[input]:
      q.includes('button')?[confirm,close]:[]
  };
  const accountButton=make('',()=>{account=true;});
  const menu=make('printer Thiết lập máy in',()=>{account=false;modal=true;input.value=saved;current=saved;expanded=false;});
  const doc={
    querySelector:q=>{
      const result=doc.querySelectorAll(q);
      return result.length?result[0]:null;
    },
    querySelectorAll:q=>{
      if(q.includes('mat-dialog-container'))return modal?[dialog]:[];
      if(q.includes('mat-toolbar#app-header'))return [accountButton];
      if(q.includes('role="menu"'))return account?[menu]:[];
      if(q.includes('.cdk-overlay-container mat-option'))return expanded?options:[];
      if(q.includes('cdk-overlay-container .cdk-virtual-scroll-viewport'))return [];
      return [];
    }
  };
  const ctx={window:{},document:doc,Event:class Event{constructor(type){this.type=type}},
    location:{protocol:'https:',hostname:'wms-supra.winmart.vn',pathname:path},
    getComputedStyle:()=>({display:'block',visibility:'visible'})};
  vm.runInNewContext(script,ctx);
  return {api:ctx.window.__supraPrinterV1,ctx,
    counts:()=>({confirms,closes}),saved:()=>saved};
}
for(const page of ['/sft3/app/saleorder/list','/sft3/app/saleorder/packing']){
  const f=fixture(page),api=f.api;
  assert.equal(api.peek().ok,true);
  assert.equal(api.perform('ACCOUNT_OPEN').ok,true);
  assert.equal(api.peek().settingsVisible,true);
  assert.equal(api.perform('PRINTER_MENU').ok,true);
  assert.equal(api.peek().currentPrinter,'21-1129-D28 (Online)');
  assert.equal(api.perform('EXPAND').ok,true);
  assert.equal(api.peek().scanDone,true);
  assert.equal(api.peek().options.length,3);
  assert.equal(api.perform('PICK','Unknown Printer').ok,false);
  assert.equal(api.perform('PICK','21-1129-D29 (Online)').ok,true);
  assert.equal(api.peek().currentPrinter,'21-1129-D29 (Online)');
  assert.equal(api.peek().confirmEnabled,true);
  assert.equal(api.perform('CONFIRM','21-1129-D29 (Online)').ok,true);
  assert.equal(f.counts().confirms,1);
  assert.equal(api.peek().printerDialog,false);
  assert.equal(api.perform('ACCOUNT_OPEN').ok,true);
  assert.equal(api.perform('PRINTER_MENU').ok,true);
  assert.equal(api.peek().currentPrinter,'21-1129-D29 (Online)');
  assert.equal(api.peek().confirmEnabled,false);
  assert.equal(api.peek().closeEnabled,true);
  assert.equal(api.perform('CLOSE','21-1129-D29 (Online)').ok,true);
  assert.equal(f.counts().confirms,1);
  assert.equal(f.counts().closes,1);
}
{
  const f=fixture(),api=f.api;
  api.perform('ACCOUNT_OPEN');api.perform('PRINTER_MENU');api.perform('EXPAND');
  assert.equal(api.peek().options.length,3);
  // Explicitly choosing the unchanged printer must close; disabled Confirm isn't clicked.
  assert.equal(api.peek().confirmEnabled,false);
  assert.equal(api.perform('CLOSE','21-1129-D28 (Online)').ok,true);
  assert.equal(f.counts().confirms,0);
  assert.equal(f.counts().closes,1);
}
{
  const f=fixture('/sft3/app/saleorder/details/TEST123');
  assert.equal(f.api.peek().ok,false);
  assert.equal(f.api.perform('ACCOUNT_OPEN').ok,false);
}
console.log('PRINTER_DYNAMIC_CHOICES_TWO_ROUTES_CONFIRM_OR_CLOSE_PASS');
