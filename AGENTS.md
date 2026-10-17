# Agent instructions — SUPRA Auto In Tem Pack Lẻ

Canonical repository: tamnv2/supra-auto-in-tem-pack-le. **Completely separate** from tamnv2/supra-inventory. Never import credentials, service accounts, session storage, code logic authority or real user data from the other product.

Read in order before modifying:
1. **docs/HANDOFF_2026-10-17.md — current decisions, status, field-validation backlog (highest priority over superseded historical lines)**
2. README.md
3. docs/PROJECT_AUTHORITY.md (chronological history; older instructions may be superseded)
4. current source files and .github/workflows/build-windows.yml
5. latest GitHub PR and Actions logs/artifacts
6. sanitized field logs supplied by Owner privately

## Development and release
- Branch -> PR -> Windows Actions compile PASS -> review -> merge. Ship EXE ZIP artifact for field testing; field PASS requires real Windows + WMS + printer testing, never inferred from source or CI.
- Preserve user-approved flow: Tote scan/Enter, parcel count/Enter, staff selection confirmation, WMS browser embedded in a persistent TabPage with manual login+printer setup and tab switching, guarded WMS DOM sequence, local history and diagnostics export.
- Safety invariants: exact role/name/context target not screen coordinates or dynamic IDs; count must replace existing value then readback exact; one Tote at a time; no automatic retry of non-idempotent actions; on uncertain result stop and request manual WMS review.
- Data: person's name and SO/DO stored ONLY in authorized local business history, not debug logs; business history XLSX; sanitized JSONL ZIP by date range; no automatic uploads.
- Security: WebView2 now has a persistent per-user browser-managed profile to allow opt-in saved passwords. Profile may retain authentication state. Application code must never inspect, log or export passwords, cookies, tokens, authorization data, WMS HTML or network request/response. Never commit real browser profile or business data.
- Updates: manual ZIP/EXE installation only; no in-app updater or PowerShell installer. Optional autostart uses HKCU Run only for current standard user, with no elevation or policy bypass.
- Field status: first release is trial candidate until Owner confirms on actual packing workflow. EXE compilation is not printer-output verification.

## Documentation continuity
Every new Owner decision, observation, defect and accepted fix should be recorded in docs/PROJECT_AUTHORITY.md or another dated document in this repo. Never claim completion without commit SHA, CI evidence and real field evidence levels.
