using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class OutstandingSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly OutstandingService _outstandingService;

        public OutstandingSyncService(
            AppDbContext dbContext,
            OutstandingService outstandingService)
        {
            _dbContext = dbContext;
            _outstandingService = outstandingService;
        }

        public async Task<object> SyncAsync()
        {
            var companies = await _dbContext.Companies
                .AsNoTracking()
                .ToListAsync();

            var companiesProcessed = 0;
            var fetched = 0;
            var inserted = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var company in companies)
            {
                if (string.IsNullOrWhiteSpace(company.Name) ||
                    string.IsNullOrWhiteSpace(company.StartingFrom) ||
                    company.StartingFrom.Length != 8)
                {
                    continue;
                }

                if (!DateTime.TryParseExact(
                    company.StartingFrom,
                    "yyyyMMdd",
                    null,
                    System.Globalization.DateTimeStyles.None,
                    out var financialYearStart))
                {
                    continue;
                }

                var financialYearEnd = financialYearStart
                    .AddYears(1)
                    .AddDays(-1);

                var fromDate =
                    financialYearStart.ToString("yyyyMMdd");

                var toDate =
                    financialYearEnd.ToString("yyyyMMdd");

                var tallyRows =
                    await _outstandingService.GetOutstandingAsync(
                        company.Name,
                        fromDate,
                        toDate
                    );

                companiesProcessed++;
                fetched += tallyRows.Count;

                var existingRows = await _dbContext.Outstandings
                    .Where(x => x.CompanyId == company.Id)
                    .ToListAsync();

                foreach (var row in tallyRows)
                {
                    var existing = existingRows.FirstOrDefault(x =>
                        x.TallyGuid == row.TallyGuid &&
                        x.LedgerName == row.LedgerName &&
                        x.BillReference == row.BillReference &&
                        x.BillType == row.BillType
                    );

                    if (existing == null)
                    {
                        row.CompanyId = company.Id;
                        row.LastSyncedAt = DateTime.UtcNow;

                        _dbContext.Outstandings.Add(row);
                        existingRows.Add(row);

                        inserted++;
                        continue;
                    }

                    var changed =
                        existing.VoucherNumber != row.VoucherNumber ||
                        existing.VoucherType != row.VoucherType ||
                        existing.VoucherDate != row.VoucherDate ||
                        existing.BillDate != row.BillDate ||
                        existing.CreditPeriod != row.CreditPeriod ||
                        existing.Amount != row.Amount;

                    if (!changed)
                    {
                        skipped++;
                        continue;
                    }

                    existing.VoucherNumber = row.VoucherNumber;
                    existing.VoucherType = row.VoucherType;
                    existing.VoucherDate = row.VoucherDate;

                    existing.BillDate = row.BillDate;
                    existing.CreditPeriod = row.CreditPeriod;
                    existing.Amount = row.Amount;

                    existing.LastSyncedAt = DateTime.UtcNow;

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
    }
}