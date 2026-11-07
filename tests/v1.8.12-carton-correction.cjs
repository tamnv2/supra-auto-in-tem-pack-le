'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync('WmsHandoverJs.cs','utf8');
const script=source.split('"""')[1];
const panel=fs.readFileSync('TemChanPanel.cs','utf8');
const confirmation=fs.readFileSync('CartonConfirmationDialog.cs','utf8');
const correction=fs.readFileSync('CartonCorrectionDialog.cs','utf8');
const browser=fs.readFileSync('WmsBrowser.cs','utf8');
assert.ok(script);
assert.match(script,/window\.__supraHandoverV1=/);
assert.match(script,/location\.pathname/);
assert.match(script,/SO_ROW_OR_ACTION_AMBIGUOUS/);
assert.match(script,/UPDATE_PRECONDITION_FAILED/);
assert.match(script,/YES_DIALOG_NOT_UNIQUE/);
assert.doesNotMatch(script,/document\.cookie|localStorage|sessionStorage|XMLHttpRequest|fetch\(/);
assert.match(panel,/AutoCorrectCartonsAsync/);
assert.match(panel,/HANDOVER_NEW_QUANTITY_VERIFIED/);
assert.match(panel,/CorrectionDecision\.Back/);
assert.match(panel,/CorrectionDecision\.Exit/);
assert.match(panel,/correctionMayHaveSubmitted/);
assert.match(browser,/HandoverActionAsync/);
assert.match(confirmation,/ClientSize\s*=\s*new Size\(Math\.Min\(990/);
assert.match(confirmation,/instructions\.Replace/);
assert.match(confirmation,/actions\.Controls\.Add\(print,\s*next\+\+,\s*0\)/);
assert.match(correction,/XÁC NHẬN SỬA/);
assert.match(correction,/QUAY LẠI/);
assert.match(correction,/THOÁT/);

function fixture({rows=1,updateButtons=1,yesButtons=1,soRow='SOABC12345',path='/sft3/app/manager/sohandover'}={}) {
  const so='SOABC12345';
  let searched=false,edit=false,confirm=false,updated=0,yesCount=0,searchClicked=0;
  const style={display:'block',visibility:'visible'};
  const fieldSearch={innerText:"Mã Vị trí/ Tote/ SO cách nhau dấu ','"};
  const fieldQty={innerText:'Số lượng'};
  class Input {
    constructor(type,field) {this.type=type;this._value='';this.field=field;this.isConnected=true;}
    set value(v){this._value=String(v);}
    get value(){return this._value;}
    getClientRects(){return [{}];}
    getAttribute(){return null;}
    closest(s){ if(s.includes('mat-form-field'))return this.field; if(s.includes('mat-dialog-container'))return this.type==='number'&&edit?dlgUpdate:null;return null;}
    dispatchEvent(){return true;}
  }
  const search=new Input('text',fieldSearch);
  const quantity=new Input('number',fieldQty);
  const searchButton={
    innerText:'searchTìm Kiếm',textContent:'searchTìm Kiếm',
    click(){searched=true;searchClicked++;},
    closest(){return null;}
  };
  const editButton={innerText:'grid_view',textContent:'grid_view',
    querySelector(){return {textContent:'grid_view'};},
    click(){edit=true;}};
  const updateButton={innerText:'Cập Nhật',textContent:'Cập Nhật',
    click(){updated++;edit=false;confirm=true;}};
  const yesButton={innerText:'Có',textContent:'Có',
    click(){yesCount++;confirm=false;}};
  for(const b of [searchButton,editButton,updateButton,yesButton]){
    b.isConnected=true;b.disabled=false;b.getClientRects=()=>[{}];
    b.getAttribute=()=>null;
  }
  const row={
    isConnected:true,getClientRects:()=>[{}],
    innerText:soRow,
    querySelectorAll:q=>q==='button'?[editButton]:[]
  };
  const dlgUpdate={
    isConnected:true,getClientRects:()=>[{}],
    matches:q=>q==='mat-dialog-container',closest:()=>dlgUpdate,
    querySelector:q=>q==='app-confirm-update'?{}:null,
    querySelectorAll:q=>q==='input'?[quantity]:q==='button'?
      Array.from({length:updateButtons},()=>updateButton):[]
  };
  const dlgYes={
    isConnected:true,getClientRects:()=>[{}],
    matches:q=>q==='mat-dialog-container',closest:()=>dlgYes,
    querySelector:q=>q==='app-notification'?{}:null,
    querySelectorAll:q=>q==='button'?
      Array.from({length:yesButtons},()=>yesButton):[]
  };
  const doc={
    querySelectorAll:q=>{
      if(q==='input')return [search,...(edit?[quantity]:[])];
      if(q==='button')return [searchButton];
      if(q==='mat-row,tr,[role="row"]')return searched?Array.from({length:rows},()=>row):[];
      if(q==='mat-dialog-container,[role="dialog"]')
        return [...(edit?[dlgUpdate]:[]),...(confirm?[dlgYes]:[])];
      return [];
    }
  };
  const context={window:{},document:doc,HTMLInputElement:Input,
    Event:class Event {constructor(name,options){this.name=name;this.bubbles=options?.bubbles;}},
    getComputedStyle:()=>style,
    location:{protocol:'https:',hostname:'wms-supra.winmart.vn',pathname:path}};
  vm.runInNewContext(script,context);
  return {api:context.window.__supraHandoverV1,so,
    clicks:()=>({searchClicked,updated,yesCount}),quantity:()=>quantity.value};
}
{
  const f=fixture();const q=31,api=f.api;
  assert.equal(api.peek(f.so,q).searchInputCount,1);
  assert.equal(api.perform('SEARCH_INPUT',f.so,q).ok,true);
  assert.equal(api.peek(f.so,q).searchValueMatches,true);
  assert.equal(api.perform('SEARCH_CLICK',f.so,q).ok,true);
  assert.equal(api.peek(f.so,q).rowCount,1);
  assert.equal(api.perform('OPEN_UPDATE',f.so,q).ok,true);
  assert.equal(api.peek(f.so,q).quantityInputCount,1);
  assert.equal(api.perform('SET_QUANTITY',f.so,q).ok,true);
  assert.equal(api.peek(f.so,q).quantityMatches,true);
  assert.equal(f.quantity(),'31');
  assert.equal(api.perform('UPDATE',f.so,q).ok,true);
  assert.equal(api.peek(f.so,q).confirmYesCount,1);
  assert.equal(api.perform('CONFIRM_YES',f.so,q).ok,true);
  assert.equal(api.peek(f.so,q).confirmDialogCount,0);
  assert.deepEqual(f.clicks(),{searchClicked:1,updated:1,yesCount:1});
}
{
  const f=fixture({rows:2}),q=2,api=f.api;
  api.perform('SEARCH_INPUT',f.so,q);
  api.perform('SEARCH_CLICK',f.so,q);
  assert.equal(api.peek(f.so,q).rowCount,2);
  assert.equal(api.perform('OPEN_UPDATE',f.so,q).ok,false);
  assert.equal(f.clicks().updated,0);
}
{
  const f=fixture({soRow:'DIFFERENT_ORDER'}),q=4,api=f.api;
  api.perform('SEARCH_INPUT',f.so,q);
  api.perform('SEARCH_CLICK',f.so,q);
  assert.equal(api.perform('OPEN_UPDATE',f.so,q).ok,false);
}
{
  const f=fixture({updateButtons:0}),q=4,api=f.api;
  api.perform('SEARCH_INPUT',f.so,q);
  api.perform('SEARCH_CLICK',f.so,q);
  api.perform('OPEN_UPDATE',f.so,q);
  api.perform('SET_QUANTITY',f.so,q);
  assert.equal(api.perform('UPDATE',f.so,q).ok,false);
}
{
  const f=fixture({yesButtons:2}),q=5,api=f.api;
  api.perform('SEARCH_INPUT',f.so,q);
  api.perform('SEARCH_CLICK',f.so,q);
  api.perform('OPEN_UPDATE',f.so,q);
  api.perform('SET_QUANTITY',f.so,q);
  api.perform('UPDATE',f.so,q);
  assert.equal(api.perform('CONFIRM_YES',f.so,q).ok,false);
  assert.equal(f.clicks().yesCount,0);
}
{
  const f=fixture({path:'/sft3/app/manager/other'}),q=5;
  assert.equal(f.api.peek(f.so,q).ok,false);
  assert.equal(f.api.perform('SEARCH_INPUT',f.so,q).ok,false);
}
for(const bad of [0,-1,1.5,1000000]){
  const f=fixture();
  assert.equal(f.api.perform('SET_QUANTITY',f.so,bad).ok,false);
}
console.log('v1.8.12 carton correction regression PASS: search, exact row, quantity, UPDATE, yes, duplicate/wrong/invalid guards');
