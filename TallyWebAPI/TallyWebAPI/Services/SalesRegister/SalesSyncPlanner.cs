using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TallyWebAPI.Services.SalesRegister;

public static class SalesSyncPlanner
{
    public static SalesSyncPlan Create(SalesSourceSnapshot source, SalesVoucher[] baseline, string owner, DateTimeOffset now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(owner) || lifetime <= TimeSpan.Zero) throw new ArgumentException("An operator and positive preview lifetime are required.");
        var errors = new List<string>(source.Errors);
        if (source.CompanyId <= 0 || source.BooksFrom > source.Through) errors.Add("Invalid company/history boundaries.");
        var allSource = source.Active.Select(v => v.TallyGuid).Concat(source.Cancelled.Select(v => v.TallyGuid)).ToArray();
        if (allSource.Any(string.IsNullOrWhiteSpace) || allSource.GroupBy(Key).Any(g => g.Count() != 1)) errors.Add("Missing/duplicate source voucher GUIDs, including cancellation identities.");
        if (baseline.Any(v => v.CompanyId != source.CompanyId) || source.Active.Any(v => v.CompanyId != source.CompanyId)) errors.Add("Company isolation violation.");
        if (baseline.GroupBy(v => Key(v.TallyGuid)).Any(g => g.Count() != 1)) errors.Add("Duplicate company-scoped SQL GUIDs.");
        var stored = baseline.GroupBy(v => Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var active = source.Active.GroupBy(v => Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var cancelled = source.Cancelled.Select(v => Key(v.TallyGuid)).ToHashSet();
        foreach (var voucher in source.Active)
        {
            if (voucher.Date.Date < source.BooksFrom.Date || voucher.Date.Date > source.Through.Date ||
                string.IsNullOrWhiteSpace(voucher.VoucherNumber) || voucher.VoucherNumber.Length > 100 ||
                voucher.TallyGuid.Length > 100 || string.IsNullOrWhiteSpace(voucher.PartyName) || voucher.PartyName.Length > 250 ||
                string.IsNullOrWhiteSpace(voucher.PartyGuid) || voucher.PartyGuid.Length > 100 || voucher.VoucherType.Length > 250 ||
                !source.SalesTypes.Contains(voucher.VoucherType, StringComparer.OrdinalIgnoreCase)) errors.Add($"Invalid header {voucher.TallyGuid}.");
            if (voucher.Postings.Length == 0 || voucher.Postings.Sum(p => p.SignedAmount) != 0 ||
                !voucher.Postings.Select(p => p.Sequence).SequenceEqual(Enumerable.Range(0, voucher.Postings.Length)) ||
                voucher.Postings.Any(p => string.IsNullOrWhiteSpace(p.LedgerGuid) || p.LedgerGuid.Length > 100 || string.IsNullOrWhiteSpace(p.LedgerName) || p.LedgerName.Length > 250 || decimal.Round(p.SignedAmount, 4) != p.SignedAmount || Math.Abs(p.SignedAmount) >= 1000000000000000m))
                errors.Add($"Invalid/unbalanced ordered postings {voucher.TallyGuid}.");
            var partyRows = voucher.Postings.Where(p => Key(p.LedgerGuid) == Key(voucher.PartyGuid)).ToArray();
            if (partyRows.Length == 0 || voucher.Amount != Math.Abs(partyRows.Sum(p => p.SignedAmount)) || decimal.Round(voucher.Amount, 2) != voucher.Amount || voucher.Amount >= 100000000000000000m)
                errors.Add($"Invalid party amount/precision {voucher.TallyGuid}.");
        }
        foreach (var row in baseline.Where(v => !active.ContainsKey(Key(v.TallyGuid))))
            errors.Add($"RETAIN_AND_BLOCK: {row.TallyGuid} is {(cancelled.Contains(Key(row.TallyGuid)) ? "cancelled" : "missing from complete source")}; SQL is retained pending review.");
        var actions = new List<SalesSyncAction>();
        foreach (var row in active.Values.OrderBy(v => v.Date).ThenBy(v => Key(v.TallyGuid), StringComparer.Ordinal))
        {
            stored.TryGetValue(Key(row.TallyGuid), out var before);
            if (before is null) actions.Add(new("Insert", row.TallyGuid, null, row));
            else if (!Same(before, row)) actions.Add(new("Update", row.TallyGuid, before, row));
        }
        var plan = new SalesSyncPlan(Guid.NewGuid().ToString("N"), owner, now, now.Add(lifetime), source,
            baseline, actions.ToArray(), errors.Distinct().ToArray(), SourceHash(source), "");
        return plan with { Digest = Digest(plan) };
    }

    public static string Key(string guid) => guid.Trim().ToUpperInvariant();
    public static bool Same(SalesVoucher left, SalesVoucher right) => VoucherHash(left) == VoucherHash(right);
    public static string VoucherHash(SalesVoucher v) => Hash(new {
        v.CompanyId, Guid = Key(v.TallyGuid), v.VoucherNumber, Date = v.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        v.VoucherType, v.PartyName, PartyGuid = Key(v.PartyGuid), Amount = Number(v.Amount),
        Postings = v.Postings.Select(p => new { p.Sequence, Guid = Key(p.LedgerGuid), p.LedgerName, Amount = Number(p.SignedAmount) })
    });
    public static string SourceHash(SalesSourceSnapshot source) => Hash(new {
        source.CompanyId, source.CompanyName, source.CompanyGuid, source.BooksFrom, source.Through, source.ContextFingerprint,
        Types = source.SalesTypes.Order(StringComparer.OrdinalIgnoreCase),
        Active = source.Active.OrderBy(v => Key(v.TallyGuid), StringComparer.Ordinal).Select(VoucherHash),
        Cancelled = source.Cancelled.OrderBy(v => Key(v.TallyGuid), StringComparer.Ordinal), source.Errors
    });
    public static string Digest(SalesSyncPlan plan) => Hash(new {
        plan.Id, plan.Owner, plan.CreatedAt, plan.ExpiresAt, plan.SourceFingerprint,
        Source = SourceHash(plan.Source), Baseline = plan.Baseline.OrderBy(v => Key(v.TallyGuid), StringComparer.Ordinal).Select(VoucherHash),
        Actions = plan.Actions.Select(a => new { a.Kind, Guid = Key(a.Guid), Before = a.Before is null ? null : VoucherHash(a.Before), After = VoucherHash(a.After) }), plan.Blockers
    });
    public static SalesSyncAction[] Pending(SalesSyncPlan plan, SalesVoucher[] current)
    {
        if (current.Any(v => v.CompanyId != plan.Source.CompanyId) || current.GroupBy(v => Key(v.TallyGuid)).Any(g => g.Count() != 1))
            throw new InvalidOperationException("Company/duplicate SQL state changed; create a fresh preview.");
        var actual = current.ToDictionary(v => Key(v.TallyGuid));
        var baseline = plan.Baseline.ToDictionary(v => Key(v.TallyGuid));
        var actions = plan.Actions.ToDictionary(a => Key(a.Guid));
        foreach (var row in actual.Values)
        {
            var key = Key(row.TallyGuid);
            if (actions.TryGetValue(key, out var action) && Same(row, action.After)) continue;
            if (!baseline.TryGetValue(key, out var before) || !Same(row, before))
                throw new InvalidOperationException($"SQL voucher {row.TallyGuid} changed outside the approved plan.");
        }
        if (baseline.Keys.Any(key => !actual.ContainsKey(key))) throw new InvalidOperationException("A baseline SQL voucher disappeared; create a fresh preview.");
        return plan.Actions.Where(a => !actual.TryGetValue(Key(a.Guid), out var row) || !Same(row, a.After)).ToArray();
    }
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string Number(decimal amount) => amount.ToString("G29", CultureInfo.InvariantCulture);
}
