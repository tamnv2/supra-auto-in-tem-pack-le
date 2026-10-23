'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const js = fs.readFileSync(path.join(__dirname, 'dom-recorder.js'), 'utf8');
const cs = fs.readFileSync(path.join(__dirname, 'Program.cs'), 'utf8');

for (const forbidden of [
  /\b(?:screenX|screenY|clientX|clientY|pageX|pageY|offsetX|offsetY)\b/,
  /\b(?:localStorage|sessionStorage|document\.cookie|indexedDB)\b/,
  /\b(?:fetch|XMLHttpRequest|Network\.getResponseBody|requestHeaders)\s*\(/,
  /\b(?:input|target|el)\.value\b/,
  /\b(?:innerHTML|outerHTML)\b/
]) {
  assert.equal(forbidden.test(js), false, 'Recorder must not gather coordinates, storage, network or field values: ' + forbidden);
}
assert.match(js, /window\.chrome\?\.webview\?\.postMessage/);
assert.match(js, /querySelectorAll\(candidate\)\.length === 1/);
assert.match(js, /\[role="dialog"\]/);
assert.match(js, /makeSelector\(/);
assert.match(js, /password/);
assert.match(js, /MutationObserver/);
assert.match(cs, /CoreWebView2Environment\.CreateAsync/);
assert.match(cs, /StartUrl = "https:\/\/wms-supra\.winmart\.vn\/sft3\/app\/saleorder\/list"/);
assert.match(cs, /SpecialFolder\.DesktopDirectory/);
assert.match(cs, /IsPasswordAutosaveEnabled = false/);
assert.match(cs, /AddScriptToExecuteOnDocumentCreatedAsync/);
assert.match(cs, /TryGetWebMessageAsString/);
assert.match(cs, /IsRecordable\(args\.Source\)/);
assert.match(cs, /!uri\.AbsolutePath\.Contains\("\/login"/);
assert.doesNotMatch(cs, /GetCookiesAsync|GetContentAsync|CallDevToolsProtocolMethodAsync/);
console.log('DOM semantics/privacy regression checks PASS');
