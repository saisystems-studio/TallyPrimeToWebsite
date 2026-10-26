using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services.SalesRegister;

public sealed record SalesImportPreviewResult(SalesSyncPlan Plan, SalesReconciliation Reconciliation,
    int ExistingGuids, int MissingGuids, string[] ValidationErrors, int AprilVouchers,
    int AprilPostings, decimal AprilSalesCredit);
public sealed record SalesImportApplyResult(string PlanId, int InsertedVouchers, int InsertedPostings,
    SalesReconciliation Reconciliation, int AprilVouchers, int AprilPostings, decimal AprilSalesCredit);

/// <summary>Date-bounded, insert-only import path. It never updates or deletes Sales Register rows.</summary>
public sealed class BoundedSalesImportService(AppDbContext db, ISalesRangeSyncSource source,
    ISalesSyncRepository reader, ISalesSyncPlanStore plans, TimeProvider clock)
{
    private const int RequiredCompanyId = 1;
    private static readonly DateTime From = new(2024, 5, 1);
    private static readonly DateTime To = new(2024, 12, 31);

    public async Task<SalesImportPreviewResult> PreviewAsync(int companyId, string owner, CancellationToken token)
    {
        if (companyId != RequiredCompanyId) throw new ArgumentException("This import is restricted to CompanyId 1.");
        var snapshot = await source.ReadRangeAsync(companyId, From, To, token);
        if (!snapshot.CompanyName.Equals("JAYA JOTHI MALIGAI", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Company name does not match the approved import scope.");
        var current = await reader.ReadAsync(companyId, token);
        var scoped = current.Where(v => v.Date.Date >= From && v.Date.Date <= To).ToArray();
        var april = current.Where(v => v.Date.Year == 2024 && v.Date.Month == 4).ToArray();
        var aprilState = AprilState(april);
        var scopedSnapshot = snapshot with { ContextFingerprint = snapshot.ContextFingerprint + ":APRIL:" + aprilState.Hash };
        var plan = SalesSyncPlanner.Create(scopedSnapshot, scoped, owner, clock.GetUtcNow(), TimeSpan.FromMinutes(20));
        var blockers = plan.Blockers.ToList();
        if (plan.Actions.Any(a => a.Kind != "Insert")) blockers.Add("Existing voucher differs from Tally; insert-only import will not update it.");
        if (aprilState.Count != 103 || aprilState.Postings != 250 || aprilState.Credit != 4241929.10m)
            blockers.Add("April baseline does not match the required 103 vouchers, 250 postings and ₹42,41,929.10 credit.");
        plan = plan with { Blockers = blockers.Distinct().ToArray(), Digest = "" };
        plan = plan with { Digest = SalesSyncPlanner.Digest(plan) };
        await plans.SaveAsync(plan, token);
        var rec = SalesReconciliation.Compare(snapshot, scoped);
        return new(plan, rec, scoped.Length, rec.MissingGuids.Length, plan.Blockers,
            aprilState.Count, aprilState.Postings, aprilState.Credit);
    }

    public async Task<SalesImportApplyResult> ApplyAsync(string id, string digest, string owner, CancellationToken token)
    {
        await using var planLock = await plans.LockAsync(id, token);
        var plan = await plans.LoadAsync(id, token);
        if (string.IsNullOrWhiteSpace(owner) || plan.Owner != owner || plan.Digest != digest || SalesSyncPlanner.Digest(plan) != plan.Digest)
            throw new InvalidOperationException("Preview identity or digest does not match this operator and plan.");
        if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Preview expired; create a fresh preview.");
        if (!plan.SafeToApply || plan.Source.CompanyId != RequiredCompanyId || plan.Source.BooksFrom != From || plan.Source.Through != To || plan.Actions.Any(a => a.Kind != "Insert"))
            throw new InvalidOperationException("Preview is blocked or outside the approved date-bounded insert-only scope.");
        var preflight = await reader.ReadAsync(RequiredCompanyId, token);
        var preflightApril = AprilState(preflight.Where(v => v.Date.Year == 2024 && v.Date.Month == 4).ToArray());
        var freshSource = await source.ReadRangeAsync(RequiredCompanyId, From, To, token);
        freshSource = freshSource with { ContextFingerprint = freshSource.ContextFingerprint + ":APRIL:" + preflightApril.Hash };
        if (SalesSyncPlanner.SourceHash(freshSource) != plan.SourceFingerprint)
            throw new InvalidOperationException("Tally source or ledger mapping changed after preview; create a fresh preview.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            var actual = await reader.ReadAsync(RequiredCompanyId, token);
            var scoped = actual.Where(v => v.Date.Date >= From && v.Date.Date <= To).ToArray();
            var april = AprilState(actual.Where(v => v.Date.Year == 2024 && v.Date.Month == 4).ToArray());
            var previewAprilHash = plan.Source.ContextFingerprint[(plan.Source.ContextFingerprint.LastIndexOf(":APRIL:", StringComparison.Ordinal) + 7)..];
            if (april.Hash != previewAprilHash || april.Count != 103 || april.Postings != 250 || april.Credit != 4241929.10m)
                throw new InvalidOperationException("April baseline changed or failed its required preservation checks.");
            if (SalesSyncPlanner.Hash(scoped.OrderBy(v => SalesSyncPlanner.Key(v.TallyGuid)).Select(SalesSyncPlanner.VoucherHash)) !=
                SalesSyncPlanner.Hash(plan.Baseline.OrderBy(v => SalesSyncPlanner.Key(v.TallyGuid)).Select(SalesSyncPlanner.VoucherHash)))
                throw new InvalidOperationException("May–December SQL baseline changed after preview.");
            var insertGuids = plan.Actions.Select(a => SalesSyncPlanner.Key(a.Guid)).ToHashSet();
            if (scoped.Any(v => insertGuids.Contains(SalesSyncPlanner.Key(v.TallyGuid))))
                throw new InvalidOperationException("An approved missing GUID appeared in SQL after preview; create a fresh preview.");
            var headers = new List<SalesRegisterEntity>(); var now = clock.GetUtcNow().UtcDateTime;
            foreach (var action in plan.Actions)
            {
                var v = action.After;
                var header = new SalesRegisterEntity { CompanyId = RequiredCompanyId, TallyGuid = v.TallyGuid,
                    VoucherNumber = v.VoucherNumber, VoucherDate = v.Date.Date, VoucherType = v.VoucherType,
                    PartyLedgerName = v.PartyName, PartyLedgerGuid = v.PartyGuid, Amount = v.Amount,
                    CreatedAt = now, UpdatedAt = now,
                    Entries = v.Postings.OrderBy(p => p.Sequence).Select(p => new SalesRegisterEntryEntity {
                        LedgerGuid = p.LedgerGuid, LedgerName = p.LedgerName, SignedAmount = p.SignedAmount, EntrySequence = p.Sequence
                    }).ToList() };
                headers.Add(header);
            }
            if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("SQL context has unrelated pending writes.");
            db.SalesRegister.AddRange(headers);
            if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Entity is not SalesRegisterEntity and not SalesRegisterEntryEntity))
                throw new InvalidOperationException("Write scope includes a non-Sales-Register entity.");
            await db.SaveChangesAsync(token);
            var verified = await reader.ReadAsync(RequiredCompanyId, token);
            var afterScoped = verified.Where(v => v.Date.Date >= From && v.Date.Date <= To).ToArray();
            var finalApril = AprilState(verified.Where(v => v.Date.Year == 2024 && v.Date.Month == 4).ToArray());
            var reconciliation = SalesReconciliation.Compare(plan.Source, afterScoped);
            if (!reconciliation.Matches || reconciliation.MissingGuids.Length != 0 || reconciliation.ModifiedGuids.Length != 0 ||
                finalApril.Hash != april.Hash || afterScoped.Length != plan.Source.Active.Length)
                throw new InvalidOperationException("Post-insert reconciliation failed; transaction rolled back.");
            await tx.CommitAsync(token);
            return new(id, headers.Count, headers.Sum(h => h.Entries.Count), reconciliation,
                finalApril.Count, finalApril.Postings, finalApril.Credit);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.ChangeTracker.Clear(); }
    }

    private static (string Hash, int Count, int Postings, decimal Credit) AprilState(SalesVoucher[] april)
    {
        var credit = april.Sum(v => Math.Max(0m, SalesReconciliation.Movement(v)));
        return (SalesSyncPlanner.Hash(april.OrderBy(v => SalesSyncPlanner.Key(v.TallyGuid)).Select(SalesSyncPlanner.VoucherHash)),
            april.Length, april.Sum(v => v.Postings.Length), credit);
    }
}
