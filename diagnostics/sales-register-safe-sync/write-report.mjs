import fs from "node:fs";
import crypto from "node:crypto";
const dir = "diagnostics/sales-register-safe-sync";
const r = JSON.parse(fs.readFileSync(`${dir}/reconciliation.json`, "utf8"));
const source = JSON.parse(fs.readFileSync(`${dir}/validated-source.json`, "utf8"));
const actions = JSON.parse(fs.readFileSync(`${dir}/exact-actions.json`, "utf8"));
const sumMoney = values => values.reduce((s, v) => s + Math.round(Number(v) * 100), 0) / 100;
const money = v => Number(v).toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const monthly = r.reconciliation.monthly;
const april = monthly.find(m => m.month === "202404");
const later = monthly.filter(m => m.month >= "202405" && m.month <= "202412");
const reference = monthly.filter(m => m.month >= "202404" && m.month <= "202412");
const checks = {
  april103Unchanged: april.sourceVouchers === 103 && april.sqlVouchers === 103 && !actions.some(a => a.before?.date.startsWith("2024-04") || (a.kind === "Update" && a.after.date.startsWith("2024-04"))),
  april250Postings: april.sourcePostings === 250 && april.sqlPostings === 250,
  aprilTotal: Math.round(april.sourceCredit * 100) === 424192910 && Math.round(april.sqlCredit * 100) === 424192910,
  mayDecember749Active: later.reduce((s, m) => s + m.sourceVouchers, 0) === 749,
  mayDecember2530Postings: later.reduce((s, m) => s + m.sourcePostings, 0) === 2530,
  known26CancelledExcluded: later.reduce((s, m) => s + m.cancelledExcluded, 0) === 26,
  aprilDecemberReferenceTotal: Math.round(sumMoney(reference.map(m => m.sourceCredit - m.sourceDebit)) * 100) === 4096687832,
  activeGuidsUnique: new Set(source.active.map(v => v.tallyGuid.toUpperCase())).size === source.active.length,
  orderedBalancedPostings: source.active.every(v => v.postings.every((p, i) => p.sequence === i) && v.postings.reduce((s, p) => s + Math.round(p.signedAmount * 10000), 0) === 0),
  noValidationBlockers: r.validationReady && !r.blockers.length && !r.errors.length,
  sqlUnchanged: r.sqlUnchanged && r.beforeSha256 === r.afterSha256,
  repeatedReadOnlyPlanUnchanged: r.repeatActionsUnchanged,
  exactManifestCounts: actions.length === r.inserts + r.updates && actions.filter(a => a.kind === "Insert").reduce((s, a) => s + a.after.postings.length, 0) === r.insertPostings,
};
fs.writeFileSync(`${dir}/reference-checks.json`, JSON.stringify(checks, null, 2));
const checksum = crypto.createHash("sha256").update(fs.readFileSync(`${dir}/exact-actions.json`)).digest("hex").toUpperCase();
const lines = [
  "# Safe Sales Register infrastructure and read-only reconciliation", "",
  "**STOP BEFORE APPLY. No SQL writes or schema changes were executed against TallyWebDB.**", "",
  `Fresh source preview: ${new Date(r.generatedAt).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })} IST. Company: **${r.companyName}** (CompanyId ${r.companyId}, GUID ${r.companyGuid}). Company ID was supplied at runtime; no sample company is encoded in application logic.`, "",
  `Books From **${r.booksFrom.slice(0, 10)}**; available Sales dates **${r.earliestSales.slice(0, 10)} through ${r.latestSales.slice(0, 10)}**. Company-reported history end ${r.through.slice(0, 10)}. Sales types: ${source.salesTypes.join(", ")}.`, "",
  "## Readiness", "",
  `- validationReady: **${r.validationReady}**; validation blockers: **${r.blockers.length}**.`,
  `- approvalGranted: **${r.approvalGranted}**. No application approval plan was created or approved.`,
  `- applyEnabled: **${r.applyEnabled}** (disabled by default).`,
  `- safeToApply: **${r.safeToApply}**. Public safeToApply includes validation, explicit approval, enablement and expiry; validationReady exposes the separate data result.`, "",
  "## Exact expected changes", "",
  `**${r.inserts.toLocaleString("en-IN")} new voucher headers and ${r.insertPostings.toLocaleString("en-IN")} signed postings; ${r.updates} updates; ${r.unchanged} unchanged existing vouchers.** Newly discovered ${r.reconciliation.cancelledExcluded} cancelled vouchers are diagnostics only. There are no approved deletions.`, "",
  `The exhaustive [exact-actions.json](exact-actions.json) lists every CompanyId/GUID, voucher number, date, type, party, amount and ordered signed posting. Manifest SHA256: \`${checksum}\`. It is a read-only review manifest, **not a registered/approved executable application plan**; its diagnostic plan ID cannot authorize apply.`, "",
  "| Financial year | Active source | Stored SQL | Source postings | Missing headers | Cancelled | Expected Sales credit INR |",
  "|---|---:|---:|---:|---:|---:|---:|",
];
for (const fy of r.financialYears) lines.push(`| ${fy.financialYear} | ${fy.active} | ${fy.stored} | ${fy.postings} | ${fy.active - fy.stored} | ${fy.cancelled} | ${money(fy.credit)} |`);
lines.push("", `Active source: **${r.reconciliation.sourceVouchers} vouchers, ${r.reconciliation.sourcePostings} postings, INR ${money(sumMoney(r.financialYears.map(y => y.credit - y.debit)))} Cr**. Current SQL: **${r.reconciliation.sqlVouchers} vouchers, ${r.reconciliation.sqlPostings} postings**. Current SQL is intentionally incomplete: reconciliation.matches=${r.reconciliation.matches} because ${r.reconciliation.missingGuids.length} active source GUIDs are missing. Unexpected SQL GUIDs=${r.reconciliation.unexpectedGuids.length}; modified stored GUIDs=${r.reconciliation.modifiedGuids.length}.`, "",
  "All four years were discovered dynamically. Raw counts, GUID differences, posting counts, cancellations and month-by-month source/SQL debit/credit/cumulative closing are in [reconciliation.json](reconciliation.json). Closing below is cumulative over all available history, starting at zero report-period opening; a selected financial-year/custom report starts its cumulative movement at that selected period.", "",
  "| Month | Source vouchers | SQL vouchers | Source postings | SQL postings | Source credit INR | SQL credit INR | Source closing INR | SQL closing INR | Cancelled excluded |",
  "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
for (const m of monthly) lines.push(`| ${m.month} | ${m.sourceVouchers} | ${m.sqlVouchers} | ${m.sourcePostings} | ${m.sqlPostings} | ${money(m.sourceCredit)} | ${money(m.sqlCredit)} | ${money(Math.abs(m.sourceClosing))} ${m.sourceClosing > 0 ? "Cr" : m.sourceClosing < 0 ? "Dr" : ""} | ${money(Math.abs(m.sqlClosing))} ${m.sqlClosing > 0 ? "Cr" : m.sqlClosing < 0 ? "Dr" : ""} | ${m.cancelledExcluded} |`);
lines.push("", "Source/SQL monthly debit columns are also recorded in JSON (all zero for this captured history).", "", "## Validation and SQL preservation", "");
for (const [name, result] of Object.entries(checks)) lines.push(`- ${name}: **${result}**.`);
lines.push("", `Full before/after SHA256 (both tables, every column and NULL, all companies, ordered by ID): \`${r.beforeSha256}\` / \`${r.afterSha256}\`. **Identical.** April remains 103 headers, 250 postings, INR 42,41,929.10, with no planned writes to those rows.`, "",
  "The new source adapter made export-only Tally requests: company/history discovery, voucher classifications, monthly ledger exports and repeated identity/classification/company checks. It validates mappings, exact date bounds, cancellation status, ordered postings, balances and SQL precision. The live diagnostic used a SELECT-only interceptor and local endpoint guards, directly constructing services without API startup, hosted synchronization, migrations or seeding. Repeated planning used the same captured source plus fresh SQL reads; this was not a second full fresh Tally export.", "",
  "## Implemented files and behavior", "",
  "- `Services/SalesRegister/SalesSyncModels.cs`, `SalesSyncPlanner.cs`: immutable snapshots, exact insert/update actions, fingerprints, validation and retain-and-block cancellation/absence policy.",
  "- `SalesSyncCoordinator.cs`, `SalesSyncPlanStore.cs`: operator/digest/expiry-bound explicit approval, protected durable files, exclusive plan lease, disabled-by-default one-batch apply and recovery from exact committed SQL after-state.",
  "- `SalesSyncSource.cs`, `SalesSyncRepository.cs`, `SalesReconciliation.cs`: full dynamic history extraction, immediately-before-apply source revalidation, company-scoped serializable transactions, rollback, ordered posting replacement and complete reconciliation.",
  "- `Controllers/SalesRegisterController.cs`, `Program.cs`: authenticated preview; Admin approval/apply; SQL-only stored detail/period endpoints; DI and Windows DPAPI key protection. Legacy unbound apply service write block removed and boolean apply explicitly rejected.",
  "- Frontend `SalesRegister.jsx`, `salesRegisterStored.js`, `salesRegisterService.js`: dynamic stored financial-year selection, month/voucher drill-down from independent SQL, signed postings and stored-only detail exports. Removed live Tally/shared invoice enrichment from this report. Existing report exports, scrolling and anchored footer remain.",
  "- `tests/SalesRegisterSafety.Checks/`: isolated write-path/rollback, orchestration, protected-store, source adapter and controller/date/company tests; explicit separate SELECT-only live diagnostic mode. New frontend stored-detail/FY/export tests.",
  "- Existing InvoiceData, LedgerReport and StockSummary fixture harnesses now supply a mocked non-live base address required by the existing transport contract. This corrected pre-existing fixture setup failures; no unrelated application module was changed.", "",
  "## Test results and review", "",
  "- Fresh API/backend safety harness build: passed, 0 errors. New safety suite: passed deterministic insert/update/cancellation/absence/duplicate validation; approval/owner/digest/expiry/enablement; drift; bounded batches; interruption/retry; real disposable SQLite rollback, duplicate prevention, posting replacement, ID/timestamp preservation, company isolation and stored detail/date filtering; protected plan persistence/tamper/path/lease checks; XML validation and metadata drift.",
  "- Frontend: all 53 tests passed; targeted ESLint passed; Vite production build passed (87 modules).", 
  "- InvoiceData, LedgerReport and StockSummary default mocked backend suites passed after fixture base-address correction (StockSummary: 0 failed checks).",
  "- Existing legacy SalesRegisterSync parser fixtures passed in the baseline run. The freshly rebuilt legacy executable compiled but Windows Application Control blocked execution (0x800711C7). The new isolated safety suite ran successfully; no control/security setting was bypassed or changed.",
  "- Independent read-only code review: no Critical/Important safety defect confirmed. Kept public safeToApply conservative; consumers seeking validation-only readiness use validationReady.",
  "- Existing NU1510/NU1902/NU1903 package warnings and existing TallyService nullable warning remain; dependencies of the application were not changed. SQLite provider is test-only.", "",
  "## Remaining blockers and limits", "",
  "1. No operator approval has been granted and apply remains disabled. Load the rebuilt safe API/frontend before creating a fresh application preview and approval. No API/Sync Agent was restarted during this task; the older running API has not been replaced. Future local validation should use Development with Startup:RunLegacyDatabaseInitialization=false. Existing non-Development legacy initialization/general auto-sync remain untouched and require separate deployment review.",
  "2. A persistent application-private plan/key directory and stable service identity are deployment prerequisites. Windows keys use DPAPI. Multi-host or non-Windows deployment needs appropriate shared protected storage/locking and key protection before enabling apply.",
  "3. Tally cannot be locked atomically with SQL. Fresh per-request revalidation and metadata checks detect drift but do not create a distributed snapshot; final reconciliation is mandatory and earlier successful batches remain committed if a later request fails or expires.",
  "4. Stored cancelled/missing-source vouchers intentionally retain SQL rows and block apply for review. No schema migration is needed or executed under this chosen policy.",
  "5. Disposable SQLite tests do not prove SQL Server contention/range-lock/collation behavior. The plan lease was tested between store instances within one process. Actual browser in-flight company/date-switch interactions, print/share dialogs and visual layout were not integration-tested.",
  "6. Fresh execution of the rebuilt legacy parser harness is blocked by Windows Application Control; baseline parser checks and the new source/safety suites passed. No OS or SQL security settings were changed.", "",
  "**No INSERT, UPDATE, DELETE or schema operation was executed against TallyWebDB.** Write tests ran only in disposable in-memory SQLite. No shared voucher data, unrelated SQL table, Tally company data, Sync Agent, VPS or SQL security setting was modified. Application code remains uncommitted in the user-approved checkout; no merge/push/deployment occurred.", "",
  "STOP BEFORE APPLY. This report and exact action manifest are for review. A future apply requires explicit user authorization plus a fresh unexpired application preview, operator approval and separately enabled apply configuration."
);
fs.writeFileSync(`${dir}/report.md`, lines.join("\n") + "\n");
console.log(JSON.stringify({ checks, manifestSha256: checksum, report: `${dir}/report.md` }, null, 2));
if (Object.values(checks).some(v => !v)) process.exitCode = 1;

