# Sales Register All-History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Discover and reconcile every available Sales period without SQL writes, then implement an explicitly approved transactional synchronization workflow.

**Architecture:** Keep discovery separate from API startup and hosted synchronization. Use company GUID matching, source history discovery and monthly exports to compare signed ordered postings against the two independent Sales Register tables. A later apply must be bound to an approved preview and revalidate the source and SQL snapshot.

**Tech Stack:** .NET 10, EF Core SQL Server, Tally XML, React/Vite; standalone PowerShell SELECT-only discovery.

**Spec:** User attachment `C:/Users/Sai_Dev_3/.codex/attachments/73b71a98-d1b5-4391-abcb-607ad27c4748/Pasted text.txt`.

## Global Constraints

- STOP BEFORE ANY SQL WRITE and wait for explicit user approval.
- Use only SalesRegister_tbl and SalesRegisterEntries_tbl for synchronization writes.
- Preserve existing April records unless source changes are verified.
- Never modify shared voucher tables, Tally company data, Windows Sync Agent, VPS or authentication/security settings.
- Reference figures belong in validation only; discover company, dates and voucher types dynamically.
- Page refresh never starts synchronization.

## Review Focus

- Tally source changes between discovery and apply must invalidate approval.
- A failed or incomplete export must never imply deletion.
- A GUID reused in another company must remain independently scoped.
- Unknown cancellation status or malformed active postings must block readiness.
- Voucher detail must show stored postings even when Tally is unavailable.

### Task 1: Read-only discovery and reconciliation (current deliverable)

**Files:** `diagnostics/sales-register-all-years/discover.ps1`, `diagnostics/sales-register-all-years/results/discovery.json`, `diagnostics/sales-register-all-years/report.md`.

**Interfaces:** Explicit company ID; local configured SQL and Tally endpoints; outputs company/date metadata, FY/month counts, missing/modified/cancelled identities, mapping and balance errors, SQL snapshot hashes and safeToApply.

- [x] Discover books and history boundaries; resolve Sales parent ancestry.
- [x] Export monthly signed postings and compare exact company-scoped voucher headers and ordered entries.
- [x] Compare metadata identities/counts with batches and SQL hashes before/after; report every unresolved blocker.
- [x] Verify April and May–December reference figures without encoding them in discovery logic.
- [x] Run frontend build, report regression tests and backend checks; distinguish fresh execution from previously compiled fixtures.

### Task 2: Approval-bound synchronization (future; no SQL execution authorized)

**Files:** `TallyWebAPI/TallyWebAPI/Services/SalesRegisterSyncService.cs`, `TallyWebAPI/TallyWebAPI/Controllers/SalesRegisterController.cs`, `tests/SalesRegisterSync.Checks/`.

**Interfaces:** Preview produces an immutable plan identifier, source/SQL fingerprints and insert/update/reconciliation actions; apply consumes explicit approval for that exact plan.

- [ ] Add failing cases for modified headers/postings, repeat runs, company-scoped equal GUIDs, unknown cancellation, partial exports, stale approval and failed batch rollback.
- [ ] Implement validated updates and ordered posting replacement in resumable serializable batches; never touch shared tables.
- [ ] Keep absent/cancelled stored records blocked until an explicit reconciliation policy is approved; do not infer deletion from a failed export.
- [ ] Revalidate fingerprints before each apply and reconcile stored identities, posting counts and totals after commit.
- [ ] Run fixture tests without live SQL writes. Live apply remains separately approval-gated.

### Task 3: Stored-data website drill-down and history selection (future)

**Files:** `TallyWebFrontend/src/pages/Reports/SalesRegister/SalesRegister.jsx`, `TallyWebFrontend/src/services/salesRegisterService.js`, Sales Register model/date tests, Sales Register controller.

**Interfaces:** Existing report GET returns independent stored postings; company-scoped history metadata supplies available periods. Voucher detail consumes the selected SQL voucher's postings.

- [ ] Add failing tests for offline stored detail, empty months, FY/custom/all-history filters, date/company refresh races and unsynchronized coverage notices.
- [ ] Replace live Tally detail retrieval with stored Sales Register entries; preserve export controls and report layout.
- [ ] Populate financial-year choices from discovered data and show truthful SQL coverage.
- [ ] Run build and report regressions; visually verify month/voucher navigation, scrolling, totals and export controls before claiming UI completion.
