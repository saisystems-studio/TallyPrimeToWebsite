# Safe Sales Register infrastructure and read-only reconciliation

**STOP BEFORE APPLY. No SQL writes or schema changes were executed against TallyWebDB.**

Fresh source preview: 23/10/2026, 11:56:15 am IST. Company: **JAYA JOTHI MALIGAI** (CompanyId 1, GUID 5bd36bbb-820e-4784-b692-fe9891019a11). Company ID was supplied at runtime; no sample company is encoded in application logic.

Books From **2021-04-01**; available Sales dates **2021-04-01 through 2024-12-31**. Company-reported history end 2024-12-31. Sales types: GST Sales, Sales.

## Readiness

- validationReady: **true**; validation blockers: **0**.
- approvalGranted: **false**. No application approval plan was created or approved.
- applyEnabled: **false** (disabled by default).
- safeToApply: **false**. Public safeToApply includes validation, explicit approval, enablement and expiry; validationReady exposes the separate data result.

## Exact expected changes

**5,727 new voucher headers and 14,759 signed postings; 0 updates; 103 unchanged existing vouchers.** Newly discovered 26 cancelled vouchers are diagnostics only. There are no approved deletions.

The exhaustive [exact-actions.json](exact-actions.json) lists every CompanyId/GUID, voucher number, date, type, party, amount and ordered signed posting. Manifest SHA256: `97FE8B535E330C7B5F406E0E6F261113D51CFEEC046F2DF6E6F3EA4762D10A83`. It is a read-only review manifest, **not a registered/approved executable application plan**; its diagnostic plan ID cannot authorize apply.

| Financial year | Active source | Stored SQL | Source postings | Missing headers | Cancelled | Expected Sales credit INR |
|---|---:|---:|---:|---:|---:|---:|
| 2021-2022 | 1617 | 0 | 3743 | 1617 | 0 | 3,27,92,837.01 |
| 2022-2023 | 1766 | 0 | 4169 | 1766 | 0 | 4,31,62,174.26 |
| 2023-2024 | 1595 | 0 | 4317 | 1595 | 0 | 5,46,72,864.58 |
| 2024-2025 | 852 | 103 | 2780 | 749 | 26 | 4,09,66,878.32 |

Active source: **5830 vouchers, 15009 postings, INR 17,15,94,754.17 Cr**. Current SQL: **103 vouchers, 250 postings**. Current SQL is intentionally incomplete: reconciliation.matches=false because 5727 active source GUIDs are missing. Unexpected SQL GUIDs=0; modified stored GUIDs=0.

All four years were discovered dynamically. Raw counts, GUID differences, posting counts, cancellations and month-by-month source/SQL debit/credit/cumulative closing are in [reconciliation.json](reconciliation.json). Closing below is cumulative over all available history, starting at zero report-period opening; a selected financial-year/custom report starts its cumulative movement at that selected period.

