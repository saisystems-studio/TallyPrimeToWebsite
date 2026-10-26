namespace TallyWebAPI.Services.PurchaseRegister;

public sealed record PurchasePosting(int Sequence, string LedgerGuid, string LedgerName, decimal SignedAmount, bool IsPurchaseLedger);
public sealed record PurchaseVoucher(int CompanyId, string TallyGuid, string VoucherNumber, DateTime Date, string VoucherType,
    string PartyName, string PartyGuid, string[] PurchaseLedgerNames, string[] PurchaseLedgerGuids,
    decimal Amount, decimal Debit, decimal Credit, string Narration, PurchasePosting[] Postings);
public sealed record CancelledPurchase(string TallyGuid, DateTime Date, string VoucherNumber, string VoucherType);
public sealed record PurchaseSourceSnapshot(int CompanyId, string CompanyName, string CompanyGuid, DateTime From, DateTime To,
    string[] PurchaseTypes, PurchaseVoucher[] Active, CancelledPurchase[] Cancelled, string[] Errors, string Fingerprint);
public sealed record PurchaseMonthReconciliation(string Month, int SourceVouchers, int SqlVouchers, int SourcePostings, int SqlPostings,
    decimal SourceDebit, decimal SqlDebit, decimal SourceCredit, decimal SqlCredit,
    decimal SourceClosing, decimal SqlClosing, int CancelledExcluded);
public sealed record PurchaseReconciliation(bool Matches, int SourceVouchers, int SqlVouchers, int SourcePostings, int SqlPostings,
    string[] MissingGuids, string[] UnexpectedGuids, string[] ModifiedGuids, int CancelledExcluded,
    decimal SourceDebit, decimal SqlDebit, decimal SourceCredit, decimal SqlCredit, PurchaseMonthReconciliation[] Monthly);
public sealed record PurchaseImportPlan(string Id, string Owner, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    PurchaseSourceSnapshot Source, PurchaseVoucher[] Baseline, string BaselineFingerprint, string Digest)
{
    public bool SafeToApply => Source.Errors.Length == 0 && Source.Active.Select(v => Key(v.TallyGuid)).Distinct().Count() == Source.Active.Length;
    public static string Key(string guid) => guid.Trim().ToUpperInvariant();
}
public sealed record PurchaseApplyRequest(string PlanId, string Digest);
public sealed record PurchasePreviewRequest(int CompanyId, string FromDate, string ToDate);
public sealed record PurchaseApplyResult(string PlanId, int InsertedVouchers, int InsertedPostings, PurchaseReconciliation Reconciliation);
