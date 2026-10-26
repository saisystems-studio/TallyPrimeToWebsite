# All-history Sales Register read-only discovery

**safeToApply: false. No SQL INSERT, UPDATE, DELETE or schema operation was executed. No apply endpoint was called.**

Company: **JAYA JOTHI MALIGAI**, SQL company ID 1. GUID matched to the loaded Tally company. Company ID 1 was selected for this run because it owns the existing April register referenced in the request; no company name is embedded in discovery logic.
Books From: **20210401**. Company-reported EndingAt: **20241231**. Earliest/latest Sales voucher: **20210401-20241231**. Types: Sales, GST Sales.

Discovered 5830 active Sales vouchers and 26 cancelled vouchers. Metadata and monthly exports cover the company-reported books/history range. Cancelled vouchers contribute neither postings nor active financial totals.

| Financial year | Active | Cancelled | Postings | Existing unchanged | Missing | Modified | Expected Sales total (INR) |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2021-2022 | 1617 | 0 | 3743 | 0 | 1617 | 0 | 3,27,92,837.01 |
| 2022-2023 | 1766 | 0 | 4169 | 0 | 1766 | 0 | 4,31,62,174.26 |
| 2023-2024 | 1595 | 0 | 4317 | 0 | 1595 | 0 | 5,46,72,864.58 |
| 2024-2025 | 852 | 26 | 2780 | 103 | 749 | 0 | 4,09,66,878.32 |

Existing independent SQL tables contain **103 headers, 250 postings, INR 42,41,929.10**. Missing source identities: 5727; modified: 0; stored absent/cancelled identities: 0. Counts of missing rows describe differences, not approved insert actions.

## Validation and preservation

Source validation passed: **True**. Duplicate source GUID groups: 0; ledger mapping errors: 0; unbalanced active vouchers: 0; validation errors: 0.

- april103Vouchers: **True**
- april250Postings: **True**
- aprilTotal: **True**
- mayDecember749Active: **True**
- mayDecember2530Postings: **True**
- mayDecember26CancelledExcluded: **True**
- aprilDecemberReferenceTotal: **True**
- sqlUnchanged: **True**

Before/after SHA256 of every column in both independent register tables: `5FDB4E0E214A4952CE2B15FAFBE52E7687BCE98EB2CA92F22406EC4FDFAAAF0A` / `5FDB4E0E214A4952CE2B15FAFBE52E7687BCE98EB2CA92F22406EC4FDFAAAF0A`. Identical: **True**.

Financial values are derived from signed party postings. Credit is positive Sales movement, Debit is its negative absolute value. Closing is cumulative net movement with Cr for positive and Dr for negative; the table below starts at Books From with zero report-period opening. A selected financial-year/custom report must restart cumulative movement at that selected period.

