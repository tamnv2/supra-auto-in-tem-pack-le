'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const src = fs.readFileSync('WmsTemChanJs.cs','utf8');
const script = src.split('"""')[1];
const form = fs.readFileSync('MainForm.cs','utf8');
const browser = fs.readFileSync('WmsBrowser.cs','utf8');
const panel = fs.readFileSync('TemChanPanel.cs','utf8');
assert.ok(script && script.includes('window.__supraTemChanV1'));
assert.match(script, /expectedStatus\s*=\s*'đã lưu trữ hàng sau đóng gói'/);
assert.match(script, /Number\.isSafeInteger/);
assert.match(script, /end <= total/);
assert.match(script, /input\[name="startPackage"\]/);
assert.match(script, /input\[name="endPackage"\]/);
assert.match(script, /PRINT_ROW_NOT_UNIQUE/);
assert.match(script, /mat-row,tr,\[role="row"\]/);
assert.match(script, /mat-grid-list mat-grid-tile/);
assert.match(script, /PRINT_CONFIRM_AMBIGUOUS/);
assert.match(script, /PRINT_COMMAND_SENT/);
assert.doesNotMatch(script, /\b(?:screenX|screenY|clientX|clientY|pageX|pageY)\b/);
assert.doesNotMatch(script, /\b(?:localStorage|sessionStorage|document\.cookie)\b/);
assert.match(form, /BuildStartupChoice/);
assert.match(form, /BuildOperationSettings/);
assert.match(form, /SwitchOperation/);
assert.match(browser, /details\/"/);
assert.match(panel, /_so\.KeyDown/);
assert.doesNotMatch(panel, /BeginInvoke\(new Action/);
assert.match(script,/dialogReason/);
assert.doesNotMatch(panel, /TryAppendHistory|HistoryRow|ExportXlsx|SaveHistory/);

function fixture({status='Đã lưu trữ hàng sau đóng gói', total='4',
  begin='1', end='4', prints=1, confirmButtons=1, decorative=[],
  omitStatusCaption=false, duplicateStatusTiles=false}={}) {
  const so='SOABC12345';
  let activeTab=false, dialogOpen=false, commandCount=0;
  const css={display:'block',visibility:'visible'};
  const make=(extra={})=>({
    isConnected:true,disabled:false,
    getClientRects:()=>[{}],getAttribute:(k)=>null,
    classList:{contains:(c)=>c==='app-table-action-button'},
    closest:(s)=>s.includes('tr')||s.includes('mat-cell')?{}:null,
    querySelector:()=>null, querySelectorAll:()=>[],innerText:'',
    click:()=>{}, ...extra
  });
  const tab = make({innerText:'THÔNG TIN KIỆN HÀNG',
    getAttribute:key=>key==='aria-selected'?(activeTab?'true':'false'):null,
    classList:{contains:c=>c==='mat-tab-label-active'&&activeTab},
    click:()=>{activeTab=true;}});
  const rows=Array.from({length:prints},()=>make({
    classList:{contains:c=>c==='app-table-action-button'},
    querySelector:()=>({textContent:'print'}),
    click:()=>{dialogOpen=true;}
  }));
  const input1=make({value:begin});
  const input2=make({value:end});
  const confirms=Array.from({length:confirmButtons},()=>make({
    innerText:'In Nhãn', click:()=>{commandCount++;dialogOpen=false;}
  }));
  const dialog=make({
    innerText:'Tùy chọn in nhãn Mã SO: '+so+' Tổng số thùng: '+total+
      ' Số thùng bắt đầu: Số thùng kết thúc: In Nhãn Thoát',
    querySelectorAll:(q)=>q==='input[name="startPackage"]'?[input1]:
      q==='input[name="endPackage"]'?[input2]:
      q==='button'?confirms:[]
  });
  const statusEl=make({tagName:'DIV'});
  const nodes=[
    ...(!omitStatusCaption?[{textContent:'Trạng thái:',parentElement:statusEl}]:[]),
    ...decorative.map(textContent=>({textContent,parentElement:statusEl})),
    {textContent:status,parentElement:statusEl},
    {textContent:'Thông tin kiện hàng',parentElement:statusEl}
  ];
  const document={
    body:{},
    createTreeWalker:()=>({nextNode:(()=>{let i=-1;return ()=>nodes[++i]||null;})()}),
    querySelectorAll:q=>{
      if(q==='[role="tab"],.mat-tab-label')return [tab];
      if(q==='mat-grid-list mat-grid-tile')
        return (duplicateStatusTiles?[status,status]:[status])
          .map(text=>make({innerText:text}));
      if(q==='button.app-table-action-button')return rows;
      if(q==='mat-dialog-container')return dialogOpen?[dialog]:[];
      if(q==='[role="dialog"]')return dialogOpen?[dialog]:[];
      return [];
    }
  };
  const window={};const context={
    window,document,NodeFilter:{SHOW_TEXT:4},
    getComputedStyle:()=>css,
    location:{protocol:'https:',hostname:'wms-supra.winmart.vn',
      pathname:'/sft3/app/saleorder/details/'+so,search:'?PickType=PickOrder'},
    Number,String,RegExp,Array
  };
  vm.runInNewContext(script,context);
  return {api:window.__supraTemChanV1,so,
    commands:()=>commandCount,tab:()=>activeTab,dialog:()=>dialogOpen};
}

{
  const f=fixture();
  assert.equal(f.api.peek(f.so).statusValid,true);
  assert.equal(f.api.peek(f.so).statusSource,'ORDER_HEADER_TILE');
  assert.equal(f.api.peek(f.so).printCount,1);
  // A closed dialog must NOT overwrite the top-level valid SO state.
  assert.equal(f.api.peek(f.so).reason,'OK');
  assert.equal(f.api.peek(f.so).dialogReason,'PRINT_DIALOG_ABSENT');
  assert.equal(f.api.perform('TAB',f.so).ok,true);
  assert.equal(f.api.perform('OPEN_PRINT',f.so).ok,true);
  assert.equal(f.api.peek(f.so).rangeValid,true);
  assert.equal(f.api.perform('PRINT_LABEL',f.so).ok,true);
  assert.equal(f.commands(),1);
  assert.equal(f.dialog(),false);
}
for(const end of ['1.3','0.6','-1','0','NaN','10000']){
  const f=fixture({end});
  f.api.perform('TAB',f.so);f.api.perform('OPEN_PRINT',f.so);
  assert.equal(f.api.peek(f.so).rangeValid,false,'end='+end);
  assert.equal(f.api.perform('PRINT_LABEL',f.so).ok,false);
  assert.equal(f.commands(),0);
}
for(const total of ['1.5','0','-2']){
  const f=fixture({total});
  f.api.perform('TAB',f.so);f.api.perform('OPEN_PRINT',f.so);
  assert.equal(f.api.peek(f.so).rangeValid,false,'total='+total);
}
{
  // Regression: WMS may render decorative/help text between the field caption
  // and the green status value. Old code used only the first following text.
  const f=fixture({decorative:[' ','?', '•']});
  assert.equal(f.api.peek(f.so).statusValid,true);
  assert.equal(f.api.peek(f.so).statusKnown,true);
}
{
  // Actual field trace: WMS status is in a mat-grid-tile, while its caption
  // can be absent after moving to the "Thông tin kiện hàng" tab.
  const f=fixture({omitStatusCaption:true});
  assert.equal(f.api.peek(f.so).statusValid,true);
  assert.equal(f.api.peek(f.so).statusSource,'ORDER_HEADER_TILE');
  assert.equal(f.api.perform('TAB',f.so).ok,true);
  assert.equal(f.api.peek(f.so).printCount,1);
  assert.equal(f.api.perform('OPEN_PRINT',f.so).ok,true);
  assert.equal(f.dialog(),true);
}
{
  // Multiple same status tiles is ambiguous; never click a print button.
  const f=fixture({omitStatusCaption:true,duplicateStatusTiles:true});
  assert.equal(f.api.peek(f.so).statusValid,false);
  assert.equal(f.api.peek(f.so).statusSource,'HEADER_STATUS_AMBIGUOUS');
  assert.equal(f.api.perform('OPEN_PRINT',f.so).ok,false);
}
{
  const f=fixture({status:'Đang đóng gói'});
  assert.equal(f.api.peek(f.so).statusValid,false);
  assert.equal(f.api.peek(f.so).reason,'STATUS_NOT_READY');
  assert.equal(f.api.perform('TAB',f.so).reason,'STATUS_NOT_READY');
  assert.equal(f.api.perform('OPEN_PRINT',f.so).reason,'STATUS_NOT_READY');
}
{
  const f=fixture({prints:2});
  f.api.perform('TAB',f.so);
  assert.equal(f.api.perform('OPEN_PRINT',f.so).ok,false);
  assert.equal(f.dialog(),false);
}
{
  const f=fixture({confirmButtons:2});
  f.api.perform('TAB',f.so);f.api.perform('OPEN_PRINT',f.so);
  assert.equal(f.api.perform('PRINT_LABEL',f.so).ok,false);
  assert.equal(f.commands(),0);
}
console.log('Dual-operation checks PASS: status, exact SO, integer box range, unique print, one-shot click');
