// SUPRA Tem Chan test recorder. Never read input.value, storage, cookies, network or coordinates.
(() => {
  'use strict';
  if (window.__supraTemChanRecorder) return;

  let active = false;
  let lastPath = '';
  let lastSnapshot = '';
  let snapshotTimer = 0;
  let observer = null;

  const allowed = () =>
    location.protocol === 'https:' &&
    location.hostname === 'wms-supra.winmart.vn' &&
    location.pathname.startsWith('/sft3/app/') &&
    !/login/i.test(location.pathname);

  const clean = s => String(s == null ? '' : s).replace(/\s+/g, ' ').trim();
  const sensitive = s =>
    /password|passwd|secret|token|bearer|cookie|authorization|session|otp|mật khẩu|đăng nhập|login|auth/i
      .test(String(s || ''));

  const safe = (s, max = 130) => {
    let value = clean(s);
    if (sensitive(value)) return '[SENSITIVE]';
    value = value.replace(/[\w.+-]+@[\w.-]+\.[a-z]{2,}/ig, '[EMAIL]')
      .replace(/(?<!\d)(?:\+?84|0)[\d.\s-]{8,13}(?!\d)/g, '[PHONE]')
      .replace(/\b\d{7,}\b/g, '[NUMBER]')
      .replace(/\b[A-Za-z0-9_-]{28,}\b/g, '[IDENTIFIER]');
    return value.slice(0, max);
  };

  const blocked = el => {
    if (!(el instanceof Element)) return true;
    if (el.closest('[data-sensitive], input[type=password], input[type=hidden], ' +
        '[autocomplete="current-password"], [autocomplete="one-time-code"]'))
      return true;
    const meta = ['name','id','type','autocomplete','aria-label','placeholder','title','formcontrolname']
      .map(k => el.getAttribute(k)).join(' ');
    if (sensitive(meta)) return true;
    const form = el.closest('form');
    if (form && form.querySelector('input[type=password], [autocomplete="one-time-code"]'))
      return true; // Completely exclude authentication forms, even nonpassword controls.
    return false;
  };

  const emit = data => {
    if (!active || !allowed()) return;
    try { window.chrome?.webview?.postMessage(JSON.stringify(data)); } catch (_) { }
  };

  const visible = el => {
    if (!el || !el.isConnected) return false;
    const style = getComputedStyle(el);
    return style.display !== 'none' && style.visibility !== 'hidden' &&
      el.getClientRects().length > 0;
  };

  const escaped = s => {
    try { return CSS.escape(s); }
    catch (_) { return String(s).replace(/[^a-zA-Z0-9_-]/g, '\\$&'); }
  };

  const safeAttribute = (el, key) => {
    const value = clean(el.getAttribute(key));
    if (!value || sensitive(key) || sensitive(value)) return '';
    if (value.length > 100 || /[?&#=]/.test(value)) return '';
    // Reject high-entropy/dynamic IDs, UUIDs and generated framework identifiers.
    if (/\d{5,}/.test(value) || /[a-f0-9]{8}-[a-f0-9-]{16,}/i.test(value) ||
        /^(?:mat-|cdk-|ng-|ember|react)/i.test(value) && /\d/.test(value))
      return '';
    return safe(value, 90);
  };

  const makeSelector = el => {
    const tag = el.tagName.toLowerCase();
    const candidates = [];
    const id = safeAttribute(el, 'id');
    const testId = safeAttribute(el, 'data-testid') || safeAttribute(el, 'data-test');
    const formControlName = safeAttribute(el, 'formcontrolname');
    const name = safeAttribute(el, 'name');
    const aria = safeAttribute(el, 'aria-label');
    const title = safeAttribute(el, 'title');
    if (testId) candidates.push('[data-testid="' + escaped(testId) + '"]');
    if (id) candidates.push('#' + escaped(id));
    if (formControlName) candidates.push(tag + '[formcontrolname="' + escaped(formControlName) + '"]');
    if (name) candidates.push(tag + '[name="' + escaped(name) + '"]');
    if (aria) candidates.push(tag + '[aria-label="' + escaped(aria) + '"]');
    if (title) candidates.push(tag + '[title="' + escaped(title) + '"]');
    if (el.getAttribute('role') && !sensitive(el.getAttribute('role')))
      candidates.push('[role="' + escaped(el.getAttribute('role')) + '"]');
    const classes = Array.from(el.classList).filter(c =>
      /^[a-z][a-z0-9_-]{1,29}$/i.test(c) && !sensitive(c) && !/\d{4,}/.test(c)).slice(0, 3);
    if (classes.length) candidates.push(tag + '.' + classes.map(escaped).join('.'));
    candidates.push(tag);

    // A fallback is diagnostic only when it matches more than one element. Never
    // guess nth-child/nth-of-type or click a coordinate.
    for (const candidate of candidates) {
      try {
        if (document.querySelectorAll(candidate).length === 1)
          return { selector: candidate, unique: true };
      } catch (_) { }
    }
    return { selector: candidates[0], unique: false };
  };

  const normalizeTarget = target => {
    const origin = target instanceof Element ? target : target?.parentElement;
    if (!origin) return null;
    return origin.closest(
      'button,a,select,input,textarea,label,[role="button"],[role="menuitem"],' +
      '[role="option"],[role="combobox"],[role="checkbox"],[role="radio"],' +
      'mat-option,mat-select,[data-testid],td,th,[role="row"],tr') || origin;
  };

  const textOf = el => {
    if (el.matches('input,textarea,select,option,td,tr,[role="row"]'))
      return ''; // Never collect field values or table row business contents.
    const value = safe(el.innerText || el.textContent || '', 110);
    return sensitive(value) ? '[SENSITIVE]' : value;
  };

  const owningDialog = el => {
    const dialog = el.closest('mat-dialog-container,[role="dialog"],.mat-mdc-dialog-container,' +
      '.modal,.modal-dialog');
    if (!dialog) return '';
    const heading = dialog.querySelector('h1,h2,h3,h4,[mat-dialog-title],.modal-title');
    return heading ? safe(heading.textContent, 100) : '[DIALOG]';
  };

  const tableHeader = el => {
    const cell = el.closest('td,[role="gridcell"]');
    if (!cell) return '';
    const dataKey = cell.getAttribute('data-column') || cell.getAttribute('mat-column-def') ||
      cell.getAttribute('aria-label');
    if (dataKey && !sensitive(dataKey)) return safe(dataKey, 80);
    const head = cell.closest('table')?.querySelector('thead');
    const idx = cell.cellIndex;
    return head && idx >= 0 ? safe(head.querySelectorAll('th')[idx]?.textContent, 80) : '';
  };

  function describe(target) {
    const el = normalizeTarget(target);
    if (!el || blocked(el)) return null;
    const selector = makeSelector(el);
    const attrs = {};
    for (const [output, attr] of [
      ['id','id'], ['name','name'], ['title','title'], ['ariaLabel','aria-label'],
      ['placeholder','placeholder'], ['formControlName','formcontrolname'],
      ['testId','data-testid'], ['type','type']
    ]) attrs[output] = safeAttribute(el, attr);
    let label = '';
    const labelEl = el.closest('label') ||
      (el.id ? Array.from(document.querySelectorAll('label[for]'))
        .find(node => node.htmlFor === el.id) : null);
    if (labelEl && !blocked(labelEl)) label = safe(labelEl.textContent, 100);
    return {
      tag: el.tagName.toLowerCase(),
      role: safeAttribute(el, 'role'),
      ...attrs,
      label,
      text: textOf(el),
      selector: selector.selector,
      unique: selector.unique,
      dialog: owningDialog(el),
      cellHeader: tableHeader(el),
      disabled: !!el.disabled || el.getAttribute('aria-disabled') === 'true',
      visible: visible(el)
    };
  }

  const record = (kind, event, key = '') => {
    if (!active || !allowed()) return;
    const detail = describe(event?.target);
    if (!detail) return;
    emit({ kind, ...detail, ...(key ? { key } : {}) });
    if (kind === 'click' || kind === 'change' || kind === 'submit')
      scheduleSnapshot(kind);
  };

  const snapshot = reason => {
    if (!active || !allowed()) return;
    const selectors = [
      '[role="dialog"]', 'mat-dialog-container', '.modal-dialog',
      'button', 'input', 'select', 'mat-select',
      '[role="button"]', '[role="menuitem"]', '[role="combobox"]', '[role="option"]'
    ].join(',');
    const controls = [];
    for (const el of document.querySelectorAll(selectors)) {
      if (controls.length >= 28) break;
      if (blocked(el) || !visible(el)) continue;
      if (el.matches('input[type=hidden]')) continue;
      const desc = describe(el);
      if (desc) controls.push(desc);
    }
    const fingerprint = JSON.stringify(controls);
    if (fingerprint === lastSnapshot && reason === 'mutation') return;
    lastSnapshot = fingerprint;
    emit({kind:'snapshot', reason: safe(reason, 50), controls});
  };

  const scheduleSnapshot = reason => {
    if (!active) return;
    clearTimeout(snapshotTimer);
    snapshotTimer = setTimeout(() => snapshot(reason), 450);
  };

  const checkRoute = () => {
    if (!active || !allowed()) return;
    const current = location.pathname;
    if (current === lastPath) return;
    lastPath = current;
    emit({kind:'route', reason:'SPA pathname changed'});
    scheduleSnapshot('route');
  };

  document.addEventListener('pointerdown', e => record('pointerdown', e), true);
  document.addEventListener('click', e => record('click', e), true);
  document.addEventListener('focusin', e => record('focus', e), true);
  document.addEventListener('change', e => record('change', e), true);
  document.addEventListener('submit', e => record('submit', e), true);
  document.addEventListener('keydown', e => {
    const key = e.key === ' ' ? 'Space' : e.key;
    if (['Enter','Tab','Escape','ArrowUp','ArrowDown','Space'].includes(key))
      record('keydown', e, key);
  }, true);
  document.addEventListener('dialogopen', () => scheduleSnapshot('dialogopen'), true);
  window.addEventListener('beforeprint', () => emit({kind:'beforeprint'}));
  window.addEventListener('afterprint', () => emit({kind:'afterprint'}));
  window.addEventListener('popstate', checkRoute);
  window.addEventListener('hashchange', checkRoute);

  const observe = () => {
    if (observer || !document.documentElement) return;
    observer = new MutationObserver(() => {
      if (!active) return;
      checkRoute();
      scheduleSnapshot('mutation');
    });
    observer.observe(document.documentElement, {subtree:true, childList:true, attributes:true,
      attributeFilter:['aria-expanded','aria-selected','disabled','hidden','class','style']});
  };
  if (document.documentElement) observe();
  else document.addEventListener('DOMContentLoaded', observe, {once:true});

  window.__supraTemChanRecorder = {
    setActive(on) {
      active = !!on;
      if (active && allowed()) {
        checkRoute();
        snapshot('begin');
      } else {
        clearTimeout(snapshotTimer);
      }
    },
    snapshot: reason => snapshot(String(reason || 'manual').slice(0, 40))
  };
})();
