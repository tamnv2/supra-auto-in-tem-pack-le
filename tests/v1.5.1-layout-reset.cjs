'use strict';
const fs=require('node:fs');
const assert=require('node:assert/strict');
const main=fs.readFileSync('MainForm.cs','utf8');
const browser=fs.readFileSync('WmsBrowser.cs','utf8');
const project=fs.readFileSync('SupraAutoPackLe.csproj','utf8');
// Prevent tab captions from wrapping, regardless of Windows font scaling.
assert.match(main,/new TableLayoutPanel\{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1/);
assert.match(main,/actions\.Controls\.Add\(actionButtons\[i\],i,0\)/);
assert.match(main,/exports\.Controls\.Add\(_exportXlsx,0,0\)/);
assert.match(main,/TextFormatFlags\.SingleLine/);
assert.match(main,/TextFormatFlags\.EndEllipsis/);
assert.match(main,/page==_productionTab\?"SẢN LƯỢNG"/);
assert.match(main,/page==_aboutTab\?"THÔNG TIN"/);
// Must open the real WMS tab before the first form render, without creating another WebView2.
assert.match(main,/_tabs\.TabPages\.AddRange\(\[_operationTab,_webTab,_productionTab,_aboutTab\]\);\s*_tabs\.SelectedTab=_webTab/);
assert.match(main,/browserLayout\.Controls\.Add\(_browser,0,1\)/);
// Production totals are separate scroll-safe cards, not one clipped multiline text label.
assert.match(main,/Label\[\] _metricCards/);
assert.match(main,/root\.RowStyles\.Add\(new RowStyle\(SizeType\.Absolute,174\)\)/);
assert.match(main,/root\.Controls\.Add\(metrics,0,1\)/);
assert.match(main,/Thời gian ở giữa/);
assert.doesNotMatch(main,/Trung vị \/ lượt/);
assert.match(main,/ScrollBars=ScrollBars\.Vertical/);
// About tab must provide explicit six scrollable disclosure sections.
assert.match(main,/sections\.Controls\.Add\(Section\("MỤC ĐÍCH SỬ DỤNG"/);
assert.match(main,/sections\.Controls\.Add\(Section\("GIỚI HẠN VÀ BẢO MẬT"/);
assert.match(main,/sections\.Controls\.Add\(Section\("ĐĂNG NHẬP VÀ MẬT KHẨU"/);
// Reset only while no active packing cycle and only after confirmation.
assert.match(main,/private async Task ResetBrowserAsync/);
assert.match(main,/if\(_busy\)/);
assert.match(main,/MessageBoxButtons\.YesNo,MessageBoxIcon\.Warning\)!=DialogResult\.Yes\)return;/);
assert.match(main,/if\(_setupTask!=null\)await _setupTask;/);
assert.match(main,/await _browser\.ClearBrowsingDataAsync\(\)/);
// Native WebView2 data clear only; no manual profile file, cookie, password access.
assert.match(browser,/await core\.Profile\.ClearBrowsingDataAsync\(\)/);
assert.doesNotMatch(browser,/(CookieManager|GetCookiesAsync|GetPassword|PasswordVault|Directory\.Delete\(_profile)/);
assert.match(project,/<Version>1\.7\.0<\/Version>/);
console.log('LAYOUT_RESET_REGRESSION_PASS');