| Month | Source vouchers | SQL vouchers | Source postings | SQL postings | Source credit INR | SQL credit INR | Source closing INR | SQL closing INR | Cancelled excluded |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 202104 | 152 | 0 | 434 | 0 | 27,06,433.05 | 0.00 | 27,06,433.05 Cr | 0.00  | 0 |
| 202105 | 75 | 0 | 187 | 0 | 18,58,338.11 | 0.00 | 45,64,771.16 Cr | 0.00  | 0 |
| 202106 | 81 | 0 | 234 | 0 | 20,43,256.85 | 0.00 | 66,08,028.01 Cr | 0.00  | 0 |
| 202107 | 127 | 0 | 325 | 0 | 23,39,835.50 | 0.00 | 89,47,863.51 Cr | 0.00  | 0 |
| 202108 | 161 | 0 | 324 | 0 | 28,21,947.50 | 0.00 | 1,17,69,811.01 Cr | 0.00  | 0 |
| 202109 | 192 | 0 | 418 | 0 | 34,66,940.00 | 0.00 | 1,52,36,751.01 Cr | 0.00  | 0 |
| 202110 | 166 | 0 | 396 | 0 | 38,69,431.00 | 0.00 | 1,91,06,182.01 Cr | 0.00  | 0 |
| 202111 | 106 | 0 | 239 | 0 | 18,49,009.00 | 0.00 | 2,09,55,191.01 Cr | 0.00  | 0 |
| 202112 | 154 | 0 | 320 | 0 | 31,74,514.50 | 0.00 | 2,41,29,705.51 Cr | 0.00  | 0 |
| 202201 | 138 | 0 | 289 | 0 | 26,53,077.50 | 0.00 | 2,67,82,783.01 Cr | 0.00  | 0 |
| 202202 | 135 | 0 | 291 | 0 | 32,09,129.50 | 0.00 | 2,99,91,912.51 Cr | 0.00  | 0 |
| 202203 | 130 | 0 | 286 | 0 | 28,00,924.50 | 0.00 | 3,27,92,837.01 Cr | 0.00  | 0 |
| 202204 | 132 | 0 | 296 | 0 | 28,34,439.00 | 0.00 | 3,56,27,276.01 Cr | 0.00  | 0 |
| 202205 | 131 | 0 | 286 | 0 | 23,90,314.00 | 0.00 | 3,80,17,590.01 Cr | 0.00  | 0 |
| 202206 | 161 | 0 | 357 | 0 | 26,57,267.00 | 0.00 | 4,06,74,857.01 Cr | 0.00  | 0 |
| 202207 | 130 | 0 | 296 | 0 | 30,86,753.50 | 0.00 | 4,37,61,610.51 Cr | 0.00  | 0 |
| 202208 | 133 | 0 | 308 | 0 | 30,41,293.00 | 0.00 | 4,68,02,903.51 Cr | 0.00  | 0 |
| 202209 | 172 | 0 | 408 | 0 | 45,78,180.50 | 0.00 | 5,13,81,084.01 Cr | 0.00  | 0 |
| 202210 | 122 | 0 | 297 | 0 | 37,05,307.50 | 0.00 | 5,50,86,391.51 Cr | 0.00  | 0 |
| 202211 | 152 | 0 | 364 | 0 | 36,88,699.00 | 0.00 | 5,87,75,090.51 Cr | 0.00  | 0 |
| 202212 | 155 | 0 | 371 | 0 | 46,23,994.50 | 0.00 | 6,33,99,085.01 Cr | 0.00  | 0 |
| 202301 | 189 | 0 | 431 | 0 | 48,44,692.00 | 0.00 | 6,82,43,777.01 Cr | 0.00  | 0 |
| 202302 | 160 | 0 | 401 | 0 | 47,48,977.50 | 0.00 | 7,29,92,754.51 Cr | 0.00  | 0 |
| 202303 | 129 | 0 | 354 | 0 | 29,62,256.76 | 0.00 | 7,59,55,011.27 Cr | 0.00  | 0 |
| 202304 | 159 | 0 | 377 | 0 | 45,55,730.50 | 0.00 | 8,05,10,741.77 Cr | 0.00  | 0 |
| 202305 | 135 | 0 | 330 | 0 | 39,18,032.00 | 0.00 | 8,44,28,773.77 Cr | 0.00  | 0 |
| 202306 | 168 | 0 | 507 | 0 | 41,71,330.30 | 0.00 | 8,86,00,104.07 Cr | 0.00  | 0 |
| 202307 | 159 | 0 | 449 | 0 | 40,16,979.80 | 0.00 | 9,26,17,083.87 Cr | 0.00  | 0 |
| 202308 | 134 | 0 | 340 | 0 | 44,94,285.00 | 0.00 | 9,71,11,368.87 Cr | 0.00  | 0 |
| 202309 | 92 | 0 | 254 | 0 | 48,74,464.00 | 0.00 | 10,19,85,832.87 Cr | 0.00  | 0 |
| 202310 | 142 | 0 | 409 | 0 | 40,82,259.70 | 0.00 | 10,60,68,092.57 Cr | 0.00  | 0 |
| 202311 | 80 | 0 | 239 | 0 | 54,44,575.00 | 0.00 | 11,15,12,667.57 Cr | 0.00  | 0 |
| 202312 | 130 | 0 | 358 | 0 | 43,53,132.00 | 0.00 | 11,58,65,799.57 Cr | 0.00  | 0 |
| 202401 | 143 | 0 | 354 | 0 | 53,11,253.18 | 0.00 | 12,11,77,052.75 Cr | 0.00  | 0 |
| 202402 | 140 | 0 | 376 | 0 | 53,64,340.10 | 0.00 | 12,65,41,392.85 Cr | 0.00  | 0 |
| 202403 | 113 | 0 | 324 | 0 | 40,86,483.00 | 0.00 | 13,06,27,875.85 Cr | 0.00  | 0 |
| 202404 | 103 | 103 | 250 | 250 | 42,41,929.10 | 42,41,929.10 | 13,48,69,804.95 Cr | 42,41,929.10 Cr | 0 |
| 202405 | 117 | 0 | 300 | 0 | 47,30,804.11 | 0.00 | 13,96,00,609.06 Cr | 42,41,929.10 Cr | 0 |
| 202406 | 116 | 0 | 290 | 0 | 45,56,603.00 | 0.00 | 14,41,57,212.06 Cr | 42,41,929.10 Cr | 0 |
| 202407 | 151 | 0 | 411 | 0 | 57,80,057.47 | 0.00 | 14,99,37,269.53 Cr | 42,41,929.10 Cr | 0 |
| 202408 | 107 | 0 | 306 | 0 | 50,37,213.00 | 0.00 | 15,49,74,482.53 Cr | 42,41,929.10 Cr | 0 |
| 202409 | 156 | 0 | 526 | 0 | 54,62,724.00 | 0.00 | 16,04,37,206.53 Cr | 42,41,929.10 Cr | 23 |
| 202410 | 41 | 0 | 261 | 0 | 48,97,427.00 | 0.00 | 16,53,34,633.53 Cr | 42,41,929.10 Cr | 3 |
| 202411 | 38 | 0 | 277 | 0 | 45,69,127.00 | 0.00 | 16,99,03,760.53 Cr | 42,41,929.10 Cr | 0 |
| 202412 | 23 | 0 | 159 | 0 | 16,90,993.64 | 0.00 | 17,15,94,754.17 Cr | 42,41,929.10 Cr | 0 |