| Month | Active | Cancelled | Postings | Missing | Modified | Debit INR | Credit INR | All-history closing INR |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 202104 | 152 | 0 | 434 | 152 | 0 | 0.00 | 27,06,433.05 | 27,06,433.05 Cr |
| 202105 | 75 | 0 | 187 | 75 | 0 | 0.00 | 18,58,338.11 | 45,64,771.16 Cr |
| 202106 | 81 | 0 | 234 | 81 | 0 | 0.00 | 20,43,256.85 | 66,08,028.01 Cr |
| 202107 | 127 | 0 | 325 | 127 | 0 | 0.00 | 23,39,835.50 | 89,47,863.51 Cr |
| 202108 | 161 | 0 | 324 | 161 | 0 | 0.00 | 28,21,947.50 | 1,17,69,811.01 Cr |
| 202109 | 192 | 0 | 418 | 192 | 0 | 0.00 | 34,66,940.00 | 1,52,36,751.01 Cr |
| 202110 | 166 | 0 | 396 | 166 | 0 | 0.00 | 38,69,431.00 | 1,91,06,182.01 Cr |
| 202111 | 106 | 0 | 239 | 106 | 0 | 0.00 | 18,49,009.00 | 2,09,55,191.01 Cr |
| 202112 | 154 | 0 | 320 | 154 | 0 | 0.00 | 31,74,514.50 | 2,41,29,705.51 Cr |
| 202201 | 138 | 0 | 289 | 138 | 0 | 0.00 | 26,53,077.50 | 2,67,82,783.01 Cr |
| 202202 | 135 | 0 | 291 | 135 | 0 | 0.00 | 32,09,129.50 | 2,99,91,912.51 Cr |
| 202203 | 130 | 0 | 286 | 130 | 0 | 0.00 | 28,00,924.50 | 3,27,92,837.01 Cr |
| 202204 | 132 | 0 | 296 | 132 | 0 | 0.00 | 28,34,439.00 | 3,56,27,276.01 Cr |
| 202205 | 131 | 0 | 286 | 131 | 0 | 0.00 | 23,90,314.00 | 3,80,17,590.01 Cr |
| 202206 | 161 | 0 | 357 | 161 | 0 | 0.00 | 26,57,267.00 | 4,06,74,857.01 Cr |
| 202207 | 130 | 0 | 296 | 130 | 0 | 0.00 | 30,86,753.50 | 4,37,61,610.51 Cr |
| 202208 | 133 | 0 | 308 | 133 | 0 | 0.00 | 30,41,293.00 | 4,68,02,903.51 Cr |
| 202209 | 172 | 0 | 408 | 172 | 0 | 0.00 | 45,78,180.50 | 5,13,81,084.01 Cr |
| 202210 | 122 | 0 | 297 | 122 | 0 | 0.00 | 37,05,307.50 | 5,50,86,391.51 Cr |
| 202211 | 152 | 0 | 364 | 152 | 0 | 0.00 | 36,88,699.00 | 5,87,75,090.51 Cr |
| 202212 | 155 | 0 | 371 | 155 | 0 | 0.00 | 46,23,994.50 | 6,33,99,085.01 Cr |
| 202301 | 189 | 0 | 431 | 189 | 0 | 0.00 | 48,44,692.00 | 6,82,43,777.01 Cr |
| 202302 | 160 | 0 | 401 | 160 | 0 | 0.00 | 47,48,977.50 | 7,29,92,754.51 Cr |
| 202303 | 129 | 0 | 354 | 129 | 0 | 0.00 | 29,62,256.76 | 7,59,55,011.27 Cr |
| 202304 | 159 | 0 | 377 | 159 | 0 | 0.00 | 45,55,730.50 | 8,05,10,741.77 Cr |
| 202305 | 135 | 0 | 330 | 135 | 0 | 0.00 | 39,18,032.00 | 8,44,28,773.77 Cr |
| 202306 | 168 | 0 | 507 | 168 | 0 | 0.00 | 41,71,330.30 | 8,86,00,104.07 Cr |
| 202307 | 159 | 0 | 449 | 159 | 0 | 0.00 | 40,16,979.80 | 9,26,17,083.87 Cr |
| 202308 | 134 | 0 | 340 | 134 | 0 | 0.00 | 44,94,285.00 | 9,71,11,368.87 Cr |
| 202309 | 92 | 0 | 254 | 92 | 0 | 0.00 | 48,74,464.00 | 10,19,85,832.87 Cr |
| 202310 | 142 | 0 | 409 | 142 | 0 | 0.00 | 40,82,259.70 | 10,60,68,092.57 Cr |
| 202311 | 80 | 0 | 239 | 80 | 0 | 0.00 | 54,44,575.00 | 11,15,12,667.57 Cr |
| 202312 | 130 | 0 | 358 | 130 | 0 | 0.00 | 43,53,132.00 | 11,58,65,799.57 Cr |
| 202401 | 143 | 0 | 354 | 143 | 0 | 0.00 | 53,11,253.18 | 12,11,77,052.75 Cr |
| 202402 | 140 | 0 | 376 | 140 | 0 | 0.00 | 53,64,340.10 | 12,65,41,392.85 Cr |
| 202403 | 113 | 0 | 324 | 113 | 0 | 0.00 | 40,86,483.00 | 13,06,27,875.85 Cr |
| 202404 | 103 | 0 | 250 | 0 | 0 | 0.00 | 42,41,929.10 | 13,48,69,804.95 Cr |
| 202405 | 117 | 0 | 300 | 117 | 0 | 0.00 | 47,30,804.11 | 13,96,00,609.06 Cr |
| 202406 | 116 | 0 | 290 | 116 | 0 | 0.00 | 45,56,603.00 | 14,41,57,212.06 Cr |
| 202407 | 151 | 0 | 411 | 151 | 0 | 0.00 | 57,80,057.47 | 14,99,37,269.53 Cr |
| 202408 | 107 | 0 | 306 | 107 | 0 | 0.00 | 50,37,213.00 | 15,49,74,482.53 Cr |
| 202409 | 156 | 23 | 526 | 156 | 0 | 0.00 | 54,62,724.00 | 16,04,37,206.53 Cr |
| 202410 | 41 | 3 | 261 | 41 | 0 | 0.00 | 48,97,427.00 | 16,53,34,633.53 Cr |
| 202411 | 38 | 0 | 277 | 38 | 0 | 0.00 | 45,69,127.00 | 16,99,03,760.53 Cr |
| 202412 | 23 | 0 | 159 | 23 | 0 | 0.00 | 16,90,993.64 | 17,15,94,754.17 Cr |

