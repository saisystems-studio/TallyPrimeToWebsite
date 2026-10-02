using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class VoucherSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly VoucherService _voucherService;

        public VoucherSyncService(
            AppDbContext dbContext,
            VoucherService voucherService)
        {
            _dbContext = dbContext;
            _voucherService = voucherService;
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
                if (string.IsNullOrWhiteSpace(company.StartingFrom))
                    continue;

                if (!DateTime.TryParseExact(
                        company.StartingFrom,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
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
    }
}