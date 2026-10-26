using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class VoucherSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly VoucherService _voucherService;
        private readonly TallyService _tallyService;

        public VoucherSyncService(
            AppDbContext dbContext,
            VoucherService voucherService,
            TallyService tallyService)
        {
            _dbContext = dbContext;
            _voucherService = voucherService;
            _tallyService = tallyService;
        }

        public async Task<object> SyncAsync()
        {
            var companies = await _dbContext.Companies
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            int companiesProcessed = 0;
            int fetched = 0;
            int inserted = 0;
            int updated = 0;
            int skipped = 0;

            foreach (var company in companies)
            {
                var hasBooksFrom = DateTime.TryParseExact(
                    company.BooksFrom,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var financialYearStart);
                if (!hasBooksFrom && !DateTime.TryParseExact(
                        company.StartingFrom,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out financialYearStart))
                    continue;

                var financialYearEnd = DateTime.TryParseExact(
                    company.FinancialYearEnd,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var configuredEnd)
                        ? configuredEnd
                        : financialYearStart.AddYears(1).AddDays(-1);

                // Always request the complete available company history. The
                // voucher GUID upsert below makes this safe to repeat and keeps
                // historical ledger postings available for period openings.
                var syncStart = financialYearStart;
                var syncEnd = financialYearEnd < DateTime.Today
                    ? financialYearEnd
                    : DateTime.Today;
                if (syncStart > syncEnd)
                    continue;

                var fromDate =
                    syncStart.ToString("yyyyMMdd");

                var toDate =
                    syncEnd.ToString("yyyyMMdd");

                var tallyVouchers =
                    await _voucherService.GetVoucherListAsync(
                        fromDate,
                        toDate,
                        company.Name
                    );

                companiesProcessed++;
                fetched += tallyVouchers.Count;

                var existingVouchers =
                    await _dbContext.Vouchers
                        .Where(x => x.CompanyId == company.Id)
                        .ToListAsync();

                var existingByGuid = existingVouchers
                    .Where(x => !string.IsNullOrWhiteSpace(x.TallyGuid))
                    .GroupBy(
                        x => x.TallyGuid,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToDictionary(
                        x => x.Key,
                        x => x.First(),
                        StringComparer.OrdinalIgnoreCase
                    );

                foreach (var voucher in tallyVouchers)
                {
                    if (string.IsNullOrWhiteSpace(voucher.Guid))
                        continue;

                    decimal? amount = null;

                    if (decimal.TryParse(
                            voucher.Amount,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var parsedAmount))
                    {
                        amount = parsedAmount;
                    }

                    if (!existingByGuid.TryGetValue(
                            voucher.Guid,
                            out var existing))
                    {
                        var newVoucher = new VoucherEntity
                        {
                            CompanyId = company.Id,
                            TallyGuid = voucher.Guid,

                            VoucherNumber = voucher.VoucherNumber,
                            VoucherType = voucher.VoucherType,
                            VoucherDate = voucher.Date,

                            PartyName = voucher.PartyName,
                            PartyGstin = voucher.PartyGstin,

                            State = voucher.State,
                            PlaceOfSupply = voucher.PlaceOfSupply,

                            Amount = amount,
                            Narration = voucher.Narration,
                            LastSyncedAt = DateTime.Now
                        };

                        _dbContext.Vouchers.Add(newVoucher);

                        existingByGuid[voucher.Guid] = newVoucher;

                        inserted++;
                        continue;
                    }

                    bool changed =
                        existing.VoucherNumber != voucher.VoucherNumber ||
                        existing.VoucherType != voucher.VoucherType ||
                        existing.VoucherDate != voucher.Date ||
                        existing.PartyName != voucher.PartyName ||
                        existing.PartyGstin != voucher.PartyGstin ||
                        existing.State != voucher.State ||
                        existing.PlaceOfSupply != voucher.PlaceOfSupply ||
                        existing.Amount != amount ||
                        existing.Narration != voucher.Narration;

                    if (!changed)
                    {
                        skipped++;
                        continue;
                    }

                    existing.VoucherNumber = voucher.VoucherNumber;
                    existing.VoucherType = voucher.VoucherType;
                    existing.VoucherDate = voucher.Date;

                    existing.PartyName = voucher.PartyName;
                    existing.PartyGstin = voucher.PartyGstin;

                    existing.State = voucher.State;
                    existing.PlaceOfSupply = voucher.PlaceOfSupply;

                    existing.Amount = amount;
                    existing.Narration = voucher.Narration;
                    existing.LastSyncedAt = DateTime.Now;

                    updated++;
                }

                await _dbContext.SaveChangesAsync();
            }

            return new
            {
                companiesProcessed,
                fetched,
                inserted,
                updated,
                skipped
            };
        }

        // Explicit repair only; the normal sync/Windows Agent never invokes it.
        // Preview and apply use the same validations. One SaveChanges is atomic.
        public async Task<object> BackfillPostingsAsync(
            int companyId, string fromDate, string toDate, bool apply = false, bool salesOnly = false)
        {
            if (salesOnly)
                throw new InvalidOperationException(
                    "Sales backfills into shared voucher tables are disabled. Use the independent Sales Register preview/apply API.");

            if (!DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var from) ||
                !DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var to) || from > to)
                throw new ArgumentException("Supply an ordered yyyyMMdd date range.");
            var company = await _dbContext.Companies.AsNoTracking().SingleAsync(x => x.Id == companyId);
            var ledgers = await _dbContext.Ledgers.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync();
            var incoming = await _voucherService.GetVoucherListAsync(fromDate, toDate, company.Name);
            if (incoming.Count == 0)
                throw new InvalidOperationException("Tally returned no vouchers; no backfill can be verified.");
            if (incoming.Any(x => string.IsNullOrWhiteSpace(x.Guid) ||
                    !DateTime.TryParseExact(x.Date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                    date < from || date > to) ||
                incoming.GroupBy(x => x.Guid.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                throw new InvalidOperationException("Tally returned missing/duplicate voucher GUIDs or dates outside the requested range.");
            var existing = await _dbContext.Vouchers.Include(x => x.LedgerEntries)
                .Where(x => x.CompanyId == companyId).ToListAsync();
            if (existing.GroupBy(x => x.TallyGuid.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                throw new InvalidOperationException("Duplicate stored voucher GUIDs require review before backfill.");
            var byGuid = existing.ToDictionary(x => x.TallyGuid.Trim(), StringComparer.OrdinalIgnoreCase);
            int headersAdded = 0, postingsAdded = 0;
            foreach (var source in incoming)
            {
                var guid = source.Guid.Trim();
                if (!byGuid.TryGetValue(guid, out var voucher))
                {
                    voucher = new VoucherEntity {
                        CompanyId = companyId, TallyGuid = guid, VoucherDate = source.Date,
                        VoucherNumber = source.VoucherNumber, VoucherType = source.VoucherType,
                        PartyName = source.PartyName, PartyGstin = source.PartyGstin,
                        State = source.State, PlaceOfSupply = source.PlaceOfSupply, Narration = source.Narration,
                        Amount = decimal.TryParse(source.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : null,
                        LastSyncedAt = DateTime.UtcNow
                    };
                    _dbContext.Vouchers.Add(voucher);
                    byGuid.Add(guid, voucher);
                    headersAdded++;
                }
                else if (voucher.VoucherDate != source.Date || voucher.VoucherNumber != source.VoucherNumber ||
                         voucher.VoucherType != source.VoucherType)
                    throw new InvalidOperationException("Existing voucher identity differs from Tally; backfill stopped.");
                postingsAdded += VoucherPostingBackfill.Apply(voucher, source.Postings, ledgers);
            }
            if (apply) await _dbContext.SaveChangesAsync();
            else _dbContext.ChangeTracker.Clear();
            return new { companyId, companyName = company.Name, fromDate, toDate, preview = !apply,
                vouchersFetched = incoming.Count, headersAdded, postingsAdded };
        }


    }
}
