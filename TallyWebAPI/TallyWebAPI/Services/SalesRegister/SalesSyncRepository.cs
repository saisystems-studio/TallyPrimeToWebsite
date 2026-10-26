using System.Data;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services.SalesRegister;

public sealed class SalesSyncRepository(AppDbContext db, TimeProvider clock) : ISalesSyncRepository
{
    public async Task<SalesVoucher[]> ReadAsync(int companyId, CancellationToken token = default)
    {
        if (companyId <= 0) throw new ArgumentException("A valid company is required.");
        var rows = await db.SalesRegister.AsNoTracking().Include(v => v.Entries).Where(v => v.CompanyId == companyId).ToListAsync(token);
        return rows.Select(ToVoucher).ToArray();
    }
    public async Task CommitAsync(SalesSyncPlan plan, SalesSyncAction[] batch, CancellationToken token = default)
    {
        if (!plan.SafeToApply || plan.ApprovedAt is null || SalesSyncPlanner.Digest(plan) != plan.Digest || batch.Length is < 1 or > 250)
            throw new InvalidOperationException("Invalid validated batch.");
        if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Synchronization requires a clean scoped SQL context.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Preview expired before transaction.");
            var current = await ReadAsync(plan.Source.CompanyId, token);
            var pending = SalesSyncPlanner.Pending(plan, current).Select(a => SalesSyncPlanner.Key(a.Guid)).ToHashSet();
            var approved = plan.Actions.ToDictionary(a => SalesSyncPlanner.Key(a.Guid));
            if (batch.Select(a => SalesSyncPlanner.Key(a.Guid)).Distinct().Count() != batch.Length || batch.Any(a =>
                !approved.TryGetValue(SalesSyncPlanner.Key(a.Guid), out var original) || a.Kind != original.Kind ||
                SalesSyncPlanner.VoucherHash(a.After) != SalesSyncPlanner.VoucherHash(original.After) ||
                a.After.CompanyId != plan.Source.CompanyId)) throw new InvalidOperationException("Batch differs from approved actions.");
            var work = batch.Where(a => pending.Contains(SalesSyncPlanner.Key(a.Guid))).Select(a => approved[SalesSyncPlanner.Key(a.Guid)]).ToArray();
            var currentKeys = current.ToDictionary(v => SalesSyncPlanner.Key(v.TallyGuid), v => v.TallyGuid);
            var updateKeys = work.Where(a => a.Kind == "Update").Select(a => currentKeys[SalesSyncPlanner.Key(a.Guid)]).ToArray();
            var updates = await db.SalesRegister.Include(v => v.Entries).Where(v => v.CompanyId == plan.Source.CompanyId && updateKeys.Contains(v.TallyGuid)).ToListAsync(token);
            var byGuid = updates.ToDictionary(v => SalesSyncPlanner.Key(v.TallyGuid));
            // Remove old postings first to avoid unique (header, sequence) collisions; both saves are inside this transaction.
            foreach (var header in updates) { db.SalesRegisterEntries.RemoveRange(header.Entries); header.Entries.Clear(); }
            GuardWrites();
            if (updates.Count > 0) await db.SaveChangesAsync(token);
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var action in work)
            {
                SalesRegisterEntity header;
                if (action.Kind == "Insert") { header = new() { CompanyId = plan.Source.CompanyId, CreatedAt = now }; db.SalesRegister.Add(header); }
                else if (action.Kind == "Update" && byGuid.TryGetValue(SalesSyncPlanner.Key(action.Guid), out var existing)) header = existing;
                else throw new InvalidOperationException("Approved update target disappeared.");
                var v = action.After;
                header.TallyGuid = v.TallyGuid; header.VoucherNumber = v.VoucherNumber; header.VoucherDate = v.Date.Date;
                header.VoucherType = v.VoucherType; header.PartyLedgerName = v.PartyName; header.PartyLedgerGuid = v.PartyGuid;
                header.Amount = v.Amount; header.UpdatedAt = now;
                header.Entries = v.Postings.Select(p => new SalesRegisterEntryEntity { LedgerGuid = p.LedgerGuid, LedgerName = p.LedgerName, SignedAmount = p.SignedAmount, EntrySequence = p.Sequence }).ToList();
            }
            GuardWrites();
            if (work.Length > 0) await db.SaveChangesAsync(token);
            var verified = await ReadAsync(plan.Source.CompanyId, token);
            var remaining = SalesSyncPlanner.Pending(plan, verified).Select(a => SalesSyncPlanner.Key(a.Guid)).ToHashSet();
            if (work.Any(a => remaining.Contains(SalesSyncPlanner.Key(a.Guid)))) throw new InvalidOperationException("Batch SQL reconciliation failed.");
            if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Preview expired during transaction; batch rolled back.");
            await transaction.CommitAsync(token);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        finally { db.ChangeTracker.Clear(); }
    }
    private void GuardWrites()
    {
        if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Entity is not SalesRegisterEntity && e.Entity is not SalesRegisterEntryEntity))
            throw new InvalidOperationException("Only the independent Sales Register tables may be written.");
    }
    public static SalesVoucher ToVoucher(SalesRegisterEntity row) => new(row.CompanyId, row.TallyGuid, row.VoucherNumber, row.VoucherDate.Date,
        row.VoucherType, row.PartyLedgerName, row.PartyLedgerGuid ?? "", row.Amount,
        row.Entries.OrderBy(p => p.EntrySequence).Select(p => new SalesPosting(p.EntrySequence, p.LedgerGuid, p.LedgerName, p.SignedAmount)).ToArray());
}
