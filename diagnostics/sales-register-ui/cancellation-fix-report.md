# Cancelled Sales voucher handling — verified read-only preview

22 October 2026. Company: JAYA JOTHI MALIGAI; GUID 5bd36bbb-820e-4784-b692-fe9891019a11. Period: 1 May–31 December 2024.

## Root cause and correction
VoucherService previously parsed ALLLEDGERENTRIES.LIST before checking cancellation state. Cancelled Sales vouchers returned by Tally contain empty party/posting elements, so posting validation aborted the entire period before SalesRegisterSyncService could classify or exclude them.

The independent register now uses GetSalesRegisterVouchersAsync, explicitly fetching Tally IsCancelled. The direct ISCANCELLED XML child is trimmed and compared case-insensitively: Yes is cancelled, No is active. Missing/unrecognized status fails active validation; it is never silently treated as cancellation. Detection is based on the source flag, not blank parties, zero amounts, GUIDs, numbers or dates.

Cancelled vouchers retain GUID, number, date, voucher type and raw cancellation status for response diagnostics. Their postings are not parsed and no financial amount is fabricated. They are removed before active GUID duplicate checks, ledger mapping, balance validation, totals and insertion planning. Reused cancelled identities/numbers therefore do not create false active duplicates. Active GUID uniqueness, existing company-scoped GUID checks and cross-company protection remain in force. Active amounts and signed full ledger postings use the original parser unchanged. Malformed active postings remain invalid and block readiness.

## New-build full preview results
| Month 2024 | Active / missing SQL | Cancelled excluded | Signed postings | Source total (INR) | Reference difference |
|---|---:|---:|---:|---:|---:|
| May | 117 | 0 | 300 | 47,30,804.11 | 0.00 |
| June | 116 | 0 | 290 | 45,56,603.00 | 0.00 |
| July | 151 | 0 | 411 | 57,80,057.47 | 0.00 |
| August | 107 | 0 | 306 | 50,37,213.00 | 0.00 |
| September | 156 | 23 | 526 | 54,62,724.00 | 0.00 |
| October | 41 | 3 | 261 | 48,97,427.00 | 0.00 |
| November | 38 | 0 | 277 | 45,69,127.00 | 0.00 |
| December | 23 | 0 | 159 | 16,90,993.64 | 0.00 |

775 raw Sales rows = 749 active + 26 cancelled. 749 missing active headers and 2,530 postings planned; total INR 3,67,24,949.22. Including existing April, INR 4,09,66,878.32. All monthly references match exactly. Sales classification includes Sales and GST Sales via Tally parent Sales.

Full response: cancellation-full-preview.json. preview=true, applied=false, safeToApply=true. Invalid active vouchers=0; duplicate active GUID rows=0; cross-company GUID matches=0; unbalanced active vouchers=0; ledger mapping errors=0; validationErrors=[]; unmapped posting ledgers=[]. Cancelled identity/status diagnostics are included in the response.

## SQL preservation and execution method
Executed the newly built service directly through CancellationPreview with apply:false. The harness constructs AppDbContext and source clients without API startup, seeding, migrations or hosted synchronization. Its EF command interceptor allows SELECT-only reader commands and rejects nonquery commands. Direct table checks/snapshots are SELECT-only.

Entire SalesRegister_tbl and SalesRegisterEntries_tbl contents (all columns, ID order, including NULLs) were hashed before and after the preview. Both SHA256 values: 5FDB4E0E214A4952CE2B15FAFBE52E7687BCE98EB2CA92F22406EC4FDFAAAF0A. April remains 103 headers, 250 postings, INR 42,41,929.10. See cancellation-sql-verification.json.

The existing running API was not restarted/replaced; this evidence is from the newly compiled service, not the older running API process. Load the corrected build before a future operational preview/apply. No sync/apply endpoint, SQL mutation, schema change, Windows Sync Agent restart, Tally company modification or VPS access occurred.

## Changed files and validation
- TallyWebAPI/TallyWebAPI/DTOs/VoucherDto.cs: source cancellation/error metadata only; no database entity/schema changes.
- TallyWebAPI/TallyWebAPI/Services/VoucherService.cs: independent opt-in cancellation-aware source reader; legacy callers retain their previous behavior.
- TallyWebAPI/TallyWebAPI/Services/SalesRegisterSyncService.cs: exclude cancelled rows from active planning/validation; monthly and cancellation diagnostics.
- tests/SalesRegisterSync.Checks/Program.cs: regression cases for empty cancelled postings, identity reuse, active duplicate retention, mixed-case status, explicit source fetch, malformed active postings, unknown status and legacy compatibility.
- tests/SalesRegisterSync.Checks/CancellationPreview.cs: guarded read-only full-period verification, monthly reference assertions, and SQL snapshot assertions. Verification constants exist only in the test harness.

Regression initially failed because the cancellation-aware path was absent; after correction all fixture checks passed. API and harness build passed with 0 errors. Existing build warnings remain: NU1510, NU1902 and NU1903 (System.Security.Cryptography.Pkcs 9.0.11 advisory warnings). No package changes were made. Full live preview and exact SQL preservation assertions passed with exit code 0.

## Stop point
safeToApply=true establishes validation readiness, not authorization. No insertion has occurred. Stop here; explicit approval is required before any synchronization apply.
