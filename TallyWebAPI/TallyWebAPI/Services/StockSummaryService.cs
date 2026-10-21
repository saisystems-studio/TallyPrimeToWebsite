using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TallyWebAPI.DTOs;

namespace TallyWebAPI.Services;

public class StockSummaryService(HttpClient httpClient)
{
    public async Task VerifyCompanyIdentityAsync(string companyName, string expectedTallyGuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("The selected company has no name in the company master.");
        if (string.IsNullOrWhiteSpace(expectedTallyGuid))
            throw new InvalidDataException("The selected SQL company has no Tally GUID. No Stock Summary data was saved.");

        const string collectionName = "WebStockSummaryCompanyIdentity";
        var tdl = new XElement("TDL", new XElement("TDLMESSAGE",
            new XElement("COLLECTION", new XAttribute("NAME", collectionName),
                new XElement("TYPE", "Company"), new XElement("FETCH", "Name,GUID"))));
        var variables = new XElement("STATICVARIABLES", new XElement("SVEXPORTFORMAT", "$$SysName:XML"));
        var doc = ReadXml(await Send("Collection", collectionName, variables, tdl, cancellationToken));
        var matchingCompanies = doc.Descendants()
            .Where(x => x.Name.LocalName.Equals("COMPANY", StringComparison.OrdinalIgnoreCase) && x.Attribute("NAME") is not null)
            .Where(x => string.Equals(x.Attribute("NAME")?.Value.Trim(), companyName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingCompanies.Count != 1)
            throw new InvalidDataException($"TallyPrime did not return one unambiguous identity for '{companyName}'. No Stock Summary data was saved.");

        var actualGuid = matchingCompanies[0].Elements()
            .FirstOrDefault(x => x.Name.LocalName.Equals("GUID", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
        if (string.IsNullOrWhiteSpace(actualGuid) ||
            !string.Equals(actualGuid, expectedTallyGuid.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"TallyPrime company identity does not match the SQL company '{companyName}'. No Stock Summary data was saved.");
    }

    public static (string From, string To) ResolveFilters(string? fromDate, string? toDate,
        string? booksFrom, string? verifiedLatestAvailableDate, string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Please select a company.");
        var formats = new[] { "yyyyMMdd", "yyyy-MM-dd" };
        static DateOnly Parse(string? value, string[] acceptedFormats, string message)
        {
            if (!DateOnly.TryParseExact(value?.Trim(), acceptedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) throw new ArgumentException(message);
            return date;
        }

        var start = string.IsNullOrWhiteSpace(fromDate)
            ? Parse(booksFrom, formats, "The selected company has no valid Books From date.")
            : Parse(fromDate, formats, "A valid From Date is required.");
        var end = string.IsNullOrWhiteSpace(toDate)
            ? Parse(verifiedLatestAvailableDate, formats,
                "The latest accounting date cannot be determined reliably. Enter a To Date or verify the latest company entry date in TallyPrime. SQL voucher history has no completeness watermark.")
            : Parse(toDate, formats, "A valid To Date is required.");
        if (start > end) throw new ArgumentException("From Date cannot be after To Date.");
        return (start.ToString("yyyyMMdd"), end.ToString("yyyyMMdd"));
    }

    public static (string From, string To) ValidateFilters(string fromDate, string toDate, string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Please select a company.");
        var formats = new[] { "yyyyMMdd", "yyyy-MM-dd" };
        if (!DateOnly.TryParseExact(fromDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
            throw new ArgumentException("A valid From Date is required.");
        if (!DateOnly.TryParseExact(toDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            throw new ArgumentException("A valid To Date is required.");
        if (from > to) throw new ArgumentException("From Date cannot be after To Date.");
        return (from.ToString("yyyyMMdd"), to.ToString("yyyyMMdd"));
    }

    public static string ResolveFromDate(string? fromDate, string? booksFrom, string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Please select a company.");
        var value = string.IsNullOrWhiteSpace(fromDate) ? booksFrom : fromDate;
        var message = string.IsNullOrWhiteSpace(fromDate)
            ? "The selected company has no valid Books From date."
            : "A valid From Date is required.";
        if (!DateOnly.TryParseExact(value?.Trim(), new[] { "yyyyMMdd", "yyyy-MM-dd" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) throw new ArgumentException(message);
        return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    public async Task<string> GetRawStockSummaryAsync(string fromDate, string toDate, string companyName,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ValidateFilters(fromDate, toDate, companyName);
        // Native report valuation incorporates stock valuation methods, additional costs and journals.
        // Never replace its values with master balances or standard selling/cost rates.
        var variables = Variables(from, to, companyName);
        variables.Add(new XElement("DSPSHOWOPENING", "Yes"), new XElement("DSPSHOWINWARDS", "Yes"),
            new XElement("DSPSHOWOUTWARDS", "Yes"), new XElement("DSPSHOWCLOSING", "Yes"),
            new XElement("ISITEMWISE", "Yes"), new XElement("EXPLODEFLAG", "Yes"),
            new XElement("SVEXPLODEALLLEVELS", "Yes"));
        return await Send("Data", "Stock Summary", variables, null, cancellationToken);
    }

    public async Task<StockSummaryDto> GetStockSummaryAsync(string fromDate, string toDate, string companyName,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ValidateFilters(fromDate, toDate, companyName);
        var native = ReadXml(await GetRawStockSummaryAsync(from, to, companyName, cancellationToken));
        var stockItems = ReadXml(await GetMasters("StockItem", from, to, companyName, cancellationToken));
        var stockGroups = ReadXml(await GetMasters("StockGroup", from, to, companyName, cancellationToken));
        return Map(native, stockItems, stockGroups, from, to, companyName);
    }

    public async Task<string> GetLatestVoucherDateAsync(string companyName, string? booksFrom,
        CancellationToken cancellationToken = default)
    {
        return await GetLatestVoucherDateIfAvailableAsync(companyName, booksFrom, cancellationToken)
            ?? throw new InvalidDataException("TallyPrime returned no voucher date for the selected company.");
    }

    public async Task<string?> GetLatestVoucherDateIfAvailableAsync(string companyName, string? booksFrom,
        CancellationToken cancellationToken = default)
    {
        _ = booksFrom; // Retained for the existing public call contract.
        var dates = await GetCompanyAccountingDatesAsync(companyName, cancellationToken);
        return dates.To;
    }

    public async Task<(string From, string To)> ResolveRequestedPeriodAsync(string? fromDate, string? toDate,
        string? booksFrom, string companyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromDate) && string.IsNullOrWhiteSpace(toDate))
            return await GetCurrentReportingPeriodAsync(companyName, cancellationToken);

        var from = ResolveFromDate(fromDate, booksFrom, companyName);
        string? latest = null;
        if (!string.IsNullOrWhiteSpace(fromDate) && string.IsNullOrWhiteSpace(toDate))
        {
            latest = await GetLatestVoucherDateIfAvailableAsync(companyName, from, cancellationToken);
            if (latest is null)
                latest = (await GetCurrentReportingPeriodAsync(companyName, cancellationToken)).To;
        }
        return ResolveFilters(from, toDate, booksFrom, latest, companyName);
    }

    public async Task<(string From, string To)> GetCurrentReportingPeriodAsync(string companyName,
        CancellationToken cancellationToken = default)
    {
        var dates = await GetCompanyAccountingDatesAsync(companyName, cancellationToken);
        var formats = new[] { "yyyyMMdd", "yyyy-MM-dd" };
        if (!DateOnly.TryParseExact(dates.From, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateOnly.TryParseExact(dates.To, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            throw new InvalidDataException($"TallyPrime did not expose a valid default Stock Summary period for '{companyName}'. The period cannot be resolved safely.");
        if (from > to)
            throw new InvalidDataException($"TallyPrime's default Stock Summary period is reversed for '{companyName}'. Verify the company's accounting period.");
        return (from.ToString("yyyyMMdd", CultureInfo.InvariantCulture), to.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
    }

    private async Task<(string From, string To)> GetCompanyAccountingDatesAsync(string companyName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Please select a company.");
        const string collectionName = "WebCompanyAccountingDates";
        var tdl = new XElement("TDL", new XElement("TDLMESSAGE",
            new XElement("COLLECTION", new XAttribute("NAME", collectionName),
                new XElement("TYPE", "Company"),
                new XElement("FETCH", "Name,StartingFrom,EndingAt"),
                new XElement("COMPUTE", "StockSummaryFrom: $$FinYearBeg:$EndingAt:$StartingFrom"),
                new XElement("COMPUTE", "StockSummaryTo: $EndingAt"))));
        var variables = new XElement("STATICVARIABLES",
            new XElement("SVEXPORTFORMAT", "$$SysName:XML"),
            new XElement("SVCURRENTCOMPANY", companyName));
        var doc = ReadXml(await Send("Collection", collectionName, variables, tdl, cancellationToken));
        var companies = doc.Descendants().Where(x => x.Name.LocalName.Equals("COMPANY", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(x.Attribute("NAME")?.Value.Trim(), companyName.Trim(), StringComparison.OrdinalIgnoreCase) ||
             string.Equals(x.Elements().FirstOrDefault(y => y.Name.LocalName.Equals("NAME", StringComparison.OrdinalIgnoreCase))?.Value.Trim(), companyName.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        if (companies.Count != 1)
            throw new InvalidDataException($"TallyPrime did not return one unambiguous accounting period for '{companyName}'.");

        static string Field(XElement company, string field) => company.Elements()
            .FirstOrDefault(x => x.Name.LocalName.Equals(field, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
        return (Field(companies[0], "STOCKSUMMARYFROM"), Field(companies[0], "STOCKSUMMARYTO"));
    }

    private Task<string> GetMasters(string type, string from, string to, string company, CancellationToken token)
    {
        var name = $"WebStockSummary{type}";
        var fields = type == "StockItem" ? "Name,Parent,GUID,BaseUnits" : "Name,Parent,GUID";
        var tdl = new XElement("TDL", new XElement("TDLMESSAGE", new XElement("COLLECTION",
            new XAttribute("NAME", name), new XElement("TYPE", type), new XElement("FETCH", fields))));
        return Send("Collection", name, Variables(from, to, company), tdl, token);
    }

    private static XElement Variables(string from, string to, string company) => new("STATICVARIABLES",
        new XElement("SVEXPORTFORMAT", "$$SysName:XML"), new XElement("SVCURRENTCOMPANY", company),
        new XElement("SVFROMDATE", new XAttribute("TYPE", "Date"), from),
        new XElement("SVTODATE", new XAttribute("TYPE", "Date"), to));

    private Task<string> Send(string type, string id, XElement variables, XElement? tdl, CancellationToken token)
    {
        var envelope = new XElement("ENVELOPE", new XElement("HEADER", new XElement("VERSION", 1),
            new XElement("TALLYREQUEST", "Export"), new XElement("TYPE", type), new XElement("ID", id)),
            new XElement("BODY", new XElement("DESC", variables, tdl)));
        return TallyXmlTransport.PostAsync(httpClient, envelope.ToString(SaveOptions.DisableFormatting), token);
    }

    private static XDocument ReadXml(string xml)
    {
        xml = Regex.Replace(xml, @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);", "");
        XDocument doc;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(reader);
        }
        catch (XmlException ex) { throw new InvalidDataException("Tally returned invalid Stock Summary XML.", ex); }
        if (doc.Root?.Name.LocalName != "ENVELOPE" || doc.Descendants("LINEERROR").Any() ||
            doc.Descendants("STATUS").Any(x => x.Value == "0"))
            throw new InvalidDataException("Tally could not export Stock Summary. Check that the selected company is open in local TallyPrime.");
        return doc;
    }

    private static StockSummaryDto Map(XDocument native, XDocument stockItems, XDocument stockGroups,
        string from, string to, string company)
    {
        var masterRows = stockItems.Descendants("STOCKITEM").ToList();
        var groupRows = stockGroups.Descendants("STOCKGROUP").ToList();
        var masters = UniqueByName(masterRows, "stock item");
        var groupMasters = UniqueByName(groupRows, "stock group");
        var parents = groupRows.ToDictionary(x => NormalizeGroup(Name(x)),
            x => NormalizeGroup(Text(x, "PARENT")), StringComparer.OrdinalIgnoreCase);
        var names = native.Descendants("DSPACCNAME").ToList();
        var balances = native.Descendants("DSPSTKINFO").ToList();
        if (names.Count != balances.Count) throw Incomplete("Report names and balances do not match.");
        if (names.Count == 0 && masters.Count > 0)
            throw Incomplete("No item rows were exported. Verify the report configuration in TallyPrime.");

        var items = new List<StockSummaryItemDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < names.Count; index++)
        {
            var name = Text(names[index], "DSPDISPNAME");
            if (!masters.TryGetValue(name, out var master))
            {
                // Detailed exports may include group subtotals. Recompute them from unique leaves.
                if (parents.ContainsKey(name) || name.Equals("Grand Total", StringComparison.OrdinalIgnoreCase)) continue;
                throw Incomplete($"The report row '{name}' could not be matched to a stock item.");
            }
            if (!seen.Add(name)) throw Incomplete($"Duplicate stock item '{name}' in the report.");
            var opening = ReadColumn(balances[index], "DSPSTKOP", "DSPOPQTY", "DSPOPRATE", "DSPOPAMTA", 1m);
            var inward = ReadColumn(balances[index], "DSPSTKIN", "DSPINQTY", "DSPINRATE", "DSPDRAMTA", -1m);
            var outward = ReadColumn(balances[index], "DSPSTKOUT", "DSPOUTQTY", "DSPOUTRATE", "DSPNETTCRAMTA", 1m);
            var closing = ReadColumn(balances[index], "DSPSTKCL", "DSPCLQTY", "DSPCLRATE", "DSPCLAMTA", -1m);
            var unit = Text(master, "BASEUNITS");
            var parentGroup = NormalizeGroup(Text(master, "PARENT"));
            if (parentGroup != "Primary" && !groupMasters.ContainsKey(parentGroup))
                throw Incomplete($"Stock item '{name}' refers to missing stock group '{parentGroup}'.");
            var item = new StockSummaryItemDto
            {
                StockItemName = name, StockGroup = parentGroup, TallyGuid = Text(master, "GUID"), Unit = unit,
                OpeningQuantity = Quantity(opening.Quantity, unit), InwardQuantity = Quantity(inward.Quantity, unit),
                OutwardQuantity = Quantity(outward.Quantity, unit), ClosingQuantity = Quantity(closing.Quantity, unit),
                OpeningRate = opening.Rate,
                OpeningValue = opening.Value, InwardValue = inward.Value, OutwardValue = outward.Value, ClosingValue = closing.Value,
                InwardRate = inward.Rate, OutwardRate = outward.Rate, ClosingRate = closing.Rate,
                OpeningQuantityText = opening.Quantity, InwardQuantityText = inward.Quantity,
                OutwardQuantityText = outward.Quantity, ClosingQuantityText = closing.Quantity,
                OpeningRateText = opening.RateText,
                InwardRateText = inward.RateText, OutwardRateText = outward.RateText, ClosingRateText = closing.RateText
            };
            if (item.OpeningQuantity is decimal op && item.InwardQuantity is decimal iq &&
                item.OutwardQuantity is decimal oq && item.ClosingQuantity is decimal cq && op + iq - oq != cq)
                throw Incomplete($"Opening + inwards - outwards does not reconcile for '{name}'.");
            items.Add(item);
        }
        var groups = new Dictionary<string, StockSummaryGroupDto>(StringComparer.OrdinalIgnoreCase);
        StockSummaryGroupDto EnsureGroup(string name, HashSet<string> path)
        {
            if (!path.Add(name)) throw Incomplete("A circular stock group hierarchy was returned.");
            if (groups.TryGetValue(name, out var existing)) return existing;
            var groupGuid = "";
            if (name != "Primary")
            {
                if (!groupMasters.TryGetValue(name, out var master))
                    throw Incomplete($"Stock group '{name}' is missing from the Stock Group master export.");
                groupGuid = Text(master, "GUID");
            }
            var group = new StockSummaryGroupDto
            {
                GroupName = name,
                TallyGuid = groupGuid
            };
            groups.Add(name, group);
            if (name != "Primary" && parents.TryGetValue(name, out var parent) && parent != "Primary")
                EnsureGroup(parent, path).Children.Add(group);
            return group;
        }
        foreach (var item in items) EnsureGroup(item.StockGroup, new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Items.Add(item);
        var childNames = groups.Values.SelectMany(x => x.Children).Select(x => x.GroupName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roots = groups.Values.Where(x => !childNames.Contains(x.GroupName)).OrderBy(x => x.GroupName).ToList();
        foreach (var root in roots) AggregateGroup(root);
        var report = new StockSummaryDto { CompanyName = company, FromDate = from, ToDate = to, TotalItems = items.Count, Groups = roots };
        Aggregate(report.GrandTotal, items);
        return report;
    }

    private static List<StockSummaryItemDto> AggregateGroup(StockSummaryGroupDto group)
    {
        group.Children = group.Children.OrderBy(x => x.GroupName).ToList();
        group.Items = group.Items.OrderBy(x => x.StockItemName).ToList();
        var leaves = group.Items.Concat(group.Children.SelectMany(AggregateGroup)).ToList();
        Aggregate(group, leaves);
        return leaves;
    }

    private static void Aggregate(StockSummaryTotalsDto total, List<StockSummaryItemDto> leaves)
    {
        total.OpeningValue = SumValues(leaves.Select(x => x.OpeningValue));
        total.InwardValue = SumValues(leaves.Select(x => x.InwardValue));
        total.OutwardValue = SumValues(leaves.Select(x => x.OutwardValue));
        total.ClosingValue = SumValues(leaves.Select(x => x.ClosingValue));
        var units = leaves.Select(x => x.Unit).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (units.Count != 1 || string.IsNullOrWhiteSpace(units[0])) return;
        total.Unit = units[0];
        total.OpeningQuantity = SumQuantity(leaves.Select(x => x.OpeningQuantity));
        total.InwardQuantity = SumQuantity(leaves.Select(x => x.InwardQuantity));
        total.OutwardQuantity = SumQuantity(leaves.Select(x => x.OutwardQuantity));
        total.ClosingQuantity = SumQuantity(leaves.Select(x => x.ClosingQuantity));
        // The native export contains item rates only. A derived group average is not a Tally rate.
        total.OpeningRate = total.InwardRate = total.OutwardRate = total.ClosingRate = null;
    }

    private static decimal? SumValues(IEnumerable<decimal?> amounts)
    {
        var values = amounts.ToList();
        return values.Any(x => x is not null) ? values.Sum(x => x ?? 0m) : null;
    }

    private static decimal? SumQuantity(IEnumerable<decimal?> quantities)
    {
        var values = quantities.ToList();
        return values.Any(x => x is null) ? null : values.Sum(x => x!.Value);
    }
    private static string Name(XElement element) => element.Attribute("NAME")?.Value.Trim() ?? Text(element, "NAME");
    private static string Text(XElement element, string tag) => element.Element(tag)?.Value.Trim() ?? "";
    private static string NormalizeGroup(string value) => string.IsNullOrWhiteSpace(value) || value.Trim('*', ' ').Equals("Primary", StringComparison.OrdinalIgnoreCase) ? "Primary" : value;
    private static InvalidDataException Incomplete(string reason) => new($"Stock Summary is incomplete. {reason} No estimated balances have been substituted.");

    private static Dictionary<string, XElement> UniqueByName(IEnumerable<XElement> elements, string description)
    {
        var rows = elements.ToList();
        var duplicate = rows.GroupBy(Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw Incomplete($"The {description} master contains ambiguous duplicate name '{duplicate.Key}'.");
        return rows.ToDictionary(Name, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Column(string Quantity, decimal? Rate, string RateText, decimal? Value);
    private static Column ReadColumn(XElement info, string tag, string quantityTag, string rateTag, string amountTag, decimal amountSign)
    {
        var column = info.Element(tag) ?? throw Incomplete($"Tally did not export the {tag} column.");
        string Required(string field) => column.Element(field)?.Value.Trim()
            ?? throw Incomplete($"Tally did not export {field}.");
        var quantity = Required(quantityTag);
        var rate = Required(rateTag);
        var amountText = Required(amountTag);
        decimal? value;
        if (string.IsNullOrWhiteSpace(amountText))
        {
            // Tally can report a non-zero quantity while leaving the valuation amount blank
            // (observed for PMP Rice Mill closing stock). Keep that native blank as null.
            value = null;
        }
        else value = Number(amountText) * amountSign;
        return new Column(quantity, string.IsNullOrWhiteSpace(rate) ? null : Number(rate.Split('/')[0]), rate, value);
    }

    private static decimal Number(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var normalized = text.Replace(",", "").Replace("(-)", "-").Trim();
        if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var number)) return number;
        throw Incomplete($"Unrecognized numeric value '{text}'.");
    }

    private static decimal? Quantity(string text, string baseUnit)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Replace(",", "").Replace("(-)", "-").Trim();
        var match = Regex.Match(normalized, @"^([+-]?\d+(?:\.\d+)?)\s*(.*)$");
        if (!match.Success) throw Incomplete($"Unrecognized stock quantity '{text}'.");
        // Preserve compound/alternate-unit text. Never treat the first number as a base-unit total.
        var unit = match.Groups[2].Value.Trim();
        if (unit.Length > 0 && !unit.Equals(baseUnit, StringComparison.OrdinalIgnoreCase)) return null;
        return Number(match.Groups[1].Value);
    }
}

