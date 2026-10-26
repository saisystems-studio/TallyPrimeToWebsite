# Safe Sales Register implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the user-approved safe synchronization design and SQL-only report detail, stopping before live apply.

**Architecture:** Separate validated immutable source snapshots, deterministic plans, protected file approval storage, orchestration and an EF transactional repository. Keep approval and progress outside SQL. Infer completed actions from exact approved SQL states for crash recovery.

**Tech Stack:** .NET 10 / EF Core SQL Server, React/Vite, console backend regression harness, Node test runner.

**Spec:** `docs/superpowers/specs/2026-10-23-safe-sales-register-design.md` (approved by user).

## Global Constraints

- No configured TallyWebDB writes or schema operations, no live apply.
- Only independent Sales Register tables are eventual write targets.
- Preserve April unless its exact source state changes; retain-and-block stored cancellations/absence.
- Never start the API/Sync Agent, access VPS, import Tally data or change SQL security during tests.

## Review Focus

- Crash after commit before progress persistence: infer exact approved after-state, never duplicate or overwrite unrelated changes.
- Expired/changed/foreign approvals: reject before writes.
- Partial exports and unknown cancellation flags: block, never infer deletions.
- Reused GUID across companies: isolate every query and transaction.
- Tally offline/selection race: stored detail remains correct and ignores stale results.

### Task 1: Plan engine and orchestration

**Files:** create `Services/SalesRegister/SalesSyncModels.cs`, `SalesSyncPlanner.cs`, `SalesSyncCoordinator.cs`, `SalesSyncPlanStore.cs`; new `tests/SalesRegisterSafety.Checks/`.

**Interfaces:** `ISalesSyncSource.ReadAsync(companyId, cancellationToken)` returns validated `SalesSourceSnapshot`; `ISalesSyncRepository.ReadAsync` returns company vouchers; `CommitAsync(plan, batch, token)` serializably checks approved states and writes bounded actions. `PreviewAsync`, `ApproveAsync`, `ApplyBatchAsync` produce exact manifests and explicit readiness flags.

- [x] Write red tests for preview actions, cancellation/absence blocking, duplicate/company isolation, source/SQL fingerprint drift, approval/operator/expiry, insert/update, retry/crash and rollback.
- [x] Implement immutable DTOs, canonical fingerprints, deterministic planner, protected atomic file store and one-batch coordinator.
- [x] Run tests green; preserve full source/stored fingerprints and action-level before/after evidence.

### Task 2: Real adapters and endpoints

**Files:** `SalesSyncSource.cs`, `SalesSyncRepository.cs`, existing Sales Register service/controller and Program DI registration; safety harness adapter tests.

**Interfaces:** source exports company metadata, bounded monthly identity/posting collections and repeated metadata checks; repository uses company-scoped SQL-only reads and serializable two-table writes. Authenticated routes bind approval to user ID and plan digest; legacy boolean apply rejects.

- [x] Add red tests for malformed XML/status, exact ordered postings, metadata drift and repository rollback/isolated query behavior.
- [x] Implement adapters with strict validation, disabled-by-default apply switch and a separate approval endpoint.
- [x] Verify no test uses configured SQL for writes; build into isolated tmp directory.

### Task 3: Stored-data report

**Files:** Sales Register controller, frontend service/page/model and new detail tests.

**Interfaces:** company-scoped stored detail and history GETs; detail adapter derives only from stored header/postings.

- [x] Add red tests for stored detail identity, company mismatch, signed posting sequence and financial-year selection.
- [x] Replace live Tally detail/enrichment with SQL-only detail/export, dynamic FY choices and stale-selection protection.
- [x] Run frontend suite and production build; preserve existing report controls/layout.

### Task 4: Evidence and review

**Files:** diagnostic safety harness read-only mode, `diagnostics/sales-register-safe-sync/` report and exact insert/update JSON manifest.

- [x] Run full source/SQL preview with SQL write-blocking interceptor and unchanged table hashes; create exact actions without granting approval.
- [x] Run all appropriate backend/frontend regressions and review safety-critical paths.
- [x] Report safeToApply, approvalGranted/applyEnabled, exact inserts/updates and remaining limits. STOP BEFORE APPLY.

