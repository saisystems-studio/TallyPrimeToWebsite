using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class StockItemSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly StockSummaryService _stockSummaryService;
        private readonly TallyService _tallyService;

        public StockItemSyncService(
            AppDbContext dbContext,
            StockSummaryService stockSummaryService,
            TallyService tallyService)
        {
            _dbContext = dbContext;
            _stockSummaryService = stockSummaryService;
            _tallyService = tallyService;
        }

        public async Task<StockItemSyncResult> SyncAsync()
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
                {
                    continue;
                }

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

                // Fetch THIS company's stock items from Tally
                var xml = await _stockSummaryService
                    .GetRawStockSummaryAsync(
                        fromDate,
                        toDate,
                        company.Name);

                if (string.IsNullOrWhiteSpace(xml))
                {
                    continue;
                }

                xml = CleanXml(xml);

                var document = XDocument.Parse(xml);

                var tallyItems = document
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "STOCKITEM",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                companiesProcessed++;

                // Load only THIS company's existing stock items
                var existingItems = await _dbContext.StockItems
                    .Where(x => x.CompanyId == company.Id)
                    .ToListAsync();

                var existingByGuid = existingItems
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.TallyGuid))
                    .GroupBy(
                        x => x.TallyGuid!,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        x => x.Key,
                        x => x.First(),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var tallyItem in tallyItems)
                {
                    var name =
                        tallyItem
                            .Attribute("NAME")
                            ?.Value
                            ?.Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = GetValue(
                            tallyItem,
                            "NAME");
                    }

                    var guid = GetValue(
                        tallyItem,
                        "GUID");

                    if (string.IsNullOrWhiteSpace(name) ||
                        string.IsNullOrWhiteSpace(guid))
                    {
                        continue;
                    }

                    fetched++;

                    var stockGroup = GetValue(
                        tallyItem,
                        "PARENT");

                    var unit = GetValue(
                        tallyItem,
                        "BASEUNITS");

                    var openingQuantity =
                        ParseQuantity(
                            GetValue(
                                tallyItem,
                                "OPENINGBALANCE"));

                    var closingQuantity =
                        ParseQuantity(
                            GetValue(
                                tallyItem,
                                "CLOSINGBALANCE"));

                    var masterId =
                        ParseLong(
                            GetValue(
                                tallyItem,
                                "MASTERID"));

                    var alterId =
                        ParseLong(
                            GetValue(
                                tallyItem,
                                "ALTERID"));

                    // NEW ITEM
                    if (!existingByGuid.TryGetValue(
                            guid,
                            out var existing))
                    {
                        var newItem = new StockItem
                        {
                            CompanyId = company.Id,

                            TallyGuid = guid,
                            MasterId = masterId,
                            AlterId = alterId,

                            Name = name,
                            StockGroup = stockGroup,
                            Unit = unit,

                            OpeningQuantity = openingQuantity,
                            ClosingQuantity = closingQuantity,

                            LastSyncedAt = DateTime.Now
                        };

                        _dbContext.StockItems.Add(newItem);

                        existingByGuid[guid] = newItem;

                        inserted++;
                        continue;
                    }

                    bool changed =
                        !string.Equals(
                            existing.Name,
                            name,
                            StringComparison.Ordinal) ||

                        !string.Equals(
                            existing.StockGroup ?? "",
                            stockGroup,
                            StringComparison.Ordinal) ||

                        !string.Equals(
                            existing.Unit ?? "",
                            unit,
                            StringComparison.Ordinal) ||

                        existing.OpeningQuantity !=
                            openingQuantity ||

                        existing.ClosingQuantity !=
                            closingQuantity ||

                        existing.MasterId !=
                            masterId ||

                        existing.AlterId !=
                            alterId;

                    if (!changed)
                    {
                        skipped++;
                        continue;
                    }

                    existing.Name = name;
                    existing.StockGroup = stockGroup;
                    existing.Unit = unit;

                    existing.OpeningQuantity =
                        openingQuantity;

                    existing.ClosingQuantity =
                        closingQuantity;

                    existing.MasterId = masterId;
                    existing.AlterId = alterId;

                    existing.LastSyncedAt =
                        DateTime.Now;

                    updated++;
                }

                await _dbContext.SaveChangesAsync();
            }

            return new StockItemSyncResult
            {
                CompanyName =
                    $"{companiesProcessed} companies",

                FinancialYearStart = "",
                FinancialYearEnd = "",

                Fetched = fetched,
                Inserted = inserted,
                Updated = updated,
                Skipped = skipped
            };
        }
        // =========================================================
        // XML VALUE HELPER
        // =========================================================

        private static string GetValue(
            XElement parent,
            string elementName)
        {
            return parent
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        elementName,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim() ?? "";
        }

        // =========================================================
        // LONG PARSER
        // =========================================================

        private static long? ParseLong(
            string value)
        {
            if (long.TryParse(
                    value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var result))
            {
                return result;
            }

            return null;
        }

        // =========================================================
        // QUANTITY PARSER
        // =========================================================

        private static decimal ParseQuantity(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            var text = value
                .Trim()
                .Replace(",", "");

            var negative =
                text.Contains("(-)") ||
                text.StartsWith("-");

            text = text.Replace(
                "(-)",
                "");

            var match = Regex.Match(
                text,
                @"[-+]?\d+(?:\.\d+)?");

            if (!match.Success)
                return 0;

            if (!decimal.TryParse(
                    match.Value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var quantity))
            {
                return 0;
            }

            quantity =
                Math.Abs(quantity);

            return negative
                ? -quantity
                : quantity;
        }

        // =========================================================
        // CLEAN TALLY XML
        // =========================================================

        private static string CleanXml(
            string xml)
        {
            xml = Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                "");

            xml = Regex.Replace(
                xml,
                @"(<\/?)UDF:",
                "$1");

            return xml;
        }
    }

    // =============================================================
    // SYNC RESULT
    // =============================================================

    public class StockItemSyncResult
    {
        public string CompanyName { get; set; } = "";

        public string FinancialYearStart { get; set; } = "";

        public string FinancialYearEnd { get; set; } = "";

        public int Fetched { get; set; }

        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }
    }
}