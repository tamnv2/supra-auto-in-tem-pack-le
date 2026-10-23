'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');

const read=p=>fs.readFileSync(p,'utf8');
const main=read('MainForm.cs');
const browser=read('WmsBrowser.cs');
const autoStart=read('AutoStartManager.cs');
const store=read('LocalStore.cs');
const project=read('SupraAutoPackLe.csproj');
const workflow=read('.github/workflows/build-windows.yml');

// One embedded browser remains attached across WinForms TabPage changes.
assert.match(main,/_operationTab=new\("NGHIỆP VỤ"\)/);
assert.match(main,/_webTab=new\("TRÌNH DUYỆT WMS"\)/);
assert.match(main,/_aboutTab=new\("THÔNG TIN PHẦN MỀM"\)/);
assert.match(main,/browserLayout\.Controls\.Add\(_browser,0,1\)/);
assert.match(main,/_operationTab\.Controls\.Add\(root\)/);
assert.match(main,/_tabs\.SelectedTab=_webTab/);
assert.match(main,/_tabs\.SelectedTab=_operationTab/);
assert.match(browser,/class WmsBrowser : UserControl/);
assert.doesNotMatch(browser,/class WmsBrowser : Form/);

// Native browser password manager; the application does not read credentials.
assert.match(browser,/settings\.IsInPrivateModeEnabled=false/);
assert.match(browser,/core\.Settings\.IsPasswordAutosaveEnabled=true/);
assert.match(browser,/core\.Settings\.IsGeneralAutofillEnabled=false/);
assert.match(browser,/Path\.Combine\(storage\.Root,"webview2-profile"\)/);
assert.doesNotMatch(browser,/Delete\(_profile/);
assert.doesNotMatch(browser,/(GetCookiesAsync|CookieManager|PasswordVault|localStorage\[|sessionStorage\[)/);

// No in-app self-update, PowerShell installer, or credentials.
assert.equal(fs.existsSync('UpdateClient.cs'),false);
assert.doesNotMatch(main,/(CheckAndInstallAsync|_update\.Click|CẬP NHẬT PHẦN MỀM)/);
assert.match(project,/<Version>1\.(?:7|8)\.0<\/Version>/);
assert.match(project,/<AssemblyName>WmsPackingAssistant<\/AssemblyName>/);
assert.match(workflow,/publish\/WmsPackingAssistant\.exe/);

// Per-user autostart should not request elevation or change machine-wide keys.
assert.match(autoStart,/Registry\.CurrentUser/);
assert.match(autoStart,/Software\\Microsoft\\Windows\\CurrentVersion\\Run/);
assert.doesNotMatch(autoStart,/(Registry\.LocalMachine|HKLM|schtasks|runas|ExecutionPolicy)/);

// Preserve the existing on-disk directory and append-only history mechanism.
assert.match(store,/LocalApplicationData\), "SupraAutoPackLe"/);
assert.match(store,/history\.jsonl/);
assert.match(store,/file\.Flush\(flushToDisk:true\)/);
console.log('V1.4_ARCHITECTURE_PASS');
