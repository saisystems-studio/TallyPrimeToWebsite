# Sales Register synchronization

The report reads only `SalesRegister_tbl` / `SalesRegisterEntries_tbl`. No page refresh invokes synchronization. No migration is required for the selected **retain-and-block** policy.

## Endpoints

- `GET /api/sales-register?companyId=<id>[&fromDate=yyyyMMdd&toDate=yyyyMMdd]`: stored report. Both dates or neither are required.
- `GET /api/sales-register/periods?companyId=<id>`: stored date coverage and company financial-year start; never claims complete source coverage.
- `GET /api/sales-register/vouchers/<sql-id>?companyId=<id>`: stored header and ordered signed ledger postings. No Tally/shared-table fallback.
- `POST /api/sales-register/sync/preview?companyId=<id>`: discover complete company history, validate, persist a protected unapproved plan, return exact actions/digest/expiry/reconciliation. No SQL writes. Range query parameters are not used: approval covers full company history.
- `POST /api/sales-register/sync/approve`: authenticated Admin body `{ "planId": "<preview-id>", "digest": "<preview-digest>", "explicitlyApproved": true }`. Binds explicit approval to the preview owner and digest. No SQL writes.
- `POST /api/sales-register/sync/apply`: authenticated Admin body `{ "planId": "<approved-id>", "digest": "<approved-digest>" }`. Executes at most one bounded batch. Requires `SalesRegisterSync:ApplyEnabled=true` and explicit approval. **Do not call during the current task.**

Any previous apply request containing only company/dates or a boolean is rejected; the legacy service's boolean apply method is also disabled. New controller actions require the approved plan contract.

`validationReady` describes source/SQL validation. `approvalGranted` and `applyEnabled` are separate. Public `safeToApply` is conservative: all three must be true and the preview must remain unexpired. Missing current SQL vouchers are planned actions, not validation errors. Stored cancelled/missing-source vouchers are always blocking diagnostics; no header is deleted.

## Transactions and recovery

Preview plans contain exact baseline/after states, source fingerprint, owner, expiration and immutable digest. Each apply request freshly exports Tally and rechecks mappings and classification. A serializable transaction rechecks all company-scoped SQL baseline rows, then inserts or updates only approved vouchers. Updates replace signed postings in two saves inside the same transaction to respect the unique sequence index. Failure rolls back the entire batch. Unchanged rows, IDs and timestamps are not saved.

Progress is inferred from exact approved after-states in SQL. A crash after commit but before an HTTP acknowledgement does not require a SQL progress table and will not duplicate records on retry. A request never starts a background batch loop. A plan can expire mid-run; remaining work needs a new preview and approval. Reconciliation returns incomplete counts while batches remain and must match fully when no actions remain.

## Deployment prerequisites (not executed here)

- Rebuild/load this API and frontend before using these routes. The already running older API was not restarted by this task.
- For future local verification, use Development with `Startup:RunLegacyDatabaseInitialization=false`. Existing non-Development legacy initialization and general auto-sync are outside this feature and remain untouched; review those separately before a production deployment. This task never started the API in either environment.
- Apply defaults to **disabled**. Configuration defaults: `SalesRegisterSync:BatchSize=100` (clamped 1–250), `PreviewMinutes=30` (clamped 1–120). Enabling apply is a separate deployment action, not an approval substitute.
- `.sales-register-plans/` under the API content root must be persistent and accessible only to the application identity and administrators. Plan contents are protected with ASP.NET Data Protection; Windows keys use DPAPI under the application identity. Back up keys securely with the plans. Changing service identity/losing keys invalidates stored plans and requires fresh previews.
- Restrict directory permissions on non-Windows deployments and configure appropriate key encryption. This workflow targets one application host; multiple hosts require a shared protected plan store and cross-host locking before enabling apply.
- Preserve the existing unique `(CompanyId,TallyGuid)` and `(SalesRegisterId,EntrySequence)` indexes. No schema changes were executed or are required.
- Tally and SQL cannot form one atomic distributed snapshot. Per-request revalidation, metadata checks and transactional SQL checks detect drift but cannot lock Tally. Final read-only reconciliation is mandatory; failed later batches leave earlier complete batches committed and resumable.

## Tests and read-only diagnostic

`tests/SalesRegisterSafety.Checks` with no arguments runs fixture/XML/file-store tests and disposable in-memory SQLite write tests. It never loads configured SQL credentials in fixture mode. The existing `SalesRegisterSync.Checks` continues to cover legacy parser compatibility.

The explicit `--read-only-live <companyId>` mode uses a SELECT-only EF interceptor, local endpoint guards and complete before/after register hashes. It calls only source reads, repository reads and pure planning. It writes local diagnostic JSON files, not application approval plans, and never invokes approval/apply. `exact-actions.json` is a review manifest, not an executable or approved application plan.
