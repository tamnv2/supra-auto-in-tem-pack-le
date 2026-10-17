'use strict';
const fs=require('node:fs');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const browser=fs.readFileSync('WmsBrowser.cs','utf8');
const source=fs.readFileSync('WmsRecorderJs.cs','utf8');
const writer=fs.readFileSync('WmsActionRecorder.cs','utf8');
const main=fs.readFileSync('MainForm.cs','utf8');
const js=source.slice(source.indexOf('"""')+3,source.lastIndexOf('"""'));
assert(!js.includes('localStorage'), 'Recorder must not read WMS storage');
assert(!js.includes('document.cookie'), 'Recorder must not read cookies');
assert(!js.includes('fetch('), 'Recorder must not send network data');
assert.match(browser,/IsPasswordAutosaveEnabled=true/);
assert.match(browser,/ClearBrowsingDataAsync\(CoreWebView2BrowsingDataKinds\.AllSite\)/);
assert(!browser.includes('ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile)'));
const iClear=browser.indexOf('await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite)');
assert(iClear>0 && browser.indexOf('core.Navigate(Target);',iClear)>iClear,
 'Site session data must be cleared BEFORE first WMS navigation');
assert.match(browser,/IsInPrivateModeEnabled=false/);
assert.match(browser,/await core.AddScriptToExecuteOnDocumentCreatedAsync\(WmsRecorderJs\.Script\)/);
assert.match(browser,/core\.WebMessageReceived/);
assert.match(writer,/ZipFile\.Open\(output,ZipArchiveMode\.Create\)/);
assert.match(writer,/DesktopDirectory/);
assert.match(writer,/Channel\.CreateUnbounded<string>/);
assert.match(main,/BẮT ĐẦU GHI/);
assert.match(main,/KẾT THÚC & XUẤT LOG/);
assert.match(main,/ẨN TRÌNH DUYỆT/);
assert.match(main,/if\(_browser\.Recording\)/);
assert.match(main,/EndRecordingAsync\(\)/);
const messages=[], listeners={};
let location={protocol:'https:',hostname:'wms-supra.winmart.vn',pathname:'/sft3/app/saleorder/packing'};
const document={
 documentElement:null,
 querySelectorAll(){return []},
 addEventListener(name,handler){listeners[name]=handler}
};
class Observer{observe(){}}
const ctx={
 window:{chrome:{webview:{postMessage:s=>messages.push(JSON.parse(s))}},addEventListener(){}},
 document,location,MutationObserver:Observer,
 setTimeout(){return 1},clearTimeout(){},
};
vm.runInNewContext(js,ctx,{timeout:3000});
const api=ctx.window.__supraActionsV17;
assert(api && typeof api.setActive==='function');
const target=(tag,content,value='',type='button')=>({
 tagName:tag.toUpperCase(),innerText:content,textContent:content,
 value,type,id:'',classList:[],parentElement:null,
 getAttribute(name){return name==='type'?type:null},
 getClientRects(){return [1]},
 matches(query){return tag==='input' && query.includes('input')},
 closest(){return this}
});
api.setActive(true);
let button=target('button','Thiết lập máy in');
listeners.click({target:button});
assert(messages.some(x=>x.kind==='click' && x.text==='Thiết lập máy in'));
let input=target('input','', 'super-sensitive-TOTE-2026','text');
listeners.change({target:input});
assert(!messages.some(x=>JSON.stringify(x).includes('super-sensitive')),
 'Free-text input values must never reach host');
let code=target('input','','SDG.22','text');
listeners.change({target:code});
assert(messages.some(x=>x.selection==='SDG.22'));
let password=target('input','','DO-NOT-LOG-PASSWORD','password');
const before=messages.length;
listeners.click({target:password});
assert.equal(messages.length,before,'Password fields must not produce any message');
location.hostname='auth-supra.winmart.vn';
listeners.click({target:button});
assert.equal(messages.length,before,'Auth origin must not be recorded');
api.setActive(false);
location.hostname='wms-supra.winmart.vn';
listeners.click({target:button});
assert.equal(messages.length,before,'Stop must disable capture');
console.log('WMS_SESSION_PASSWORD_PRESERVE_AND_UI_TRACE_PASS');
