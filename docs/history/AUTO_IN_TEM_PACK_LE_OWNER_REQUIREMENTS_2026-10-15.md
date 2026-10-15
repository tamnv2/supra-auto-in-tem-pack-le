# LƯU TRỮ LỊCH SỬ – KHÔNG PHẢI ĐẶC TẢ HIỆN HÀNH

**Nguồn:** nhánh proposal của repo Inventory ngày 15/10/2026. Tài liệu này chỉ để tham khảo nguồn gốc quyết định. **Quyết định hiện hành nằm ở `AGENTS.md` và `docs/PROJECT_AUTHORITY.md` trong repo mới.** Các hạn chế D169 và lựa chọn không cập nhật tự động cũ đã được thay bằng repo độc lập và yêu cầu cập nhật theo nút của Owner. Không dùng tài liệu này để mở rộng quyền WMS hoặc dữ liệu phiên.

---

# SUPRA Auto In Tem Pack Lẻ — Owner requirements confirmed 2026-10-15

**State:** OWNER_REQUIREMENTS_CONFIRMED_AS_DISCUSSION / OFF_SCOPE_WMS_PACKING / NO CODE / NO RELEASE / awaiting credential-storage choice and test permission.  
**Repository placement:** documentation on independent pre-existing proposal branch only. **No new Dxxx**, no merge to main, no WMS mutation, no new external resource provision; Inventory D169 remains open without final Owner PASS.  
**Source evidence:** sanitized review of Owner-supplied Recorder v2 ZIP (66 events, one 71.4s manual session), see `WMS_TOTE_LABEL_RECORDER_V2_LOG_REVIEW_2026-10-15.md`. Raw log and all personal/operational data must stay out of public Git history.

## 1. Approved business interaction for candidate Windows app

**Working product name:** `SUPRA Auto In Tem Pack Lẻ`. Name does **not** change the actual WMS button: Recorder captures **ĐÓNG GÓI HÀNG CHẴN**; do not silently change that step based on product name.

### Operator setup

- Dedicated Windows portable EXE, operator-owned embedded WebView2 displaying the authorized WMS packing site, with **Hiện Web / Ẩn Web** UI.
- Initially show WMS for manual login and **manual printer choice**. Printer setup in the one Recorder v2 sequence was caused by test operator error; **do not add printer setup to each Tote's auto-click flow**. On printer failure, operator presses Hiện Web to repair manually, then resumes safely; do not auto-reprint on uncertain outcome.
- WebView2 must remain functioning when the embedded WMS panel is hidden. Must test background DOM/page behavior on company Windows/Office network; changing visibility must not tear down navigation/browser process or lose session. Do not attempt network bypass, proxy interception, API access, DevTools Network capture or login replay.
- **No automatic software update.** For future changes, Owner accepts rebuilding/replacing package manually. No update channel/manifest/network check required or authorized.
- A separate WebView2 user-data/profile directory is requested; HOWEVER keeping WMS login across application launches normally persists cookies/session/auth material under WebView2's own profile. This conflicts with the Owner's absolute ban on storing tokens. **STOP for Owner's explicit choice before a final executable build:** (A) isolated *persistent* WebView2 profile using browser-native session cookie storage (application never accesses, exports or logs secrets), or (B) ephemeral/InPrivate profile/no persisted login data, requires manual login each program start. Do not assert both at once. Never put raw cookies/passwords/tokens into application logs/exports.

### Per-Tote UI and personnel management

1. Only two operational inputs visible at idle: **Mã Tote** and **Số kiện**. Operator types/scans both, presses **OK**.
2. Validate both fields locally: nonblank Tote; positive integer number of parcels, reasonable length/bounds; no mutation at this point. Show **the full list of saved names** (responsive grid / scrollbar for larger team). List is user-maintained **add/edit/delete**; shared two shifts. Default every app launch: **no personnel selected**. Selecting a name opens an explicit final confirmation dialog.
3. Confirm -> snapshot current Tote + number of parcels + selected operator in RAM, lock both inputs and personnel changes affecting active job until the cycle ends. Names may be stored locally for reuse (personal data not to be committed to public repo/logged by default). Do not need to show SO or derive SO from Tote.
4. The WMS embedded browser runs via manual UI interaction while shown or automated UI operations when future code is authorized. When cycle completes and the PTVC scan input/expected route is back, unlock Tote and parcel inputs, clear prior operator selection when starting a new app run (not necessarily every Tote), focus Tote ready for scan.

### Exact target WMS UI workflow (subject to semantic verification)

`READY_PTVC -> TOTE_ENTER -> ENTER -> PACKING_DETAIL -> click ĐÓNG GÓI HÀNG CHẴN -> wait dialog Số lượng -> CLEAR EXISTING VALUE ENTIRELY -> enter requested parcel count -> verify visible field value exactly matches requested count -> click Cập Nhật -> click Có in the correct confirmation dialog -> wait packing/label page -> click the correct order's print icon -> click In Nhãn in active dialog -> click HOÀN THÀNH ĐÓNG GÓI -> click Có (finish-confirmation) -> click Không (do not print inspection slip) -> wait navigation back to packing PTVC input -> job marked cycle-complete and unlock next Tote`.