## Blockers and remaining work

- Current apply only inserts; modified voucher updates are not supported.
- No approved stored deletion/cancellation reconciliation policy.
- Apply is not bound to an immutable approved preview or resumable batches.
- Full-history metadata is one lightweight export; source snapshot consistency is not guaranteed.
- Voucher drill-down currently retrieves live Tally detail rather than independent stored postings.
- Live website visual verification is outstanding.
- Current service incorrectly treats equal GUIDs in different companies as conflicts; identity must consistently use CompanyId plus TallyGuid, including transactional rechecks.
- The report GET can return all stored dates, but preview API requires explicit dates and has no company-history discovery endpoint. Financial-year history choices and truthful synchronization coverage still need integration.

## Verification and evidence limits

- Fresh API/backend regression-harness build passed: 0 errors. Fresh SalesRegisterSync.Checks execution passed posting-sign, duplicate, cancellation, malformed-active and legacy behavior checks.
- Frontend production Vite build passed (86 modules). All 48 discovered frontend tests passed, including nine Sales Register tests. No browser visual or export-dialog verification is claimed.
- Initial default backend build failed because the running API executable was locked; the successful build used a separate tmp output directory. No service was stopped or restarted.
- Existing package warnings NU1510/NU1902/NU1903 remain. No dependency or security settings were changed.
- A new standalone .NET discovery executable was blocked by Windows Application Control. Native PowerShell then performed the SELECT-only local SQL and Tally export discovery. No security controls were changed.
- Saved XML from this run was reused when narrowing oversized wildcard exports, then independently replayed against a fresh SQL snapshot. This is not a second fresh full Tally export. A new source preview and approval are required before any later apply.
- Export metadata is bounded by the company-reported BooksFrom and EndingAt, with a single metadata export and monthly posting batches. No atomic Tally snapshot is available; matching metadata/batch identities and SQL preservation do not prove that amounts stayed unchanged during the export. No apply readiness is inferred.
- Discovery reads SQL companies/ledgers and the independent register tables. It contains no SaveChanges, import request, apply call or SQL mutation. It does not start API/hosted services, access the VPS or modify the Sync Agent.

Evidence: `results/discovery.json`, `results/reference-checks.json`, `results/companies.xml`, `results/history-metadata.xml`, and monthly `results/source-YYYYMM.xml`. The generic discover.ps1 defaults to fresh source exports; `-ReplaySavedExports` is explicitly offline source replay with fresh SQL reads.

Implementation work is mapped in `docs/superpowers/plans/2026-10-23-sales-register-all-history.md`. Application code and SQL are unchanged by this discovery phase. Explicit user approval is required before any SQL apply, and the blockers above must be resolved first.