Source/SQL monthly debit columns are also recorded in JSON (all zero for this captured history).

## Validation and SQL preservation

- april103Unchanged: **true**.
- april250Postings: **true**.
- aprilTotal: **true**.
- mayDecember749Active: **true**.
- mayDecember2530Postings: **true**.
- known26CancelledExcluded: **true**.
- aprilDecemberReferenceTotal: **true**.
- activeGuidsUnique: **true**.
- orderedBalancedPostings: **true**.
- noValidationBlockers: **true**.
- sqlUnchanged: **true**.
- repeatedReadOnlyPlanUnchanged: **true**.
- exactManifestCounts: **true**.

Full before/after SHA256 (both tables, every column and NULL, all companies, ordered by ID): `5FDB4E0E214A4952CE2B15FAFBE52E7687BCE98EB2CA92F22406EC4FDFAAAF0A` / `5FDB4E0E214A4952CE2B15FAFBE52E7687BCE98EB2CA92F22406EC4FDFAAAF0A`. **Identical.** April remains 103 headers, 250 postings, INR 42,41,929.10, with no planned writes to those rows.

The new source adapter made export-only Tally requests: company/history discovery, voucher classifications, monthly ledger exports and repeated identity/classification/company checks. It validates mappings, exact date bounds, cancellation status, ordered postings, balances and SQL precision. The live diagnostic used a SELECT-only interceptor and local endpoint guards, directly constructing services without API startup, hosted synchronization, migrations or seeding. Repeated planning used the same captured source plus fresh SQL reads; this was not a second full fresh Tally export.

## Implemented files and behavior

