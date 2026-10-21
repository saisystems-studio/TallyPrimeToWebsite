using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services;

public sealed class LedgerReportSnapshotService(AppDbContext db, StockSummaryService tally, LedgerReportService reports)
{
    public async Task<LedgerReportSnapshotDto?> GetAsync(int companyId, int ledgerId, string? fromDate, string? toDate, CancellationToken token)
    {
        var (company, ledger) = await GetScope(companyId, ledgerId, token);
        var (from, to) = await tally.ResolveRequestedPeriodAsync(fromDate, toDate, company.BooksFrom, company.Name, token);
        var f = DateOnly.ParseExact(from, "yyyyMMdd", CultureInfo.InvariantCulture);
        var t = DateOnly.ParseExact(to, "yyyyMMdd", CultureInfo.InvariantCulture);
        var snapshot = await db.LedgerReportSnapshots.AsNoTracking().Include(x => x.Details)
            .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.CompanyTallyGuid == company.TallyGuid && x.LedgerId == ledgerId && x.LedgerTallyGuid == ledger.TallyGuid && x.FromDate == f && x.ToDate == t, token);
        if (snapshot is null) return null;
        if (!string.Equals(snapshot.CompanyTallyGuid, company.TallyGuid, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(snapshot.LedgerTallyGuid, ledger.TallyGuid, StringComparison.OrdinalIgnoreCase)) return null;
        return Map(snapshot, company.Name, ledger.Name);
    }

    public async Task<LedgerReportSnapshotDto> SyncAsync(int companyId, int ledgerId, string? fromDate, string? toDate, bool replaceExisting, CancellationToken token)
    {
        var (company, ledger) = await GetScope(companyId, ledgerId, token);
        if (string.IsNullOrWhiteSpace(ledger.TallyGuid)) throw new InvalidDataException("The selected SQL ledger has no Tally GUID.");
        await tally.VerifyCompanyIdentityAsync(company.Name, company.TallyGuid, token);
        await reports.VerifyLedgerIdentityAsync(company.Name, ledger.Name, ledger.TallyGuid);
        var (from, to) = await tally.ResolveRequestedPeriodAsync(fromDate, toDate, company.BooksFrom, company.Name, token);
        var report = await reports.GetLedgerReportAsync(company.Name, ledger.Name, from, to);
        if (report.Transactions.Count == 0 && report.TotalDebit == 0 && report.TotalCredit == 0)
            throw new InvalidDataException("Tally returned no selected-ledger postings. The existing snapshot was left unchanged.");
        var voucherCount = report.Transactions.Select(x => (x.Date, x.VoucherType, x.VoucherNumber)).Distinct().Count();
        var debit = report.Transactions.Sum(x => x.Debit);
        var credit = report.Transactions.Sum(x => x.Credit);
        if (report.Transactions.Any(x => string.IsNullOrWhiteSpace(x.Date) || string.IsNullOrWhiteSpace(x.VoucherType) || string.IsNullOrWhiteSpace(x.VoucherNumber)) ||
            voucherCount <= 0 || report.Transactions.Count != report.Transactions.Count(x => !string.IsNullOrWhiteSpace(x.Date)) ||
            debit != report.TotalDebit || credit != report.TotalCredit ||
            LedgerBalanceMath.CalculateClosingBalance(report.OpeningBalance, debit, credit) != report.ClosingBalance)
            throw new InvalidDataException("Tally ledger report failed voucher, total, or balance reconciliation. The existing snapshot was left unchanged.");

        var f = DateOnly.ParseExact(from, "yyyyMMdd", CultureInfo.InvariantCulture);
        var t = DateOnly.ParseExact(to, "yyyyMMdd", CultureInfo.InvariantCulture);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            var existing = await db.LedgerReportSnapshots.Include(x => x.Details)
                .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.CompanyTallyGuid == company.TallyGuid && x.LedgerId == ledgerId && x.LedgerTallyGuid == ledger.TallyGuid && x.FromDate == f && x.ToDate == t, token);
            if (existing is not null && !replaceExisting)
                throw new SnapshotAlreadyExistsException("A validated snapshot already exists for this exact scope. Set replaceExisting=true to explicitly replace it.");
            var snapshot = existing ?? new LedgerReportSnapshotEntity { CompanyId = companyId, LedgerId = ledgerId, FromDate = f, ToDate = t };
            snapshot.CompanyTallyGuid = company.TallyGuid;
            snapshot.LedgerTallyGuid = ledger.TallyGuid!;
            snapshot.VoucherCount = voucherCount;
            snapshot.OpeningBalance = report.OpeningBalance;
            snapshot.TotalDebit = debit;
            snapshot.TotalCredit = credit;
            snapshot.ClosingBalance = report.ClosingBalance;
            snapshot.SyncedAt = DateTime.UtcNow;
            if (existing is null) db.LedgerReportSnapshots.Add(snapshot);
            else db.LedgerReportSnapshotDetails.RemoveRange(existing.Details);
            var order = 0;
            foreach (var row in report.Transactions)
                snapshot.Details.Add(new LedgerReportSnapshotDetailEntity
                {
                    PostingOrder = order++, VoucherDate = row.Date, Particulars = row.Particulars,
                    VoucherType = row.VoucherType, VoucherNumber = row.VoucherNumber,
                    Debit = row.Debit, Credit = row.Credit
                });
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            var result = Map(snapshot, company.Name, ledger.Name);
            result.Status = existing is null ? "Created" : "Updated";
            result.ReplacedExistingSnapshot = existing is not null;
            return result;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    private async Task<(CompanyEntity Company, LedgerEntity Ledger)> GetScope(int companyId, int ledgerId, CancellationToken token)
    {
        if (companyId <= 0 || ledgerId <= 0) throw new ArgumentException("Valid CompanyId and LedgerId are required.");
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId, token)
            ?? throw new KeyNotFoundException("The selected company was not found.");
        var ledger = await db.Ledgers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ledgerId && x.CompanyId == companyId, token)
            ?? throw new KeyNotFoundException("The selected ledger was not found in the selected company.");
        return (company, ledger);
    }

    private static LedgerReportSnapshotDto Map(LedgerReportSnapshotEntity x, string companyName, string ledgerName) => new()
    {
        CompanyId = x.CompanyId, CompanyName = companyName, LedgerId = x.LedgerId, LedgerName = ledgerName,
        FromDate = x.FromDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture), ToDate = x.ToDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        VoucherCount = x.VoucherCount, OpeningBalance = x.OpeningBalance, TotalDebit = x.TotalDebit, TotalCredit = x.TotalCredit,
        ClosingBalance = x.ClosingBalance, HistoryComplete = true, HistoryNote = $"Validated TallyPrime snapshot; synced {x.SyncedAt:dd-MMM-yyyy HH:mm} UTC.",
        Transactions = x.Details.OrderBy(d => d.PostingOrder).Select(d => new LedgerTransactionDto
        { Date = d.VoucherDate, Particulars = d.Particulars, VoucherType = d.VoucherType, VoucherNumber = d.VoucherNumber, Debit = d.Debit, Credit = d.Credit }).ToList()
    };
}

public sealed class LedgerReportSnapshotDto : LedgerReportDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public int LedgerId { get; set; }
    public int VoucherCount { get; set; }
    public string Status { get; set; } = "Saved";
    public bool ReplacedExistingSnapshot { get; set; }
}

public sealed class SnapshotAlreadyExistsException(string message) : Exception(message);
