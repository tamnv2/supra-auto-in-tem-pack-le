// Regression test for the reusable WMS confirmation dialog (no live WMS access).
'use strict';
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const source = fs.readFileSync('WmsWorkflowJs.cs', 'utf8');
const begin = source.indexOf('"""');
const end = source.lastIndexOf('"""');
assert(begin >= 0 && end > begin, 'WMS JavaScript is missing');
const script = source.slice(begin + 3, end);
const clicks = { yes: 0, no: 0 };
const button = (text, key) => ({
  innerText: text, disabled: false,
  getClientRects: () => [1],
  getAttribute: () => null,
  click: () => { clicks[key]++; }
});
const yes = button('Có', 'yes');
const no = button('Không', 'no');
const buttons = [yes, no];
const dialog = {
  innerText: 'Bạn có muốn hoàn thành đóng gói không?',
  getClientRects: () => [1],
  querySelectorAll: selector => selector.includes('button') ? buttons : []
};
const document = {
  body: { innerText: '' },
  querySelectorAll: selector => {
    if (selector.includes('mat-dialog-container')) return [dialog];
    if (selector.includes('button')) return buttons;
    return [];
  }
};
const context = {
  window: {},
  document,
  location: { protocol: 'https:', hostname: 'wms-supra.winmart.vn', pathname: '/sft3/app/saleorder/packing' },
  getComputedStyle: () => ({ display: 'block', visibility: 'visible' })
};
vm.runInNewContext(script, context, { timeout: 3000 });
const api = context.window.__supraPackLeV1;
assert(api && typeof api.peek === 'function' && typeof api.perform === 'function');
assert.equal(api.peek().outboundSlipPrompt, false);
let r = api.perform('NO');
assert.equal(r.ok, false, 'Must not press No on the finish confirmation');
assert.equal(clicks.no, 0);
// The SAME DOM node remains while WMS replaces only its question.
dialog.innerText = 'Bạn có muốn in phiếu xuất kho cho SO SOTEST123?';
assert.equal(api.peek().outboundSlipPrompt, true);
r = api.perform('NO');
assert.equal(r.ok, true, 'Must support a reused dialog DOM node');
assert.equal(clicks.no, 1);
assert.equal(clicks.yes, 0);
// Disabled or ambiguous buttons cannot be clicked.
no.disabled = true;
r = api.perform('NO');
assert.equal(r.ok, false);
assert.equal(clicks.no, 1);
no.disabled = false;
buttons.push(button('Không', 'no'));
r = api.perform('NO');
assert.equal(r.ok, false);
assert.equal(clicks.no, 1);
context.location.pathname = '/sft3/app/other';
assert.equal(api.perform('NO').ok, false);
console.log('WMS_OUTBOUND_SLIP_DIALOG_REGRESSION_PASS');
