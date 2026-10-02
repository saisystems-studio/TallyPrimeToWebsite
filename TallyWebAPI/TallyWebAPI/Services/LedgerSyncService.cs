using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class LedgerSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly TallyService _tallyService;

        public LedgerSyncService(
            AppDbContext dbContext,
            TallyService tallyService)
        {
            _dbContext = dbContext;
            _tallyService = tallyService;
        }

        public async Task<LedgerSyncResult> SyncAsync()
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
                var tallyLedgers =
                    await _tallyService.GetLedgerListAsync(
                        company.Name
                    );

                companiesProcessed++;
                fetched += tallyLedgers.Count;

                var existingLedgers = await _dbContext.Ledgers
                    .Where(x => x.CompanyId == company.Id)
                    .ToListAsync();

                var existingByGuid = existingLedgers
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.TallyGuid))
                    .GroupBy(
                        x => x.TallyGuid!,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToDictionary(
                        x => x.Key,
                        x => x.First(),
                        StringComparer.OrdinalIgnoreCase
                    );

                foreach (var tally in tallyLedgers)
                {
                    if (string.IsNullOrWhiteSpace(
                        tally.TallyGuid))
                    {
                        continue;
                    }

                    if (!existingByGuid.TryGetValue(
                        tally.TallyGuid,
                        out var ledger))
                    {
                        ledger = new LedgerEntity
                        {
                            CompanyId = company.Id,

                            TallyGuid = tally.TallyGuid,
                            MasterId = tally.MasterId,
                            AlterId = tally.AlterId,

                            Name = tally.Name,
                            Parent = tally.Parent,
                            Alias = tally.Alias,
                            MailingName = tally.MailingName,
                            Address = tally.Address,
                            State = tally.State,
                            Country = tally.Country,
                            Pincode = tally.Pincode,
                            Pan = tally.Pan,
                            Gstin = tally.Gstin,
                            RegistrationType =
                                tally.RegistrationType,
                            CreditPeriod =
                                tally.CreditPeriod,
                            BillByBill =
                                tally.BillByBill,
                            OpeningBalance =
                                tally.OpeningBalance,
                            ClosingBalance =
                                tally.ClosingBalance,

                            LastSyncedAt = DateTime.Now
                        };

                        _dbContext.Ledgers.Add(ledger);

                        existingByGuid[tally.TallyGuid] =
                            ledger;

                        inserted++;
                        continue;
                    }

                    bool changed =
                        ledger.MasterId != tally.MasterId ||
                        ledger.AlterId != tally.AlterId ||
                        ledger.Name != tally.Name ||
                        ledger.Parent != tally.Parent ||
                        ledger.Alias != tally.Alias ||
                        ledger.MailingName != tally.MailingName ||
                        ledger.Address != tally.Address ||
                        ledger.State != tally.State ||
                        ledger.Country != tally.Country ||
                        ledger.Pincode != tally.Pincode ||
                        ledger.Pan != tally.Pan ||
                        ledger.Gstin != tally.Gstin ||
                        ledger.RegistrationType !=
                            tally.RegistrationType ||
                        ledger.CreditPeriod !=
                            tally.CreditPeriod ||
                        ledger.BillByBill !=
                            tally.BillByBill ||
                        ledger.OpeningBalance !=
                            tally.OpeningBalance ||
                        ledger.ClosingBalance !=
                            tally.ClosingBalance;

                    if (!changed)
                    {
                        skipped++;
                        continue;
                    }

                    ledger.MasterId = tally.MasterId;
                    ledger.AlterId = tally.AlterId;

                    ledger.Name = tally.Name;
                    ledger.Parent = tally.Parent;
                    ledger.Alias = tally.Alias;
                    ledger.MailingName = tally.MailingName;
                    ledger.Address = tally.Address;
                    ledger.State = tally.State;
                    ledger.Country = tally.Country;
                    ledger.Pincode = tally.Pincode;
                    ledger.Pan = tally.Pan;
                    ledger.Gstin = tally.Gstin;
                    ledger.RegistrationType =
                        tally.RegistrationType;
                    ledger.CreditPeriod =
                        tally.CreditPeriod;
                    ledger.BillByBill =
                        tally.BillByBill;
                    ledger.OpeningBalance =
                        tally.OpeningBalance;
                    ledger.ClosingBalance =
                        tally.ClosingBalance;

                    ledger.LastSyncedAt = DateTime.Now;

                    updated++;
                }

                await _dbContext.SaveChangesAsync();
            }

            return new LedgerSyncResult
            {
                CompaniesProcessed = companiesProcessed,
                Fetched = fetched,
                Inserted = inserted,
                Updated = updated,
                Skipped = skipped
            };
        }
    }

    public class LedgerSyncResult
    {
        public int CompaniesProcessed { get; set; }

        public int Fetched { get; set; }

        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }
    }
}