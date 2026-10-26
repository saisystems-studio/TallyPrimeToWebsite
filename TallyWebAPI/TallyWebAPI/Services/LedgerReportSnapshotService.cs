
using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services;

public sealed class LedgerReportSnapshotService(
    AppDbContext db,
    StockSummaryService tally,
    LedgerReportService reports)
{
    // =====================================================
    // GET LEDGER REPORT - OPTIONAL DATE FILTER
    // =====================================================

    public async Task<LedgerReportSnapshotDto?> GetAsync(
        int companyId,
        int ledgerId,
        string? fromDate,
        string? toDate,
        CancellationToken token)
    {
        var (company, ledger) =
            await GetScope(companyId, ledgerId, token);

        var requestedFrom = ParseOptionalDate(fromDate);
        var requestedTo = ParseOptionalDate(toDate);

        if (requestedFrom.HasValue &&
            requestedTo.HasValue &&
            requestedFrom.Value > requestedTo.Value)
        {
            throw new ArgumentException(
                "From Date cannot be later than To Date.");
        }

        // Fetch existing validated snapshots only.
        // No Tally sync or SQL modification.

        var snapshots = await db.LedgerReportSnapshots
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.CompanyTallyGuid == company.TallyGuid &&
                x.LedgerId == ledgerId &&
                x.LedgerTallyGuid == ledger.TallyGuid)
            .ToListAsync(token);

        // Pick one snapshot covering all explicitly
        // requested date boundaries.
        //
        // Both dates empty:
        // Use the widest available snapshot.
        //
        // One date supplied:
        // The snapshot must contain that date.
        //
        // Both supplied:
        // The snapshot must cover the complete range.

        var selectedSnapshot = snapshots
            .Where(x =>
                (!requestedFrom.HasValue ||
                 (x.FromDate <= requestedFrom.Value &&
                  x.ToDate >= requestedFrom.Value)) &&
                (!requestedTo.HasValue ||
                 (x.FromDate <= requestedTo.Value &&
                  x.ToDate >= requestedTo.Value)))
            .OrderByDescending(x =>
                x.ToDate.DayNumber - x.FromDate.DayNumber)
            .ThenByDescending(x => x.SyncedAt)
            .FirstOrDefault();

        if (selectedSnapshot is null)
            return null;

        // Load saved voucher postings.
        // No writes to database.


        var selectedSnapshotWithDetails =
            await db.LedgerReportSnapshots
                .AsNoTracking()
                .Include(x => x.Details)
                .SingleAsync(
                    x => x.Id == selectedSnapshot.Id,
                    token);

        var details = selectedSnapshotWithDetails.Details
            .OrderBy(x => x.PostingOrder)
            .ToList();


        // Dates missing = use snapshot boundaries.

        var effectiveFrom =
            requestedFrom ?? selectedSnapshot.FromDate;

        var effectiveTo =
            requestedTo ?? selectedSnapshot.ToDate;

        string from = effectiveFrom.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);

        string to = effectiveTo.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);

        // Transactions before selected From Date
        // contribute to opening balance.

        var previousRows = details
            .Where(x =>
                string.CompareOrdinal(
                    x.VoucherDate, from) < 0)
            .ToList();

        decimal previousDebit =
            previousRows.Sum(x => x.Debit);

        decimal previousCredit =
            previousRows.Sum(x => x.Credit);

        decimal openingBalance =
            LedgerBalanceMath.CalculateClosingBalance(
                selectedSnapshot.OpeningBalance,
                previousDebit,
                previousCredit);

        // Filter report transactions.

        var filteredRows = details
            .Where(x =>
                string.CompareOrdinal(
                    x.VoucherDate, from) >= 0 &&
                string.CompareOrdinal(
                    x.VoucherDate, to) <= 0)
            .OrderBy(x => x.VoucherDate)
            .ThenBy(x => x.PostingOrder)
            .ToList();

        // Calculate filtered totals.

        decimal totalDebit =
            filteredRows.Sum(x => x.Debit);

        decimal totalCredit =
            filteredRows.Sum(x => x.Credit);

        decimal closingBalance =
            LedgerBalanceMath.CalculateClosingBalance(
                openingBalance,
                totalDebit,
                totalCredit);

        int voucherCount = filteredRows
            .Select(x => (
                x.VoucherDate,
                x.VoucherType,
                x.VoucherNumber))
            .Distinct()
            .Count();

        // Build response.

        var result = new LedgerReportSnapshotDto
        {
            CompanyId = company.Id,
            CompanyName = company.Name,

            LedgerId = ledger.Id,
            LedgerName = ledger.Name,

            FromDate = from,
            ToDate = to,

            VoucherCount = voucherCount,

            OpeningBalance = openingBalance,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = closingBalance,

            HistoryComplete = true,

            HistoryNote =
                "Validated TallyPrime snapshot; " +
                "available coverage " +
                selectedSnapshot.FromDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture) +
                " to " +
                selectedSnapshot.ToDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture) +
                "; synced " +
                selectedSnapshot.SyncedAt.ToString(
                    "dd-MMM-yyyy HH:mm",
                    CultureInfo.InvariantCulture) +
                " UTC.",

            Status = "Saved",

            ReplacedExistingSnapshot = false,

            Transactions = filteredRows
                .Select(x => new LedgerTransactionDto
                {
                    Date = x.VoucherDate,
                    Particulars = x.Particulars,
                    VoucherType = x.VoucherType,
                    VoucherNumber = x.VoucherNumber,
                    Debit = x.Debit,
                    Credit = x.Credit
                })
                .ToList()
        };

        return result;
    }

    // =====================================================
    // OPTIONAL DATE PARSER
    // =====================================================

    private static DateOnly? ParseOptionalDate(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] formats =
        {
            "yyyyMMdd",
            "yyyy-MM-dd"
        };

        if (DateOnly.TryParseExact(
            value.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            "Invalid date. Use YYYYMMDD or YYYY-MM-DD.");
    }

    // =====================================================
    // EXISTING SYNC LOGIC - UNCHANGED
    // =====================================================

    public async Task<LedgerReportSnapshotDto> SyncAsync(
        int companyId,
        int ledgerId,
        string? fromDate,
        string? toDate,
        bool replaceExisting,
        CancellationToken token)
    {
        var (company, ledger) =
            await GetScope(companyId, ledgerId, token);

        if (string.IsNullOrWhiteSpace(ledger.TallyGuid))
        {
            throw new InvalidDataException(
                "The selected SQL ledger has no Tally GUID.");
        }

        await tally.VerifyCompanyIdentityAsync(
            company.Name,
            company.TallyGuid,
            token);

        await reports.VerifyLedgerIdentityAsync(
            company.Name,
            ledger.Name,
            ledger.TallyGuid);

        var (from, to) =
            await tally.ResolveRequestedPeriodAsync(
                fromDate,
                toDate,
                company.BooksFrom,
                company.Name,
                token);

        var report = await reports.GetLedgerReportAsync(
            company.Name,
            ledger.Name,
            from,
            to);

        if (report.Transactions.Count == 0 &&
            report.TotalDebit == 0 &&
            report.TotalCredit == 0)
        {
            throw new InvalidDataException(
                "Tally returned no selected-ledger postings. " +
                "The existing snapshot was left unchanged.");
        }

        var voucherCount = report.Transactions
            .Select(x => (
                x.Date,
                x.VoucherType,
                x.VoucherNumber))
            .Distinct()
            .Count();

        var debit =
            report.Transactions.Sum(x => x.Debit);

        var credit =
            report.Transactions.Sum(x => x.Credit);

        if (
            report.Transactions.Any(x =>
                string.IsNullOrWhiteSpace(x.Date) ||
                string.IsNullOrWhiteSpace(x.VoucherType) ||
                string.IsNullOrWhiteSpace(x.VoucherNumber)) ||

            voucherCount <= 0 ||

            report.Transactions.Count !=
            report.Transactions.Count(x =>
                !string.IsNullOrWhiteSpace(x.Date)) ||

            debit != report.TotalDebit ||

            credit != report.TotalCredit ||

            LedgerBalanceMath.CalculateClosingBalance(
                report.OpeningBalance,
                debit,
                credit) != report.ClosingBalance
        )
        {
            throw new InvalidDataException(
                "Tally ledger report failed voucher, total, " +
                "or balance reconciliation. " +
                "The existing snapshot was left unchanged.");
        }

        var f = DateOnly.ParseExact(
            from,
            "yyyyMMdd",
            CultureInfo.InvariantCulture);

        var t = DateOnly.ParseExact(
            to,
            "yyyyMMdd",
            CultureInfo.InvariantCulture);

        await using var tx =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                token);

        try
        {
            var existing =
                await db.LedgerReportSnapshots
                    .Include(x => x.Details)
                    .SingleOrDefaultAsync(x =>
                        x.CompanyId == companyId &&
                        x.CompanyTallyGuid == company.TallyGuid &&
                        x.LedgerId == ledgerId &&
                        x.LedgerTallyGuid == ledger.TallyGuid &&
                        x.FromDate == f &&
                        x.ToDate == t,
                        token);

            if (existing is not null && !replaceExisting)
            {
                throw new SnapshotAlreadyExistsException(
                    "A validated snapshot already exists " +
                    "for this exact scope. " +
                    "Set replaceExisting=true to explicitly replace it.");
            }

            var snapshot =
                existing ?? new LedgerReportSnapshotEntity
                {
                    CompanyId = companyId,
                    LedgerId = ledgerId,
                    FromDate = f,
                    ToDate = t
                };

            snapshot.CompanyTallyGuid = company.TallyGuid;
            snapshot.LedgerTallyGuid = ledger.TallyGuid!;

            snapshot.VoucherCount = voucherCount;
            snapshot.OpeningBalance = report.OpeningBalance;
            snapshot.TotalDebit = debit;
            snapshot.TotalCredit = credit;
            snapshot.ClosingBalance = report.ClosingBalance;
            snapshot.SyncedAt = DateTime.UtcNow;

            if (existing is null)
            {
                db.LedgerReportSnapshots.Add(snapshot);
            }
            else
            {
                db.LedgerReportSnapshotDetails.RemoveRange(
                    existing.Details);
            }

            var order = 0;

            foreach (var row in report.Transactions)
            {
                snapshot.Details.Add(
                    new LedgerReportSnapshotDetailEntity
                    {
                        PostingOrder = order++,
                        VoucherDate = row.Date,
                        Particulars = row.Particulars,
                        VoucherType = row.VoucherType,
                        VoucherNumber = row.VoucherNumber,
                        Debit = row.Debit,
                        Credit = row.Credit
                    });
            }

            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);

            var result = Map(
                snapshot,
                company.Name,
                ledger.Name);

            result.Status =
                existing is null ? "Created" : "Updated";

            result.ReplacedExistingSnapshot =
                existing is not null;

            return result;
        }
        catch
        {
            await tx.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }

    // =====================================================
    // COMPANY AND LEDGER VALIDATION - UNCHANGED
    // =====================================================

    private async Task<(
        CompanyEntity Company,
        LedgerEntity Ledger)> GetScope(
            int companyId,
            int ledgerId,
            CancellationToken token)
    {
        if (companyId <= 0 || ledgerId <= 0)
        {
            throw new ArgumentException(
                "Valid CompanyId and LedgerId are required.");
        }

        var company = await db.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == companyId,
                token)
            ?? throw new KeyNotFoundException(
                "The selected company was not found.");

        var ledger = await db.Ledgers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == ledgerId &&
                     x.CompanyId == companyId,
                token)
            ?? throw new KeyNotFoundException(
                "The selected ledger was not found " +
                "in the selected company.");

        return (company, ledger);
    }

    // =====================================================
    // ORIGINAL DTO MAPPING - UNCHANGED
    // =====================================================

    private static LedgerReportSnapshotDto Map(
        LedgerReportSnapshotEntity x,
        string companyName,
        string ledgerName) => new()
        {
            CompanyId = x.CompanyId,
            CompanyName = companyName,

            LedgerId = x.LedgerId,
            LedgerName = ledgerName,

            FromDate = x.FromDate.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture),

            ToDate = x.ToDate.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture),

            VoucherCount = x.VoucherCount,

            OpeningBalance = x.OpeningBalance,
            TotalDebit = x.TotalDebit,
            TotalCredit = x.TotalCredit,
            ClosingBalance = x.ClosingBalance,

            HistoryComplete = true,

            HistoryNote =
            $"Validated TallyPrime snapshot; " +
            $"synced {x.SyncedAt:dd-MMM-yyyy HH:mm} UTC.",

            Transactions = x.Details
            .OrderBy(d => d.PostingOrder)
            .Select(d => new LedgerTransactionDto
            {
                Date = d.VoucherDate,
                Particulars = d.Particulars,
                VoucherType = d.VoucherType,
                VoucherNumber = d.VoucherNumber,
                Debit = d.Debit,
                Credit = d.Credit
            })
            .ToList()
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

public sealed class SnapshotAlreadyExistsException(
    string message) : Exception(message);
