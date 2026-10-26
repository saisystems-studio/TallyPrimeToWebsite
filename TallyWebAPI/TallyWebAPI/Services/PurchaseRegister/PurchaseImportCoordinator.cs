using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services.PurchaseRegister;

public sealed class PurchaseImportPlanStore
{
    private readonly ConcurrentDictionary<string, PurchaseImportPlan> _plans = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    public void Save(PurchaseImportPlan plan) => _plans[plan.Id] = plan;
    public PurchaseImportPlan Load(string id) => _plans.TryGetValue(id, out var p) && p.ExpiresAt > DateTimeOffset.UtcNow
        ? p : throw new InvalidOperationException("Purchase preview is missing or expired; create a fresh preview.");
    public async Task<IAsyncDisposable> LockAsync(string id, CancellationToken token)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidOperationException("Invalid Purchase preview ID.");
        var semaphore = _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, token)) throw new InvalidOperationException("This Purchase preview is already being applied.");
        return new Releaser(semaphore);
    }
    public void Remove(string id) => _plans.TryRemove(id, out _);
    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { semaphore.Release(); return ValueTask.CompletedTask; }
    }
}

public sealed record PurchasePreviewResult(PurchaseImportPlan Plan, PurchaseReconciliation Reconciliation);

public sealed class PurchaseImportCoordinator(AppDbContext db, PurchaseTallySource source,
    PurchaseImportPlanStore plans, TimeProvider clock)
{
    public async Task<PurchasePreviewResult> PreviewAsync(int companyId, DateTime from, DateTime to, string owner, CancellationToken token)
    {
        var snapshot = await source.ReadAsync(companyId, from, to, token);
        var baseline = await ReadRangeAsync(companyId, from, to, token);
        var baselineFingerprint = Fingerprint(baseline);
        var blockers = ValidateSource(snapshot).ToList();
        if (baseline.GroupBy(v => PurchaseImportPlan.Key(v.TallyGuid)).Any(g => g.Count() != 1)) blockers.Add("PurchaseRegister_tbl has duplicate company-scoped voucher GUIDs.");
        var sourceByGuid = snapshot.Active.GroupBy(v => PurchaseImportPlan.Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var sqlByGuid = baseline.GroupBy(v => PurchaseImportPlan.Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        foreach (var stored in baseline)
        {
            if (!sourceByGuid.TryGetValue(PurchaseImportPlan.Key(stored.TallyGuid), out var actual))
                blockers.Add($"Stored Purchase voucher {stored.TallyGuid} is absent or cancelled in Tally; existing row is retained.");
            else if (!Same(stored, actual)) blockers.Add($"Stored Purchase voucher {stored.TallyGuid} differs from Tally; updates are not allowed.");
        }
        var incoming = snapshot.Active.Select(v => v.TallyGuid).ToArray();
        var crossCompany = await db.PurchaseRegister.AsNoTracking().Where(v => v.CompanyId != companyId && incoming.Contains(v.TallyGuid))
            .Select(v => v.TallyGuid).ToListAsync(token);
        if (crossCompany.Count > 0) blockers.Add("Source Purchase GUIDs already exist under another SQL company.");
        snapshot = snapshot with { Errors = snapshot.Errors.Concat(blockers).Distinct().Order(StringComparer.Ordinal).ToArray() };
        var plan = CreatePlan(snapshot, baseline, baselineFingerprint, owner, clock.GetUtcNow());
        plans.Save(plan);
        return new(plan, Compare(snapshot, baseline));
    }

    public async Task<PurchaseApplyResult> ApplyAsync(string id, string digest, string owner, CancellationToken token)
    {
        await using var planLock = await plans.LockAsync(id, token);
        var plan = plans.Load(id);
        if (plan.Owner != owner || string.IsNullOrWhiteSpace(owner) || plan.Digest != digest || Digest(plan) != plan.Digest)
            throw new InvalidOperationException("Purchase preview ID/digest does not match this authenticated operator and plan.");
        if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Purchase preview expired; create a fresh preview.");
        if (!plan.SafeToApply) throw new InvalidOperationException("Purchase preview contains validation blockers.");

        var fresh = await source.ReadAsync(plan.Source.CompanyId, plan.Source.From, plan.Source.To, token);
        if (fresh.Fingerprint != plan.Source.Fingerprint || fresh.Errors.Length > 0)
            throw new InvalidOperationException("Tally Purchase source, cancellation state, classification or ledger mapping changed after preview.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Purchase preview expired before transaction.");
            var current = await ReadRangeAsync(plan.Source.CompanyId, plan.Source.From, plan.Source.To, token);
            if (Fingerprint(current) != plan.BaselineFingerprint) throw new InvalidOperationException("Purchase SQL baseline changed after preview.");
            var currentGuidSet = current.Select(v => PurchaseImportPlan.Key(v.TallyGuid)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var activeGuidSet = fresh.Active.Select(v => PurchaseImportPlan.Key(v.TallyGuid)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var insert = fresh.Active.Where(v => !currentGuidSet.Contains(PurchaseImportPlan.Key(v.TallyGuid))).ToArray();
            if (insert.Any(v => currentGuidSet.Contains(PurchaseImportPlan.Key(v.TallyGuid)))) throw new InvalidOperationException("A Purchase voucher GUID appeared in SQL after preview.");
            if (insert.Any(v => !activeGuidSet.Contains(PurchaseImportPlan.Key(v.TallyGuid)))) throw new InvalidOperationException("Planned Purchase voucher is no longer active in Tally.");
            if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("Purchase import requires a clean scoped SQL context.");

            var now = clock.GetUtcNow().UtcDateTime;
            var headers = insert.Select(v => new PurchaseRegisterEntity
            {
                CompanyId = v.CompanyId, TallyGuid = v.TallyGuid, VoucherNumber = v.VoucherNumber,
                VoucherDate = v.Date.Date, VoucherType = v.VoucherType, PartyLedgerName = v.PartyName,
                PartyLedgerGuid = v.PartyGuid, PurchaseLedgerNames = string.Join(", ", v.PurchaseLedgerNames),
                PurchaseLedgerGuids = string.Join(", ", v.PurchaseLedgerGuids), Amount = v.Amount,
                Debit = v.Debit, Credit = v.Credit, Narration = v.Narration, CreatedAt = now, UpdatedAt = now,
                Entries = v.Postings.OrderBy(p => p.Sequence).Select(p => new PurchaseRegisterEntryEntity
                {
                    LedgerGuid = p.LedgerGuid, LedgerName = p.LedgerName, SignedAmount = p.SignedAmount,
                    EntrySequence = p.Sequence, IsPurchaseLedger = p.IsPurchaseLedger
                }).ToList()
            }).ToList();
            db.PurchaseRegister.AddRange(headers);
            if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is not PurchaseRegisterEntity and not PurchaseRegisterEntryEntity))
                throw new InvalidOperationException("Purchase import attempted to write outside the independent Purchase Register tables.");
            await db.SaveChangesAsync(token);

            var verified = await ReadRangeAsync(plan.Source.CompanyId, plan.Source.From, plan.Source.To, token);
            var reconciliation = Compare(fresh, verified);
            if (!reconciliation.Matches || reconciliation.MissingGuids.Length != 0 || reconciliation.UnexpectedGuids.Length != 0 || reconciliation.ModifiedGuids.Length != 0)
                throw new InvalidOperationException("Post-insert Purchase reconciliation failed; transaction rolled back.");
            if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Purchase preview expired during transaction; transaction rolled back.");
            await tx.CommitAsync(token);
            plans.Remove(id);
            return new(id, headers.Count, headers.Sum(x => x.Entries.Count), reconciliation);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.ChangeTracker.Clear(); }
    }

    public async Task<PurchaseVoucher[]> ReadRangeAsync(int companyId, DateTime from, DateTime to, CancellationToken token)
    {
        var rows = await db.PurchaseRegister.AsNoTracking().Include(v => v.Entries)
            .Where(v => v.CompanyId == companyId && v.VoucherDate >= from && v.VoucherDate <= to)
            .OrderBy(v => v.VoucherDate).ThenBy(v => v.Id).ToListAsync(token);
        return rows.Select(ToVoucher).ToArray();
    }

    public static PurchaseReconciliation Compare(PurchaseSourceSnapshot source, PurchaseVoucher[] sql)
    {
        var expected = source.Active.GroupBy(v => PurchaseImportPlan.Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var stored = sql.GroupBy(v => PurchaseImportPlan.Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var missing = expected.Keys.Except(stored.Keys).Order(StringComparer.Ordinal).ToArray();
        var unexpected = stored.Keys.Except(expected.Keys).Order(StringComparer.Ordinal).ToArray();
        var modified = expected.Keys.Intersect(stored.Keys).Where(k => !Same(expected[k], stored[k])).Order(StringComparer.Ordinal).ToArray();
        decimal sourceClosing = 0, sqlClosing = 0; var months = new List<PurchaseMonthReconciliation>();
        for (var cursor = new DateTime(source.From.Year, source.From.Month, 1); cursor <= source.To; cursor = cursor.AddMonths(1))
        {
            var sv = source.Active.Where(v => v.Date.Year == cursor.Year && v.Date.Month == cursor.Month).ToArray();
            var qv = sql.Where(v => v.Date.Year == cursor.Year && v.Date.Month == cursor.Month).ToArray();
            var sd = sv.Sum(v => v.Debit); var sc = sv.Sum(v => v.Credit); var qd = qv.Sum(v => v.Debit); var qc = qv.Sum(v => v.Credit);
            sourceClosing += sd - sc; sqlClosing += qd - qc;
            months.Add(new(cursor.ToString("yyyyMM"), sv.Length, qv.Length, sv.Sum(v => v.Postings.Length), qv.Sum(v => v.Postings.Length),
                sd, qd, sc, qc, sourceClosing, sqlClosing, source.Cancelled.Count(v => v.Date.Year == cursor.Year && v.Date.Month == cursor.Month)));
        }
        var debit = source.Active.Sum(v => v.Debit); var credit = source.Active.Sum(v => v.Credit);
        return new(source.Errors.Length == 0 && missing.Length == 0 && unexpected.Length == 0 && modified.Length == 0 && source.Active.Length == sql.Length,
            source.Active.Length, sql.Length, source.Active.Sum(v => v.Postings.Length), sql.Sum(v => v.Postings.Length), missing, unexpected, modified,
            source.Cancelled.Length, debit, sql.Sum(v => v.Debit), credit, sql.Sum(v => v.Credit), months.ToArray());
    }

    private static IEnumerable<string> ValidateSource(PurchaseSourceSnapshot source)
    {
        foreach (var v in source.Active)
        {
            if (v.CompanyId != source.CompanyId || v.Date.Date < source.From || v.Date.Date > source.To || string.IsNullOrWhiteSpace(v.TallyGuid) || v.TallyGuid.Length > 100 ||
                string.IsNullOrWhiteSpace(v.VoucherNumber) || v.VoucherNumber.Length > 100 || string.IsNullOrWhiteSpace(v.PartyGuid) || v.PartyGuid.Length > 100 ||
                string.IsNullOrWhiteSpace(v.PartyName) || v.PartyName.Length > 250 || string.IsNullOrWhiteSpace(v.VoucherType) || v.VoucherType.Length > 250 ||
                v.Narration.Length > 2000 || string.Join(", ", v.PurchaseLedgerNames).Length > 1000 || string.Join(", ", v.PurchaseLedgerGuids).Length > 1000 ||
                v.Debit >= 100000000000000000m || v.Credit >= 100000000000000000m || v.Amount >= 100000000000000000m)
                yield return $"Invalid Purchase voucher header or amount: {v.TallyGuid}.";
            if (v.Postings.Length == 0 || decimal.Round(v.Postings.Sum(p => p.SignedAmount), 4) != 0 ||
                !v.Postings.Select(p => p.Sequence).SequenceEqual(Enumerable.Range(0, v.Postings.Length)) ||
                v.Postings.Any(p => string.IsNullOrWhiteSpace(p.LedgerGuid) || p.LedgerGuid.Length > 100 || string.IsNullOrWhiteSpace(p.LedgerName) || p.LedgerName.Length > 250 || decimal.Round(p.SignedAmount, 4) != p.SignedAmount || Math.Abs(p.SignedAmount) >= 1000000000000000m))
                yield return $"Invalid, unordered or unbalanced Purchase postings: {v.TallyGuid}.";
            var purchase = v.Postings.Where(p => p.IsPurchaseLedger).ToArray();
            if (purchase.Length == 0 || decimal.Round(purchase.Where(p => p.SignedAmount < 0).Sum(p => -p.SignedAmount), 2) != v.Debit ||
                decimal.Round(purchase.Where(p => p.SignedAmount > 0).Sum(p => p.SignedAmount), 2) != v.Credit)
                yield return $"Purchase Accounts debit/credit totals do not reconcile: {v.TallyGuid}.";
        }
        var allIds = source.Active.Select(v => v.TallyGuid).Concat(source.Cancelled.Select(v => v.TallyGuid)).ToArray();
        if (allIds.Any(string.IsNullOrWhiteSpace) || allIds.GroupBy(PurchaseImportPlan.Key).Any(g => g.Count() > 1))
            yield return "Tally Purchase source has missing or duplicate voucher GUIDs, including cancelled vouchers.";
    }

    private static PurchaseImportPlan CreatePlan(PurchaseSourceSnapshot source, PurchaseVoucher[] baseline, string baselineFingerprint, string owner, DateTimeOffset now)
    {
        var p = new PurchaseImportPlan(Guid.NewGuid().ToString("N"), owner, now, now.AddMinutes(20), source, baseline, baselineFingerprint, "");
        return p with { Digest = Digest(p) };
    }
    private static string Digest(PurchaseImportPlan p) => Hash(new { p.Id, p.Owner, p.CreatedAt, p.ExpiresAt, p.Source.Fingerprint, p.BaselineFingerprint, Errors = p.Source.Errors });
    private static string Fingerprint(PurchaseVoucher[] vouchers) => Hash(vouchers.OrderBy(v => PurchaseImportPlan.Key(v.TallyGuid), StringComparer.Ordinal).Select(VoucherHash).ToArray());
    private static string VoucherHash(PurchaseVoucher v) => Hash(new { v.CompanyId, Guid = PurchaseImportPlan.Key(v.TallyGuid), v.VoucherNumber, Date = v.Date.ToString("yyyyMMdd"), v.VoucherType, v.PartyName, PartyGuid = PurchaseImportPlan.Key(v.PartyGuid), v.PurchaseLedgerNames, v.PurchaseLedgerGuids, v.Amount, v.Debit, v.Credit, v.Narration, Postings = v.Postings.OrderBy(p => p.Sequence).ToArray() });
    private static bool Same(PurchaseVoucher a, PurchaseVoucher b) => VoucherHash(a) == VoucherHash(b);
    private static string Hash(object x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(x))));
    private static PurchaseVoucher ToVoucher(PurchaseRegisterEntity row) => new(row.CompanyId, row.TallyGuid, row.VoucherNumber, row.VoucherDate.Date,
        row.VoucherType, row.PartyLedgerName, row.PartyLedgerGuid, row.PurchaseLedgerNames.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries),
        row.PurchaseLedgerGuids.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries), row.Amount, row.Debit, row.Credit, row.Narration,
        row.Entries.OrderBy(x => x.EntrySequence).Select(x => new PurchasePosting(x.EntrySequence, x.LedgerGuid, x.LedgerName, x.SignedAmount, x.IsPurchaseLedger)).ToArray());
}
