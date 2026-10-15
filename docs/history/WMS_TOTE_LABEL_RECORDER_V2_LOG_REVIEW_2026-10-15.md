# LƯU TRỮ LỊCH SỬ – KHÔNG PHẢI ĐẶC TẢ HIỆN HÀNH

**Nguồn:** nhánh proposal của repo Inventory ngày 15/10/2026. Tài liệu này chỉ để tham khảo nguồn gốc quyết định. **Quyết định hiện hành nằm ở `AGENTS.md` và `docs/PROJECT_AUTHORITY.md` trong repo mới.** Các hạn chế D169 và lựa chọn không cập nhật tự động cũ đã được thay bằng repo độc lập và yêu cầu cập nhật theo nút của Owner. Không dùng tài liệu này để mở rộng quyền WMS hoặc dữ liệu phiên.

---

# SUPRA Tote Label — Recorder v2 observation (15 Oct 2026)

**Evidence status:** LOG_REVIEW_ONLY; one Owner-controlled manual WMS session, 66 events, 71.413 seconds. No WMS/field/physical printer success proof and no auto-click approval.  
**Source:** privately supplied Recorder v2 exported ZIP; original private log is **not uploaded to this public repository**.  
**Governance:** draft notes in the existing independent `proposal/wms-tote-label-automation-20261015` branch only. Do not promote to canonical Inventory business policy, D169 OWNER_PASS, release or scope permission. WMS packing is not a registered WMS mutation resource in Inventory scope.

## Observed ordered actions (relative elapsed time)

| Offset | Observed Recorder event(s) | Evidence classification |
| --- | --- | --- |
| 0.013s | Operator SESSION_START at Tote entry screen | OPERATOR_BOUNDARY |
| 1.977s–3.007s | Click `input[placeholder="Quét mã PTVC để đóng gói"]`; Enter | DIRECT_UI_EVENT; Tote content not logged |
| 4.168s–5.899s | Button `ĐÓNG GÓI HÀNG CHẴN` becomes visible, operator clicks | DIRECT_UI_EVENT |
| 6.190s | Input `Số lượng`, dialog actions `Cập Nhật / Thoát` become available | DOM_OBSERVED; actual entry/value not observed |
| 15.874s | Click dialog `Cập Nhật` | DIRECT_UI_EVENT |
| 16.160s–17.142s | `Có / Không` dialog appears, operator clicks `Có` | DIRECT_UI_EVENT; prompt body not logged |
| 19.595s | Label/packing result page shows `print` icon and `HOÀN THÀNH ĐÓNG GÓI` | DOM_OBSERVED |
| 23.689s | First click on `print` icon, no `In Nhãn` action observed after it | DIRECT_UI_EVENT; print result uncertain |
| 27.545s–47.442s | Open menu -> `Thiết lập máy in` -> combobox `Chọn` -> unlabelled `role=option` -> `Xác nhận` | DIRECT_UI_EVENT; printer identity/selected option not recorded |
| 50.960s–53.516s | Second click `print`; dialog `In Nhãn / Thoát` appears; click `In Nhãn` | DIRECT_UI_EVENT, no physical print receipt |
| 64.053s–65.449s | Click `HOÀN THÀNH ĐÓNG GÓI`; in subsequent `Có / Không` dialog choose `Có` | DIRECT_UI_EVENT, prompt body unknown |
| 66.479s–67.525s | Second `Có / Không` dialog appears; choose `Không` | DIRECT_UI_EVENT; interpreting this as `Không in phiếu kiểm` is unverified without modal text |
| 67.620s–69.386s | Navigate and page/input `Quét mã PTVC để đóng gói` reappears | NAVIGATE+DOM_OBSERVED |
| 71.413s | Operator SESSION_END | OPERATOR_BOUNDARY; not printer/transaction proof |

## Findings and ambiguity

1. Recorder includes click targets with literal button captions and semantic fields: this is better than a click-position trace. However `mat-option` has no captured option text; `print` is generic and may appear more than once; `Có/Không` lacks dialog title/body/step context. Dynamic Angular IDs and CSS classes are not sufficient automation identifiers.
2. The printer setup menu was visited *after* the first print-icon click and *before* the successful display of `In Nhãn` dialog. It is **not proved** that setup is required per Tote, per login, per device or once per printer. Propose printer readiness check before processing, not blindly reproducing the setup sequence for every Tote.
3. The `Số lượng` input is observed but not change/input logged, by privacy-preserving Recorder design. It is **not possible** to prove a quantity was typed, retained and applied, nor its numeric value. Do not infer this from time gap.
4. `In Nhãn` click plus dialog close indicates the action was invoked, not that printer completed physical output. No WMS transaction receipt, job identifier or printer spool completion is evidenced. Do not auto-retry label printing on uncertain outcome.
5. Navigation back to Tote field is strong evidence of UI workflow reset, not independently validated WMS order state. For automation, require post-action DOM state and appropriate verified WMS result while avoiding direct WMS API/session interception.

## Proposed finite-state workflow, not Owner-approved executable design

`WAIT_OPERATOR_LOGIN -> IDLE_TOTE_READY -> TOTE_ENTERED -> PACK_ACTION_AVAILABLE -> PACK_QUANTITY_DIALOG -> PACK_UPDATE_CONFIRMATION -> PACKED_PAGE -> PRINTER_READY_CHECK -> PRINT_DIALOG -> PRINT_REQUESTED -> FINISH_CONFIRMATION -> INSPECTION_SLIP_CHOICE -> TOTE_READY_AGAIN`, with explicit `HUMAN_REQUIRED/UNCERTAIN/CANCELLED/ERROR` branches.

- Semantic DOM anchors must be role+accessible name+dialog context+expected current stage. Require exactly one visible enabled match; do not click by screen position, CSS index or dynamic HTML ID.
- Maintain explicit confirmation for each state transition, bounded timeout, no implicit retry for side-effect steps `Cập Nhật`, `In Nhãn`, `HOÀN THÀNH`; never auto-print if uncertain.
- Setup device/printer only when evidence shows not ready; identity and driver choice remain user controlled. No external resource provisioning or API call approved.
- In standalone EXE (future only): keep WebView2 profile/update channel/diagnostics separate from Picklist Agent. Preserve startup/idle-only update and rollback; no updates mid-Tote.
- Test boundary conditions: missing/multiple DOM targets, slow network, differing screen sizes, detached popup, login expiration, multiple print buttons, reused printer setup, cancellation, printer failure and returning to Tote without printing.

## Owner confirmations requested before auto-click implementation

1. Is printer selection expected **once per computer/login**, or once per Tote? What is the exact user-visible sign that printer is ready?
2. Is `Số lượng` always **entered by operator** for each Tote, or should the program read/default it? Is `Cập Nhật -> Có` the complete save/confirmation sequence?
3. Confirm that **first `Có` after `HOÀN THÀNH ĐÓNG GÓI`** means confirm order completion, and **last `Không`** means don't print inspection slip. Get modal headings/screenshots if different.
4. Can the first `print` click legitimately open an external print window without `In Nhãn` dialog, or was it failing because printer was not selected?
5. Which WMS indicator represents a transaction actually completed, and what should the EXE do if a print request was sent but no print receipt is available?

**Next authorized step:** Owner reviews/corrects this proposed sequence, then a bounded recorder follow-up only if required. No WMS auto-click code/release/merge until registered scope and approval gates are satisfied. Nothing from this analysis is Owner acceptance.