- `Services/SalesRegister/SalesSyncModels.cs`, `SalesSyncPlanner.cs`: immutable snapshots, exact insert/update actions, fingerprints, validation and retain-and-block cancellation/absence policy.
- `SalesSyncCoordinator.cs`, `SalesSyncPlanStore.cs`: operator/digest/expiry-bound explicit approval, protected durable files, exclusive plan lease, disabled-by-default one-batch apply and recovery from exact committed SQL after-state.
- `SalesSyncSource.cs`, `SalesSyncRepository.cs`, `SalesReconciliation.cs`: full dynamic history extraction, immediately-before-apply source revalidation, company-scoped serializable transactions, rollback, ordered posting replacement and complete reconciliation.
- `Controllers/SalesRegisterController.cs`, `Program.cs`: authenticated preview; Admin approval/apply; SQL-only stored detail/period endpoints; DI and Windows DPAPI key protection. Legacy unbound apply service write block removed and boolean apply explicitly rejected.
- Frontend `SalesRegister.jsx`, `salesRegisterStored.js`, `salesRegisterService.js`: dynamic stored financial-year selection, month/voucher drill-down from independent SQL, signed postings and stored-only detail exports. Removed live Tally/shared invoice enrichment from this report. Existing report exports, scrolling and anchored footer remain.
- `tests/SalesRegisterSafety.Checks/`: isolated write-path/rollback, orchestration, protected-store, source adapter and controller/date/company tests; explicit separate SELECT-only live diagnostic mode. New frontend stored-detail/FY/export tests.
- Existing InvoiceData, LedgerReport and StockSummary fixture harnesses now supply a mocked non-live base address required by the existing transport contract. This corrected pre-existing fixture setup failures; no unrelated application module was changed.

## Test results and review

- Fresh API/backend safety harness build: passed, 0 errors. New safety suite: passed deterministic insert/update/cancellation/absence/duplicate validation; approval/owner/digest/expiry/enablement; drift; bounded batches; interruption/retry; real disposable SQLite rollback, duplicate prevention, posting replacement, ID/timestamp preservation, company isolation and stored detail/date filtering; protected plan persistence/tamper/path/lease checks; XML validation and metadata drift.
- Frontend: all 53 tests passed; targeted ESLint passed; Vite production build passed (87 modules).
- InvoiceData, LedgerReport and StockSummary default mocked backend suites passed after fixture base-address correction (StockSummary: 0 failed checks).
- Existing legacy SalesRegisterSync parser fixtures passed in the baseline run. The freshly rebuilt legacy executable compiled but Windows Application Control blocked execution (0x800711C7). The new isolated safety suite ran successfully; no control/security setting was bypassed or changed.
- Independent read-only code review: no Critical/Important safety defect confirmed. Kept public safeToApply conservative; consumers seeking validation-only readiness use validationReady.
- Existing NU1510/NU1902/NU1903 package warnings and existing TallyService nullable warning remain; dependencies of the application were not changed. SQLite provider is test-only.

## Remaining blockers and limits

1. No operator approval has been granted and apply remains disabled. Load the rebuilt safe API/frontend before creating a fresh application preview and approval. No API/Sync Agent was restarted during this task; the older running API has not been replaced. Future local validation should use Development with Startup:RunLegacyDatabaseInitialization=false. Existing non-Development legacy initialization/general auto-sync remain untouched and require separate deployment review.
2. A persistent application-private plan/key directory and stable service identity are deployment prerequisites. Windows keys use DPAPI. Multi-host or non-Windows deployment needs appropriate shared protected storage/locking and key protection before enabling apply.
3. Tally cannot be locked atomically with SQL. Fresh per-request revalidation and metadata checks detect drift but do not create a distributed snapshot; final reconciliation is mandatory and earlier successful batches remain committed if a later request fails or expires.
4. Stored cancelled/missing-source vouchers intentionally retain SQL rows and block apply for review. No schema migration is needed or executed under this chosen policy.
5. Disposable SQLite tests do not prove SQL Server contention/range-lock/collation behavior. The plan lease was tested between store instances within one process. Actual browser in-flight company/date-switch interactions, print/share dialogs and visual layout were not integration-tested.
6. Fresh execution of the rebuilt legacy parser harness is blocked by Windows Application Control; baseline parser checks and the new source/safety suites passed. No OS or SQL security settings were changed.

**No INSERT, UPDATE, DELETE or schema operation was executed against TallyWebDB.** Write tests ran only in disposable in-memory SQLite. No shared voucher data, unrelated SQL table, Tally company data, Sync Agent, VPS or SQL security setting was modified. Application code remains uncommitted in the user-approved checkout; no merge/push/deployment occurred.

STOP BEFORE APPLY. This report and exact action manifest are for review. A future apply requires explicit user authorization plus a fresh unexpired application preview, operator approval and separately enabled apply configuration.
