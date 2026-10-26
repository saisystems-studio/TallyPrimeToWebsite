# Safe Sales Register synchronization design

## Intent and constraints

Implement an operator-controlled, approval-bound Sales Register workflow using only the existing SalesRegister_tbl and SalesRegisterEntries_tbl for eventual SQL writes. During this task, all configured TallyWebDB access is SELECT-only. No live apply, schema change, shared voucher-table access, service restart, VPS access or Tally import is permitted.

## Approaches and chosen policy

The user chose **retain and block** for previously stored vouchers that become cancelled or disappear from a complete source export. Preserve their SQL records and return identity-level blocking diagnostics. Do not infer that an absent voucher was deleted: call it missing from source. Newly discovered cancelled vouchers remain diagnostics only. No schema change is required. Status columns with an unexecuted migration were considered and declined.

For approval persistence, use a protected local plan file rather than new SQL tables. The application can restart without losing its immutable approved action set. The builder proposes this storage choice because additional SQL tables are prohibited. If deployment does not provide persistent protected local storage, previews can be regenerated but approval cannot be resumed safely across restarts; this is an operational blocker, not permission to write SQL.

## Source and plan

Resolve the selected SQL company against Tally GUID and name. Discover BooksFrom and EndingAt from Tally rather than current calendar or hardcoded periods. Export monthly voucher collections using only required fields. Resolve standard Sales plus transitive custom Sales-parent types. Validate identities, dates, known cancellation status, company-scoped unique ledger mappings, party postings, balanced ordered signed postings, field lengths and SQL decimal precision before planning any action.

Source fingerprint includes company identity, history bounds, classification, active voucher headers and ordered signed postings, cancellation identities and ledger mappings. Incomplete exports, changed metadata across the export, duplicate source identities, invalid active rows and ambiguous ledger mappings block approval. Metadata identity counts must reconcile with full monthly exports. Re-read the source immediately before each apply request; reject any changed fingerprint. Tally does not offer an atomic snapshot with SQL, so final reconciliation remains mandatory and cannot roll back previously committed batches.

Create a random plan ID, immutable source/SQL fingerprints, operator identity, expiration (default 30 minutes, configurable), ordered exact insert/update actions, and expected before/after voucher states. Each update includes the complete verified source header and ordered postings. Unchanged vouchers are never written. A preview response includes the full actions and reconciliation totals; it is not approval.

## Approval and resumable apply

An authenticated operator explicitly approves the specific plan ID and digest in a separate request. Approval is bound to that operator and company. Approval creation performs no SQL writes. Apply accepts the approved plan ID/digest, never just company/dates or a boolean query parameter. The legacy boolean apply path must reject all attempts. Apply additionally defaults to disabled unless the deployment explicitly enables the Sales Register apply feature.

Protect plan contents with ASP.NET Data Protection and store atomically in an application-private directory. Persist keys only in the application-private plan directory; no SQL key storage. Acquire an exclusive per-plan file lease for cross-process execution. Reject expired/tampered/foreign-operator plans before source or SQL mutation. Do not log approval secrets or database credentials.

One explicit apply request executes at most one bounded batch (default 100, configurable with a strict maximum). No background worker or page refresh invokes apply. Re-read the complete source and company-scoped SQL baseline before the batch. Accept SQL differences only when they equal the exact approved after-state of an action already committed; reject unrelated changes, deleted unchanged rows and partial/mismatched voucher states. This makes SQL itself the durable progress evidence if the process stops after commit but before updating its plan file. Renewing an expired plan requires a new preview and approval, which naturally plans only remaining work.

Within a serializable SQL transaction, recheck all company-scoped expected states, insert new headers and ordered postings or replace the verified modified voucher's postings, and save only these two entity types. Roll back the entire batch on failure or cancellation. Preserve unchanged records' IDs and timestamps. CompanyId plus TallyGuid is the identity everywhere; equal GUIDs in other companies never block or mutate the selected company's data. Reconcile the batch before commit; return remaining actions and source/SQL totals after the batch. A final read-only reconciliation compares GUID sets, counts, ordered posting contents, monthly debit/credit/closing totals and cancellation exclusions.

## Stored report and UI

The report and voucher detail read only the independent Sales Register SQL tables. Add company-scoped SQL voucher detail and stored period metadata endpoints. Financial-year choices derive from stored date ranges and company financial-year start; custom and all-stored-data filters remain available. Report messages clearly describe stored coverage rather than claiming complete Tally synchronization.

Month clicks show the stored voucher list. Voucher clicks display stored voucher number/date/party/type/amount and signed ledger details. Remove live Tally detail retrieval and invoice-item enrichment from this report. Existing report Excel, PDF, Print and Share controls and its white scrolling container/footer remain. Detail exports use only stored voucher fields and postings; never fabricate invoice items absent from these tables. Guard asynchronous results against company/date/selection changes.

Debit/credit derive from movement opposite the signed party amount; closing is cumulative net movement within the selected report period, with Dr/Cr labels. Preserve posting signs and sequence. SQL reports remain usable when Tally is offline; synchronization endpoints fail closed when Tally is unavailable.

## Tests and final evidence

Use an in-process fake source and an isolated transactional test store for orchestration tests, plus disposable SQLite relational tests for the EF repository if the provider is available. Tests must never read configured database credentials to construct a write connection. Test insert, repeat/retry, interruption after commit, rollback, modifications, cancellation/missing-source blocks, expiration, source/SQL drift, approval/operator/company binding and company isolation. Test SQL query filtering and detail against isolated test data, and frontend stored-detail identity, date/FY/month summaries and exports.

Run builds without starting the API, migration/seeding or hosted services. Run backend and frontend regression suites. Produce an exact insert/update manifest and a full read-only reconciliation from live source and SELECT-only SQL. safeToApply reports validation/approval readiness separately from approvalGranted and applyEnabled. No live execution occurs in this task even if all validation becomes green.

## Acceptance and remaining operational limits

No schema migration is needed for the chosen retain-and-block policy. Durable plan directory availability, stable Data Protection keys and explicit apply enablement are deployment requirements. A stored cancelled/missing-source identity intentionally keeps safeToApply false until reviewed. Source changes or plan expiration require a fresh preview and approval. Multi-host deployment requires shared protected storage/locking or a future approved durable plan store; this implementation targets this single-host application.
