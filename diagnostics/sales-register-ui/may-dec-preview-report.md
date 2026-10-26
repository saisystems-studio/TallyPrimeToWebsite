# May–December Sales Register read-only preview and presentation correction

Date: 22 October 2026. Company: JAYA JOTHI MALIGAI, SQL ID 1, GUID 5bd36bbb-820e-4784-b692-fe9891019a11. SQL server Sai_Dev_2\SQLEXPRESS, database TallyWebDB.

| Month 2024 | Raw Sales vouchers | Cancelled with empty postings | Active / missing SQL | Active postings | Tally amount | SQL amount | Reference difference |
|---|---:|---:|---:|---:|---:|---:|---:|
| May | 117 | 0 | 117 | 300 | 47,30,804.11 | 0.00 | 0.00 |
| June | 116 | 0 | 116 | 290 | 45,56,603.00 | 0.00 | 0.00 |
| July | 151 | 0 | 151 | 411 | 57,80,057.47 | 0.00 | 0.00 |
| August | 107 | 0 | 107 | 306 | 50,37,213.00 | 0.00 | 0.00 |
| September | 179 | 23 | 156 | 526 | 54,62,724.00 | 0.00 | 0.00 |
| October | 44 | 3 | 41 | 261 | 48,97,427.00 | 0.00 | 0.00 |
| November | 38 | 0 | 38 | 277 | 45,69,127.00 | 0.00 | 0.00 |
| December | 23 | 0 | 23 | 159 | 16,90,993.64 | 0.00 | 0.00 |

Totals: 775 raw Sales vouchers, including 26 confirmed cancelled entries; 749 active missing vouchers, 2,530 valid signed postings, INR 36,724,949.22. With April INR 4,241,929.10, April–December reconciles to INR 40,966,878.32. References are used only in diagnostic comparisons, not production calculations.

## Read-only validation
The existing API preview path was inspected before calling it. Only sync/preview was called; never sync/apply. It verifies the SQL company against Tally GUID, fetches Tally voucher types, and includes Sales and GST Sales (both have Sales parent). Six monthly previews returned preview=true, applied=false, safeToApply=true, zero validation errors, no duplicate/cross-company GUID matches, no unexpected existing period rows, and complete company-scoped ledger mappings. Distinct mapped posting ledger names: May 17, June 15, July 14, August 15, November 19, December 17.

September/October API previews return HTTP 409: Invalid voucher ledger posting. Raw XML exports using the same collection request identify 23 and 3 empty Sales vouchers respectively. An independent read-only status export confirms ISCANCELLED=Yes and ISOPTIONAL=No for all 26 GUIDs. These are cancelled vouchers, not financial postings to insert. Their empty list elements must not be counted as valid postings or fabricated into zero-value vouchers.

After separating those confirmed cancellations in diagnostics only, September's 156 active vouchers and October's 41 active vouchers have valid company GUIDs, unique voucher GUIDs, valid in-range dates, voucher numbers, parties, a single valid party amount, company-scoped unique ledger mappings, and signed postings balancing exactly to zero. All non-empty posting names map; September has 23 distinct non-empty names and October 22. Raw counts of posting list elements include one empty placeholder per cancelled voucher (549 and 264); valid financial posting counts are 526 and 261. The SQL tables contain no May–December headers. All raw GUIDs, including cancelled ones, are missing from SQL, but only the 749 active vouchers are eligible candidates.

The final SELECT-only SQL check still returned only April: 103 headers, INR 4,241,929.10, and 250 total postings. No database records or schema were changed.

## Presentation correction
The former UI displayed the party receivable side as the Sales Register side. The supplied TallyPrime reference reports the opposing sales movement. salesMovementAmount derives the opposite of signedPartyAmount strictly for this report; the original API/SQL posting values are untouched. Negative party movements therefore appear as Credit; opposite movements appear as Debit. Closing uses cumulative Credit minus Debit over the selected reporting period, displayed as absolute amount plus Cr or Dr. No opening balance before the selected range is invented.

April now calculates Debit INR 0.00, Credit INR 4,241,929.10 and Closing INR 4,241,929.10 Cr. Header-based Grand Total stays INR 4,241,929.10 with no posting double-count. Summary, drill-down, and export presentation use the same movement convention. Existing container CSS and Excel/PDF/Print/Share mechanisms are retained.

Changed frontend files: SalesRegister.jsx, salesRegisterModel.js, salesRegisterModel.test.mjs, salesRegisterDates.test.mjs. Backend parser/services were not modified.

Nine Sales Register tests passed, including April fixture reconciliation, non-mutation of original posting signs, Cr/Dr formatting, cumulative closing across months and empty months, date grouping and dynamic presets. Production Vite build passed to tmp/sales-register-credit-format-build (86 modules). Browser click-through, print/share dialogs and visual TallyPrime comparison were not executed; browser access was unavailable earlier in this session.

## Synchronization readiness and stop point
Active source data reconciles for every requested month. May–August and November–December pass the existing preview implementation. The current end-to-end May–December apply path is NOT ready: cancelled-voucher handling must first be corrected and revalidated for September/October. Preserve cancellation metadata and explicitly exclude cancelled vouchers before requiring financial postings; do not silently ignore malformed active vouchers. Then rerun the read-only preview and obtain explicit user approval before any insertion. No apply, SQL writes, table creation, existing voucher-table changes, agent restart or VPS access occurred.