**Critical correction:** do not append to existing parcel value (`5` + `6` must become `6`, never `56`). Confirm DOM field equality after assignment and after blur/change if relevant. `Cập Nhật` and `Có` must operate on the right modal with expected context. Re-query current DOM for each step; dynamic Angular IDs/classes, DOM indexes, coordinates or prior page snapshots are not sole selectors.

**Printing completion rule accepted by Owner:** do not require physical printer-spool/printed-label proof as app cycle success. Cycle completes only after the WMS view returns to correct Tote scan path/input. This is **UI_WORKFLOW_COMPLETE**, not `PHYSICAL_PRINT_VERIFIED`; retain this distinction in logs. No automatic print retry on uncertainty.

**Safe state and recovery:** `WAIT_LOGIN`, `READY`, `BUSY`, `WAIT_DIALOG`, `NEEDS_OPERATOR`, `UNCERTAIN`, `RETURNED_TO_PTVC`. User cannot submit another Tote during `BUSY`; a timeout, duplicate/missing target, expired session, unexpected navigation or uncertain side effect must suspend auto-click and offer `Hiện Web` for supervised recovery. If a different Tote is visibly active, do not confirm or print it. Exact one visible enabled semantic candidate in expected UI context.

## 2. Diagnostics and privacy

- Local logs for fault diagnosis: installation/app version, relative timing, event sequence, state transitions, allowed WMS path only (no query/fragment), labels/roles/semantic safe button names, expected vs observed step, non-sensitive error category, WebView2/runtime state and retry-suppression outcomes.
- Default logs should never contain entered Tote, parcel value (record only validity/mismatch status), personnel names, browser profile path with identifying username, WMS HTML/body, username/password, token, cookies, headers, query strings, local/session storage, print form contents, session identifiers or personal information.
- Nothing uploaded automatically. Support **Export log ZIP** on operator request with sanitation before packaging. No Firebase, Cloudflare, Apps Script, Google Drive or GitHub call is required for this standalone local workflow.
- User-managed personnel list is a separate local application-data store, **not diagnostic logs**; support edit/delete and privacy deletion.
- Printer and WMS availability errors are human-visible; do not hide failures as success. Log failure categories rather than exception strings that could include secrets.

## 3. Evidence and safeguards still pending

1. **Blocking security decision:** persistent WebView2 session/profile cookies (browser-native) vs absolute no persisted authentication material with required re-login. Need explicit Owner choice. No design can promise both 'persistent login' and 'no session tokens saved anywhere'.
2. **Unverified semantic selectors:** Recorder click `print` generic with no row/label association. One captured session has one exact `print` button, but future multi-row/label cases may expose multiple candidates. Must implement fail-closed uniqueness and validate at least representative multi-line UI. The `Có`/`Không` modal context must be checked by caption/body using a **sanitized** allowlist (no private text captured).
3. **Background WebView2 runtime:** field test whether WMS JS/dialog lifecycle works while operator panel is hidden and Office network is used.
4. **Quantity input mechanics:** Recorder deliberately did not collect typed values; clear/set/blur/change and exact readback need a read-only test with non-sensitive synthetic count (no WMS production transaction for testing without separate permission).
5. **Operating scope:** `ops/project-scope.json` registers WMS Confirm Picklist reference, **not** WMS saleorder packing mutation; the separate product needs exact registered, Owner-approved WMS packing scope and authorized real field test before building/running production auto-click. Inventory Agent, Worker/Android/Firebase and Stable remain untouched.
6. **No Owner PASS of D169:** serial project gate prevents opening/merging an unrelated Inventory feature as if part of D169. Keep this in an isolated proposal branch only pending governance/scope resolution.
7. **Build environment:** Windows x64 EXE/WebView2 package must be built/tested on Windows with actual Office network, including startup, Hide/Show persistence, printer login, error, interruption, navigation/timeout and manual overwrite installation. No release/field PASS from a local source-only test.

## 4. Impact and next step

**Accepted Inventory base:** no changes.  
**Affected components when separately approved:** NEW Windows application, its own local personnel store, its own log store, its own WebView2 isolated profile, WMS packing browser-UI (external provider), and manually installed binary/package.  
**Unaffected:** current inventory web/worker, Android, Agent Picklist/PRIMARY/standby, Firebase, Cloudflare, Apps Script, inventory session/update manifests, Stable; never share their profile/credentials/release channel.

**Next:** resolve auth persistence choice, confirm registered packing scope and authorize bounded UI test, verify semantic selectors/background behavior, then create portable build candidate on separate authorized development branch + CI, not a purported production PASS.
