'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync('WmsSkuJs.cs','utf8');
const script=source.split('"""')[1];
const browser=fs.readFileSync('WmsBrowser.cs','utf8');
const panel=fs.readFileSync('TemChanPanel.cs','utf8');
const dialog=fs.readFileSync('CartonConfirmationDialog.cs','utf8');
const form=fs.readFileSync('MainForm.cs','utf8');
const store=fs.readFileSync('SkuWatchStore.cs','utf8');
assert.ok(script);
assert.doesNotMatch(script,/fetch\s*\(|XMLHttpRequest|document\.cookie|localStorage|sessionStorage|postMessage|https?:\/\//);
assert.match(script,/__supraSkuAuditV1=\{peek,activate,page100,next\}/);
assert.match(browser,/ScanSkuAsync/);
assert.match(browser,/_skuView\.EnsureCoreWebView2Async\(_sharedEnvironment,options\)/);
assert.match(panel,/skuScan=_browser\.ScanSkuAsync\(so,watched,ct\)/);
assert.ok(panel.indexOf('skuScan=_browser.ScanSkuAsync') < panel.indexOf('var p=await WaitAsync(so,x=>x.statusKnown'));
assert.match(dialog,/UpdateSkuAudit/);
assert.match(dialog,/SKU: /);
assert.match(dialog,/SL Đặt: /);
assert.match(dialog,/Base Unit: /);
assert.match(dialog,/if\(canPrint\)actions\.Controls\.Add\(print/);
assert.match(form,/new SkuWatchPanel\(_skuWatch,_store\)/);
assert.match(store,/File\.Move\(tmp,_path,true\)/);
assert.match(store,/UpdatedAt=DateTime\.Now/);

function fixture({wrongPath=false,badHeaders=false}={}) {
  const so='SOABC12345';
  let active=false, expanded=false, pageSize=50, page=0;
  const all=Array.from({length:105},(_,i)=>[
    i===3?'10198413':i===103?'10245142':String(10000000+i),
    'Sản phẩm '+(i+1), '12', '24', 'LOC'
  ]);
  const mk=(extra={})=>({
    isConnected:true,disabled:false,innerText:'',textContent:'',
    getClientRects:()=>[{}],getAttribute:()=>null,
    classList:{contains:()=>false},click:()=>{}, ...extra
  });
  const headings=['STT','SKU','Tên sản phẩm','PCB','SL Đặt','Base Unit','SL Xuất'];
  if(badHeaders)headings[2]='Thông tin khác';
  const header=headings.map(x=>mk({innerText:x}));
  const tab=mk({
    innerText:'THÔNG TIN CHÍNH',
    getAttribute:key=>key==='aria-selected'?(active?'true':'false'):null,
    click:()=>{active=true;}
  });
  const select=mk({
    get innerText(){return String(pageSize);},
    click:()=>{expanded=true;}
  });
  const option=mk({innerText:'100',click:()=>{pageSize=100;page=0;expanded=false;}});
  const next=mk({click:()=>{page++;}});
  const paginator=mk({querySelector:q=>{
    if(q==='.mat-paginator-range-label'){
      const begin=page*pageSize+1,end=Math.min((page+1)*pageSize,all.length);
      return {textContent:begin+' – '+end+' of '+all.length};
    }
    return null;
  },querySelectorAll:q=>{
    if(q==='mat-select')return [select];
    if(q==='button.mat-paginator-navigation-next')return [next];
    return [];
  }});
  const table=mk({querySelectorAll:q=>{
    if(q==='mat-header-cell')return header;
    if(q==='mat-row')return all.slice(page*pageSize,(page+1)*pageSize).map(values=>
      mk({querySelectorAll:q=>q==='mat-cell'
        ? [String(1),...values].map(v=>mk({innerText:v,textContent:v}))
        :[]}));
    return [];
  }});
  const document={querySelectorAll:q=>{
    if(q==='[role="tab"],.mat-tab-label')return [tab];
    if(q==='mat-table')return active?[table]:[];
    if(q==='mat-paginator')return active?[paginator]:[];
    if(q==='mat-option,[role="option"]')return expanded?[option]:[];
    return [];
  }};
  const window={};vm.runInNewContext(script,{
    window,document,getComputedStyle:()=>({display:'block',visibility:'visible'}),
    location:{protocol:'https:',hostname:'wms-supra.winmart.vn',
      pathname:wrongPath?'/other':'/sft3/app/saleorder/details/'+so,
      search:'?PickType=PickOrder'},Number,String,Array,Set
  });
  return {api:window.__supraSkuAuditV1,so};
}
{
  const f=fixture();const api=f.api,targets=['10198413','10245142'];
  assert.equal(api.peek(f.so,targets).ok,false);
  assert.equal(api.activate(f.so).ok,true);
  assert.equal(api.page100(f.so).reason,'OPEN_100_OPTIONS');
  assert.equal(api.page100(f.so).reason,'SELECTED_100');
  const first=api.peek(f.so,targets);
  assert.equal(first.ok,true);assert.equal(first.pageSize,100);
  assert.equal(first.rows,100);assert.equal(first.matches.length,1);
  assert.equal(first.matches[0].sku,'10198413');
  assert.equal(first.matches[0].orderedQuantity,'24');
  assert.equal(first.matches[0].baseUnit,'LOC');
  assert.equal(first.last,false);
  assert.equal(api.next(f.so).ok,true);
  const second=api.peek(f.so,targets);
  assert.equal(second.ok,true);assert.equal(second.start,101);
  assert.equal(second.end,105);assert.equal(second.last,true);
  assert.equal(second.matches.length,1);
  assert.equal(second.matches[0].sku,'10245142');
}
{
  const f=fixture({badHeaders:true}),api=f.api;
  api.activate(f.so);api.page100(f.so);api.page100(f.so);
  assert.equal(api.peek(f.so,['10198413']).reason,'SKU_COLUMNS_CHANGED');
}
{
  const f=fixture({wrongPath:true});
  assert.equal(f.api.activate(f.so).reason,'WRONG_SO_PAGE');
  assert.equal(f.api.peek(f.so,['10198413']).ok,false);
}
console.log('v1.8.15 SKU audit regression PASS: main tab, 100 rows, pagination, multi-page match, changed headers, wrong SO');
