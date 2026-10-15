# LƯU TRỮ LỊCH SỬ – KHÔNG PHẢI ĐẶC TẢ HIỆN HÀNH

**Nguồn:** nhánh proposal của repo Inventory ngày 15/10/2026. Tài liệu này chỉ để tham khảo nguồn gốc quyết định. **Quyết định hiện hành nằm ở `AGENTS.md` và `docs/PROJECT_AUTHORITY.md` trong repo mới.** Các hạn chế D169 và lựa chọn không cập nhật tự động cũ đã được thay bằng repo độc lập và yêu cầu cập nhật theo nút của Owner. Không dùng tài liệu này để mở rộng quyền WMS hoặc dữ liệu phiên.

---

# Auto In Tem Pack Lẻ — Owner decision and independent code preview, 2026-10-15

**Authority:** latest Owner instructions in this conversation, 2026-10-15 VN. **Status:** OWNER_APPROVED_TO_PREPARE_WINDOWS_APPLICATION; **NOT** WMS field PASS, GitHub release PASS, D169 Owner PASS or Stable authorization.  
**Governance:** main has D169 still OPEN, PR #606 Draft/UNMERGED. Owner invited a separate Dxxx if necessary, but the mandatory serial Owner-PASS policy prohibits opening a new Dxxx or merging unrelated scope while D169 remains unaccepted. Keep this as a **standalone project proposal branch**, not a new canonical Dxxx.  
**Target resource:** WMS browser `/sft3/app/saleorder/packing`. Registered `supra-wms-ui-d080` in Inventory scope is limited to confirm-picklist UI; packing mutation **not yet registered**. Standalone WMS operation must not be activated/represented as in-scope Inventory release until dedicated scope/resource approval and verification. Owner has approved the business proposal, but the repo scope registration and release workflow remain pending.

## Latest Owner-confirmed product requirements

- Working product name: **SUPRA Auto In Tem Pack Lẻ**, Windows x64 portable EXE based on **WebView2**; independent of existing Inventory Agent, Windows/browser profile and all cloud providers.
- On every app start user must manually login WMS again: **no persistent authentication**. WebView2 should use a dedicated per-run **InPrivate** profile; the application never reads/saves/logs cookies, password, token, headers, authentication or session material. Internal browser-managed transient data are never exported.
- User manually selects printer once after WMS login; printer-setting steps in Recorder v2 were an operator mistake and are **excluded** from the per-Tote flow. When print fails, user opens browser and repairs manually; no blind automatic retry.
- Maintain a **local editable/deletable/addable employee-name list across two shifts**, but **no preselected person** on application startup.
- Main view has **Tote + parcel-count** fields. Submit OK -> show **all staff names**, select one -> explicit confirmation. No click automation until confirmation.
- One bounded WMS UI cycle: set Tote, Enter -> **ĐÓNG GÓI HÀNG CHẴN** -> **clear existing parcel count fully**, set exact new number, verify -> Cập Nhật -> Có -> exactly one context-identified print icon -> In Nhãn -> HOÀN THÀNH ĐÓNG GÓI -> Có -> Không (**do not print inspection slip**) -> wait for correct PTVC field/page return. **Only then unlock next Tote**.
- WebView2 can be covered/hidden and shown for login/printer/manual repair; underlying browser remains alive. Ambiguous/missing selectors or expired session result in stop + Show Web, never coordinate/index-based guessing. Side-effect steps must not be retried blindly.
- User considers returning to the WMS Tote scan page an acceptable *UI-cycle completion* signal; it does **not** verify paper label physical output.
- Save locally: `dd/MM/yyyy`, start/end `HH:mm:ss`, duration, selected staff name, SO and DO extracted from the WMS panel **THÔNG TIN SO ĐANG ĐÓNG GÓI**. Do not store Tote unless separately approved. Manage staff list locally.
- Bounded local diagnostic trace for root-cause/reliability/latency analysis; never record credentials, tokens, raw WMS HTML, raw input, unbounded error message, session identifiers, URL query or sensitive WMS response data. Local business history explicitly contains staff, SO and DO as approved and should only be exported with user confirmation. No automatic cloud upload.
- **NO automatic update**; Owner accepts rebuilding/replacing EXE manually to suit Office network.

## Code-preview status and evidence

- A standalone Windows .NET 8 WinForms + WebView2 source prototype has been prepared as a conversation artifact **outside canonical repo source** (do not assume GitHub source is synchronized). The source bundle includes `MainForm.cs`, `WmsWorkflowJs.cs`, `LocalStore.cs`, `Program.cs`, `SupraAutoInTemPackLe.csproj`, PowerShell Windows build script, manual, and read-only mock/static tests.
- **Static JS syntax validation:** PASS with Node 22.
- **Mock DOM behavior:** PASS for Tote field, replacement of quantity 5 -> 6 (not 56), single print target, ambiguous two print targets blocked, distinct final dialog requirement, and nonallowed host block. This is NOT actual WMS proof.
- **Static source guard:** PASS for DOM allowlist, no cookie/storage/network inspection calls, InPrivate setup intent, history shape, validation and CSV export.
- **Compilation/build:** NOT VERIFIED. The authoring environment is Linux/Debian with no dotnet/MSBuild/Wine and no Internet DNS to bootstrap SDK; it cannot truthfully supply a compiled Windows EXE. The source ZIP has a Windows build script that installs official .NET SDK to user storage when necessary and then publishes self-contained win-x64 EXE on a Windows machine with Internet access.
- **Office/WMS/printer test:** NOT EXECUTED. Source can be reviewed/build-tested once governance and actual Office Windows environment are available. Do not call it PRODUCTION/RELEASE PASS.

## Risk and verification gates

1. WMS pack mutation resource scope/governance (D169 serial Owner PASS still open).
2. Windows compilation and startup with WebView2 InPrivate controller; no fallback to persistent-profile login.
3. Real DOM SO/DO extraction and exact parcel-count readback after clear/assignment.
4. Correct rendered/hidden WebView2 behavior, login, printer selection, print button and multiple-dialog identity on Office network.
5. Deterministic failure recovery with no double print/duplicate packing.
6. Confirm timestamp/date and local SO/DO/person history, diagnostic redaction and export permission; test against synthetic records first.
7. Preserve current Inventory Agent, Android, Worker and Stable unchanged; do not touch their authentication/session/update channels.

**Next step:** owner receives the source artifact and can arrange a Windows build on an Internet-enabled machine, then share compile result; before real WMS auto-click deployment reconcile registered WMS packing scope and D169 governance. When those gates pass, execute bounded controlled field tests and cut a separate signed/versioned EXE release.
