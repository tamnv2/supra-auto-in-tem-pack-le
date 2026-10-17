'use strict';
const fs=require('node:fs');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const src=fs.readFileSync('WmsWorkflowJs.cs','utf8');
const script=src.slice(src.indexOf('"""')+3,src.lastIndexOf('"""'));
const clicks={booth:0,account:0,menu:0,printerOpen:0,printerPick:0,printerConfirm:0,printerClose:0};
const node=(label,fn=()=>{})=>({
 innerText:label,disabled:false,parentElement:null,
 getClientRects:()=>[1],getAttribute:()=>null,click:fn,
 querySelectorAll:()=>[]
});
class Input {
 constructor(){this._value='';this.disabled=false;this.type='text';this.parentElement=null;}
 get value(){return this._value}
 set value(v){this._value=v}
 getClientRects(){return [1]}
 getAttribute(){return null}
 focus(){} select(){} blur(){} dispatchEvent(){}
}
const input=new Input();
let dialogType='booth',accountOpened=false,optionsShown=false;
const boothConfirm=node('Xác nhận',()=>{clicks.booth++;dialogType='none'});
const printerCombo=node('Chọn',()=>{clicks.printerOpen++;optionsShown=true});
const printerOption=node('21-1129-D29 (Online)',()=>{
 clicks.printerPick++;printerCombo.innerText='21-1129-D29 (Online)';optionsShown=false;
});
const printerConfirm=node('Xác nhận',()=>{clicks.printerConfirm++;dialogType='none'});
const printerClose=node('Đóng',()=>{clicks.printerClose++;dialogType='none'});
const booth={
 innerText:'NHẬP MÃ QUẦY ĐÓNG GÓI',parentElement:null,getClientRects:()=>[1],
 querySelectorAll:sel=>sel.includes('input')?[input]:sel.includes('button')?[boothConfirm]:[]
};
const printer={
 innerText:'CHỌN MÁY IN',parentElement:null,getClientRects:()=>[1],
 querySelectorAll:sel=>sel.includes('combobox')?[printerCombo]:sel.includes('button')?[printerConfirm,printerClose]:[]
};
const account=node('userABC - Kho Hưng Yên 1',()=>{clicks.account++;accountOpened=true});
const menu=node('Thiết lập máy in',()=>{clicks.menu++;dialogType='printer';accountOpened=false});
const document={
 body:{innerText:''},
 querySelectorAll:sel=>{
   if(sel.includes('mat-dialog-container'))return dialogType==='booth'?[booth]:dialogType==='printer'?[printer]:[];
   if(sel.includes('mat-option'))return optionsShown?[printerOption]:[];
   if(sel.includes('button'))return dialogType==='none'?(accountOpened?[account,menu]:[account]):[];
   if(sel.includes('input'))return [];
   return [];
 }
};
const ctx={window:{},document,HTMLInputElement:Input,Event:class Event{constructor(type,props){this.type=type;this.bubbles=props?.bubbles}},
 location:{protocol:'https:',hostname:'wms-supra.winmart.vn',pathname:'/sft3/app/saleorder/packing'},
 getComputedStyle:()=>({display:'block',visibility:'visible'})};
vm.runInNewContext(script,ctx,{timeout:3000});
const api=ctx.window.__supraPackLeV1;
assert.equal(api.setupPeek().boothDialog,true);
assert.equal(api.setupPerform('BOOTH_CONFIRM').ok,false,'No confirm before booth code');
assert.equal(api.setupPerform('BOOTH_SET').ok,true);
assert.equal(input.value,'SDG.24');
assert.equal(api.setupPerform('BOOTH_CONFIRM').ok,true);
assert.equal(clicks.booth,1);
assert.equal(api.setupPeek().accountTriggerCount,1);
assert.equal(api.setupPerform('ACCOUNT_OPEN').ok,true);
assert.equal(api.setupPeek().settingsVisible,true);
assert.equal(api.setupPerform('PRINTER_MENU').ok,true);
assert.equal(api.setupPeek().printerEmpty,true);
assert.equal(api.setupPerform('PRINTER_EXPAND').ok,true);
assert.equal(api.setupPeek().printerOptionVisible,true);
assert.equal(api.setupPerform('PRINTER_PICK').ok,true);
assert.equal(api.setupPeek().printerSelected,true);
assert.equal(api.setupPerform('PRINTER_CONFIRM').ok,true);
assert.equal(clicks.printerConfirm,1);
dialogType='printer';
assert.equal(api.setupPerform('PRINTER_CLOSE').ok,true);
assert.equal(clicks.printerClose,1);
ctx.location.pathname='/sft3/app/unrelated';
assert.equal(api.setupPerform('ACCOUNT_OPEN').ok,false);
assert.equal(api.setupPerform('PRINTER_CONFIRM').ok,false);
console.log('WMS_SETUP_REGRESSION_PASS');
