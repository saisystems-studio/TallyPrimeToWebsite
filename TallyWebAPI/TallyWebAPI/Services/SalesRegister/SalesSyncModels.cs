namespace TallyWebAPI.Services.SalesRegister;

public sealed record SalesPosting(int Sequence, string LedgerGuid, string LedgerName, decimal SignedAmount);
public sealed record SalesVoucher(int CompanyId, string TallyGuid, string VoucherNumber, DateTime Date,
    string VoucherType, string PartyName, string PartyGuid, decimal Amount, SalesPosting[] Postings);
public sealed record CancelledSale(string TallyGuid, DateTime Date, string VoucherNumber, string VoucherType);
public sealed record SalesSourceSnapshot(int CompanyId, string CompanyName, string CompanyGuid, DateTime BooksFrom,
    DateTime Through, string[] SalesTypes, SalesVoucher[] Active, CancelledSale[] Cancelled, string[] Errors, string ContextFingerprint);
public sealed record SalesSyncAction(string Kind, string Guid, SalesVoucher? Before, SalesVoucher After);
public sealed record SalesSyncPlan(string Id, string Owner, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    SalesSourceSnapshot Source, SalesVoucher[] Baseline, SalesSyncAction[] Actions, string[] Blockers,
    string SourceFingerprint, string Digest, DateTimeOffset? ApprovedAt = null)
{
    public bool SafeToApply => Blockers.Length == 0;
}
public sealed class SalesSyncOptions
{
    public bool ApplyEnabled { get; set; }
    public int BatchSize { get; set; } = 100;
    public int PreviewMinutes { get; set; } = 30;
}
public interface ISalesSyncSource
{
    Task<SalesSourceSnapshot> ReadAsync(int companyId, CancellationToken token = default);
}
public interface ISalesRangeSyncSource
{
    Task<SalesSourceSnapshot> ReadRangeAsync(int companyId, DateTime from, DateTime to, CancellationToken token = default);
}
public interface ISalesSyncRepository
{
    Task<SalesVoucher[]> ReadAsync(int companyId, CancellationToken token = default);
    Task CommitAsync(SalesSyncPlan plan, SalesSyncAction[] batch, CancellationToken token = default);
}
public interface ISalesSyncPlanStore
{
    Task SaveAsync(SalesSyncPlan plan, CancellationToken token = default);
    Task<SalesSyncPlan> LoadAsync(string id, CancellationToken token = default);
    Task<IAsyncDisposable> LockAsync(string id, CancellationToken token = default);
}
