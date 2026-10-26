namespace TallyWebAPI.Services.SalesRegister;

public sealed record SalesMonthComparison(string Month, int SourceVouchers, int SqlVouchers, int SourcePostings, int SqlPostings,
    decimal SourceDebit, decimal SqlDebit, decimal SourceCredit, decimal SqlCredit, decimal SourceClosing, decimal SqlClosing, int CancelledExcluded);
public sealed record SalesReconciliation(bool Matches, int SourceVouchers, int SqlVouchers, int SourcePostings, int SqlPostings,
    string[] MissingGuids, string[] UnexpectedGuids, string[] ModifiedGuids, int CancelledExcluded, SalesMonthComparison[] Monthly)
{
    public static SalesReconciliation Compare(SalesSourceSnapshot source, SalesVoucher[] sql)
    {
        var expected = source.Active.GroupBy(v => SalesSyncPlanner.Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var stored = sql.GroupBy(v => SalesSyncPlanner.Key(v.TallyGuid)).ToDictionary(g => g.Key, g => g.First());
        var missing = expected.Keys.Except(stored.Keys).Order().ToArray();
        var unexpected = stored.Keys.Except(expected.Keys).Order().ToArray();
        var modified = expected.Keys.Intersect(stored.Keys).Where(k => !SalesSyncPlanner.Same(expected[k], stored[k])).Order().ToArray();
        var months = new List<SalesMonthComparison>();
        decimal sourceClosing = 0, sqlClosing = 0;
        for (var date = new DateTime(source.BooksFrom.Year, source.BooksFrom.Month, 1); date <= source.Through; date = date.AddMonths(1))
        {
            var s = source.Active.Where(v => v.Date.Year == date.Year && v.Date.Month == date.Month).ToArray();
            var d = sql.Where(v => v.Date.Year == date.Year && v.Date.Month == date.Month).ToArray();
            var sm = s.Select(Movement).ToArray(); var dm = d.Select(Movement).ToArray();
            sourceClosing += sm.Sum(); sqlClosing += dm.Sum();
            months.Add(new(date.ToString("yyyyMM"), s.Length, d.Length, s.Sum(v => v.Postings.Length), d.Sum(v => v.Postings.Length),
                -sm.Where(a => a < 0).Sum(), -dm.Where(a => a < 0).Sum(), sm.Where(a => a > 0).Sum(), dm.Where(a => a > 0).Sum(),
                sourceClosing, sqlClosing, source.Cancelled.Count(v => v.Date.Year == date.Year && v.Date.Month == date.Month)));
        }
        return new(source.Errors.Length == 0 && expected.Count == source.Active.Length && stored.Count == sql.Length && sql.All(v => v.CompanyId == source.CompanyId) &&
            missing.Length == 0 && unexpected.Length == 0 && modified.Length == 0,
            source.Active.Length, sql.Length, source.Active.Sum(v => v.Postings.Length), sql.Sum(v => v.Postings.Length), missing, unexpected, modified, source.Cancelled.Length, months.ToArray());
    }
    public static decimal Movement(SalesVoucher v) => -v.Postings.Where(p => SalesSyncPlanner.Key(p.LedgerGuid) == SalesSyncPlanner.Key(v.PartyGuid)).Sum(p => p.SignedAmount);
}
