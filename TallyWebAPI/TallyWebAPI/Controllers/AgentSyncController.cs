using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/agent-sync")]
    public class AgentSyncController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AgentSyncController(
            AppDbContext dbContext,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [HttpPost("companies")]
        public async Task<IActionResult> SyncCompanies(
            [FromBody] AgentCompanySyncRequest request)
        {
            // ---------------------------------------------
            // AGENT AUTHENTICATION
            // ---------------------------------------------

            var expectedKey =
                _configuration["AgentSync:ApiKey"];

            var receivedKey =
                Request.Headers["X-Agent-Key"]
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(expectedKey) ||
                string.IsNullOrWhiteSpace(receivedKey) ||
                !string.Equals(
                    expectedKey,
                    receivedKey,
                    StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    message = "Invalid sync agent key."
                });
            }

            if (request.Companies == null ||
                request.Companies.Count == 0)
            {
                return Ok(new
                {
                    fetched = 0,
                    inserted = 0,
                    updated = 0,
                    skipped = 0
                });
            }

            var validCompanies = request.Companies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.TallyGuid) &&
                    !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            var existingCompanies =
                await _dbContext.Companies.ToListAsync();

            var existingByGuid = existingCompanies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.TallyGuid))
                .ToDictionary(
                    x => x.TallyGuid,
                    StringComparer.OrdinalIgnoreCase);

            var inserted = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var incoming in validCompanies)
            {
                var guid = incoming.TallyGuid.Trim();
                var name = incoming.Name.Trim();

                if (!existingByGuid.TryGetValue(
                    guid,
                    out var company))
                {
                    company = new CompanyEntity
                    {
                        TallyGuid = guid,
                        Name = name,
                        FormalName = incoming.FormalName,
                        State = incoming.State,
                        Country = incoming.Country,
                        Pincode = incoming.Pincode,
                        Email = incoming.Email,
                        Phone = incoming.Phone,
                        Gstin = incoming.Gstin,
                        GstRegistrationType =
                            incoming.GstRegistrationType,
                        StartingFrom = incoming.StartingFrom,
                        BooksFrom = incoming.BooksFrom,
                        LastSyncedAt = DateTime.UtcNow
                    };

                    _dbContext.Companies.Add(company);

                    existingByGuid[guid] = company;

                    inserted++;
                    continue;
                }

                var changed =
                    company.Name != name ||
                    company.FormalName != incoming.FormalName ||
                    company.State != incoming.State ||
                    company.Country != incoming.Country ||
                    company.Pincode != incoming.Pincode ||
                    company.Email != incoming.Email ||
                    company.Phone != incoming.Phone ||
                    company.Gstin != incoming.Gstin ||
                    company.GstRegistrationType !=
                        incoming.GstRegistrationType ||
                    company.StartingFrom != incoming.StartingFrom ||
                    company.BooksFrom != incoming.BooksFrom;

                if (!changed)
                {
                    skipped++;
                    continue;
                }

                company.Name = name;
                company.FormalName = incoming.FormalName;
                company.State = incoming.State;
                company.Country = incoming.Country;
                company.Pincode = incoming.Pincode;
                company.Email = incoming.Email;
                company.Phone = incoming.Phone;
                company.Gstin = incoming.Gstin;
                company.GstRegistrationType =
                    incoming.GstRegistrationType;
                company.StartingFrom =
                    incoming.StartingFrom;
                company.BooksFrom =
                    incoming.BooksFrom;
                company.LastSyncedAt =
                    DateTime.UtcNow;

                updated++;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                fetched = validCompanies.Count,
                inserted,
                updated,
                skipped
            });
        }


        [HttpPost("ledgers")]
        public async Task<IActionResult> SyncLedgers(
    [FromBody] AgentLedgerSyncRequest request)
        {
            var expectedKey =
                _configuration["AgentSync:ApiKey"];

            var receivedKey =
                Request.Headers["X-Agent-Key"]
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(expectedKey) ||
                string.IsNullOrWhiteSpace(receivedKey) ||
                !string.Equals(
                    expectedKey,
                    receivedKey,
                    StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    message = "Invalid sync agent key."
                });
            }

            if (request?.Ledgers == null)
            {
                return BadRequest(new
                {
                    message = "Ledger data is required."
                });
            }

            int fetched = request.Ledgers.Count;
            int inserted = 0;
            int updated = 0;
            int skipped = 0;
            int rejected = 0;

            var companyGuids = request.Ledgers
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.CompanyTallyGuid))
                .Select(x => x.CompanyTallyGuid.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var companies =
                await _dbContext.Companies
                    .Where(x =>
                        companyGuids.Contains(x.TallyGuid))
                    .ToListAsync();

            var companyByGuid = companies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.TallyGuid))
                .ToDictionary(
                    x => x.TallyGuid,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var companyGroup in request.Ledgers
                .GroupBy(
                    x => x.CompanyTallyGuid?.Trim() ?? "",
                    StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                        companyGroup.Key) ||
                    !companyByGuid.TryGetValue(
                        companyGroup.Key,
                        out var company))
                {
                    rejected += companyGroup.Count();
                    continue;
                }

                var existingLedgers =
                    await _dbContext.Ledgers
                        .Where(x =>
                            x.CompanyId == company.Id)
                        .ToListAsync();

                var existingByGuid =
                    existingLedgers
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.TallyGuid))
                        .GroupBy(
                            x => x.TallyGuid!,
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            x => x.Key,
                            x => x.First(),
                            StringComparer.OrdinalIgnoreCase);

                foreach (var tally in companyGroup)
                {
                    if (string.IsNullOrWhiteSpace(
                            tally.TallyGuid) ||
                        string.IsNullOrWhiteSpace(
                            tally.Name))
                    {
                        rejected++;
                        continue;
                    }

                    var tallyGuid =
                        tally.TallyGuid.Trim();

                    if (!existingByGuid.TryGetValue(
                            tallyGuid,
                            out var ledger))
                    {
                        ledger = new LedgerEntity
                        {
                            CompanyId = company.Id,

                            TallyGuid = tallyGuid,
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

                            LastSyncedAt =
                                DateTime.UtcNow
                        };

                        _dbContext.Ledgers.Add(ledger);

                        existingByGuid[tallyGuid] =
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
                        ledger.MailingName !=
                            tally.MailingName ||
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
                    ledger.MailingName =
                        tally.MailingName;
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

                    ledger.LastSyncedAt =
                        DateTime.UtcNow;

                    updated++;
                }
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                fetched,
                inserted,
                updated,
                skipped,
                rejected
            });
        }


        [HttpPost("stock-items")]
        public async Task<IActionResult> SyncStockItems(
    [FromBody] AgentStockItemSyncRequest request)
        {
            // ---------------------------------------------
            // AGENT AUTHENTICATION
            // ---------------------------------------------
            var expectedKey =
                _configuration["AgentSync:ApiKey"];

            var receivedKey =
                Request.Headers["X-Agent-Key"]
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(expectedKey) ||
                string.IsNullOrWhiteSpace(receivedKey) ||
                !string.Equals(
                    expectedKey,
                    receivedKey,
                    StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    message = "Invalid sync agent key."
                });
            }

            if (request?.StockItems == null)
            {
                return BadRequest(new
                {
                    message = "Stock item data is required."
                });
            }

            int fetched = request.StockItems.Count;
            int inserted = 0;
            int updated = 0;
            int skipped = 0;
            int rejected = 0;

            // ---------------------------------------------
            // FIND COMPANIES USING TALLY GUID
            // ---------------------------------------------
            var companyGuids = request.StockItems
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.CompanyTallyGuid))
                .Select(x =>
                    x.CompanyTallyGuid.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            var companies =
                await _dbContext.Companies
                    .Where(x =>
                        companyGuids.Contains(
                            x.TallyGuid))
                    .ToListAsync();

            var companyByGuid = companies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.TallyGuid))
                .ToDictionary(
                    x => x.TallyGuid,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

            // ---------------------------------------------
            // PROCESS COMPANY-WISE
            // ---------------------------------------------
            foreach (var companyGroup in
                request.StockItems.GroupBy(
                    x =>
                        x.CompanyTallyGuid?.Trim() ?? "",
                    StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                        companyGroup.Key) ||
                    !companyByGuid.TryGetValue(
                        companyGroup.Key,
                        out var company))
                {
                    rejected += companyGroup.Count();
                    continue;
                }

                var existingItems =
                    await _dbContext.StockItems
                        .Where(x =>
                            x.CompanyId == company.Id)
                        .ToListAsync();

                var existingByGuid =
                    existingItems
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x.TallyGuid))
                        .GroupBy(
                            x => x.TallyGuid!,
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            x => x.Key,
                            x => x.First(),
                            StringComparer.OrdinalIgnoreCase);

                foreach (var tally in companyGroup)
                {
                    if (string.IsNullOrWhiteSpace(
                            tally.TallyGuid) ||
                        string.IsNullOrWhiteSpace(
                            tally.Name))
                    {
                        rejected++;
                        continue;
                    }

                    var tallyGuid =
                        tally.TallyGuid.Trim();

                    var name =
                        tally.Name.Trim();

                    // -------------------------------------
                    // INSERT
                    // -------------------------------------
                    if (!existingByGuid.TryGetValue(
                            tallyGuid,
                            out var stockItem))
                    {
                        stockItem = new StockItem
                        {
                            CompanyId = company.Id,

                            TallyGuid = tallyGuid,
                            MasterId = tally.MasterId,
                            AlterId = tally.AlterId,

                            Name = name,
                            StockGroup =
                                tally.StockGroup ?? "",
                            Unit =
                                tally.Unit ?? "",

                            OpeningQuantity =
                                tally.OpeningQuantity,

                            ClosingQuantity =
                                tally.ClosingQuantity,

                            LastSyncedAt =
                                DateTime.UtcNow
                        };

                        _dbContext.StockItems.Add(
                            stockItem);

                        existingByGuid[tallyGuid] =
                            stockItem;

                        inserted++;
                        continue;
                    }

                    // -------------------------------------
                    // CHECK CHANGES
                    // -------------------------------------
                    bool changed =
                        stockItem.MasterId !=
                            tally.MasterId ||

                        stockItem.AlterId !=
                            tally.AlterId ||

                        stockItem.Name !=
                            name ||

                        stockItem.StockGroup !=
                            (tally.StockGroup ?? "") ||

                        stockItem.Unit !=
                            (tally.Unit ?? "") ||

                        stockItem.OpeningQuantity !=
                            tally.OpeningQuantity ||

                        stockItem.ClosingQuantity !=
                            tally.ClosingQuantity;

                    if (!changed)
                    {
                        skipped++;
                        continue;
                    }

                    // -------------------------------------
                    // UPDATE
                    // -------------------------------------
                    stockItem.MasterId =
                        tally.MasterId;

                    stockItem.AlterId =
                        tally.AlterId;

                    stockItem.Name =
                        name;

                    stockItem.StockGroup =
                        tally.StockGroup ?? "";

                    stockItem.Unit =
                        tally.Unit ?? "";

                    stockItem.OpeningQuantity =
                        tally.OpeningQuantity;

                    stockItem.ClosingQuantity =
                        tally.ClosingQuantity;

                    stockItem.LastSyncedAt =
                        DateTime.UtcNow;

                    updated++;
                }
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                fetched,
                inserted,
                updated,
                skipped,
                rejected
            });
        }
    }
}