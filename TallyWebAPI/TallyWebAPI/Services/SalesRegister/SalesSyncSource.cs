using System.Globalization;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Services.SalesRegister;

/// <summary>Export-only adapter. Never starts synchronization or saves an EF entity.</summary>
public sealed class SalesSyncSource(AppDbContext db, HttpClient http) : ISalesSyncSource, ISalesRangeSyncSource
{
    public Task<SalesSourceSnapshot> ReadAsync(int companyId, CancellationToken token = default) => ReadCoreAsync(companyId, null, null, token);

    public Task<SalesSourceSnapshot> ReadRangeAsync(int companyId, DateTime from, DateTime to, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new ArgumentException("Date range must be ordered.");
        return ReadCoreAsync(companyId, from.Date, to.Date, token);
    }

    private async Task<SalesSourceSnapshot> ReadCoreAsync(int companyId, DateTime? requestedFrom, DateTime? requestedTo, CancellationToken token)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId, token)
            ?? throw new InvalidOperationException("Selected SQL company does not exist.");
        var companies = await Export("SafeSalesCompanies", "Company", "GUID,Name,BooksFrom,EndingAt", "", token);
        var matching = companies.Descendants("COMPANY").Where(c => Value(c, "GUID").Equals(company.TallyGuid, StringComparison.OrdinalIgnoreCase) &&
            ((string?)c.Attribute("NAME") ?? Value(c, "NAME")).Equals(company.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matching.Length != 1) throw new InvalidOperationException("Selected company name/GUID must match exactly one loaded Tally company.");
        var books = Date(Value(matching[0], "BOOKSFROM")); var through = Date(Value(matching[0], "ENDINGAT"));
        if (books > through) throw new InvalidOperationException("Company history boundaries are invalid.");
        var rangeFrom = requestedFrom ?? books; var rangeTo = requestedTo ?? through;
        if (rangeFrom < books || rangeTo > through || rangeFrom > rangeTo) throw new ArgumentException("Requested range must fall within Tally company history.");
        var scope = $"<SVCURRENTCOMPANY>{SecurityElement.Escape(company.Name)}</SVCURRENTCOMPANY>";
        var typeDoc = await Export("SafeSalesTypes", "Voucher Type", "Name,Parent", scope, token);
        var types = Types(typeDoc);
        var salesTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sales" };
        bool added;
        do { added = false; foreach (var row in types) if (salesTypes.Contains(row.Parent)) added |= salesTypes.Add(row.Name); } while (added);
        var knownTypes = types.Select(t => t.Name).Append("Sales").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        if (types.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1)) errors.Add("Duplicate voucher type classifications.");
        var ledgers = await db.Ledgers.AsNoTracking().Where(l => l.CompanyId == companyId).Select(l => new { l.Id, l.Name, l.TallyGuid }).ToListAsync(token);
        var mappingHash = SalesSyncPlanner.Hash(ledgers.OrderBy(l => l.Id));
        var ledgerMap = ledgers.Where(l => !string.IsNullOrWhiteSpace(l.TallyGuid)).GroupBy(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().TallyGuid!, StringComparer.OrdinalIgnoreCase);
        var bounds = Range(rangeFrom, rangeTo);
        var metadata = await Export("SafeSalesHistory", "Voucher", "GUID,Date,VoucherTypeName,VoucherNumber,IsCancelled", scope + bounds, token);
        var metadataSales = SelectSales(metadata, knownTypes, salesTypes, errors);
        var active = new List<SalesVoucher>(); var cancelled = new List<CancelledSale>(); var batchIdentities = new List<string>();
        for (var start = rangeFrom; start <= rangeTo;)
        {
            token.ThrowIfCancellationRequested();
            var end = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)); if (end > through) end = through;
            var doc = await Export("SafeSalesPostings", "Voucher", "GUID,Date,VoucherTypeName,VoucherNumber,PartyLedgerName,IsCancelled,AllLedgerEntries.LedgerName,AllLedgerEntries.Amount,LedgerEntries.LedgerName,LedgerEntries.Amount", scope + Range(start, end), token);
            foreach (var v in SelectSales(doc, knownTypes, salesTypes, errors))
            {
                batchIdentities.Add(Identity(v));
                var guid = Value(v, "GUID"); var number = Value(v, "VOUCHERNUMBER"); var type = Value(v, "VOUCHERTYPENAME"); var flag = Value(v, "ISCANCELLED");
                if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(number)) { errors.Add("Source voucher has no GUID/number."); continue; }
                if (!TryDate(Value(v, "DATE"), out var date) || date < start || date > end) { errors.Add($"Invalid/out-of-batch date {guid}."); continue; }
                if (flag.Equals("Yes", StringComparison.OrdinalIgnoreCase)) { cancelled.Add(new(guid, date, number, type)); continue; }
                if (!flag.Equals("No", StringComparison.OrdinalIgnoreCase)) { errors.Add($"Unknown cancellation status {guid}."); continue; }
                var party = Value(v, "PARTYLEDGERNAME");
                if (!ledgerMap.TryGetValue(party, out var partyGuid)) { errors.Add($"Party ledger mapping is missing/ambiguous: {guid} / {party}."); continue; }
                var entryNodes = v.Elements("ALLLEDGERENTRIES.LIST").ToArray(); if (entryNodes.Length == 0) entryNodes = v.Elements("LEDGERENTRIES.LIST").ToArray();
                var postings = new List<SalesPosting>();
                foreach (var (node, index) in entryNodes.Select((n, i) => (n, i)))
                {
                    var name = Value(node, "LEDGERNAME");
                    if (!ledgerMap.TryGetValue(name, out var ledgerGuid)) { errors.Add($"Ledger mapping is missing/ambiguous: {guid} / {name}."); continue; }
                    if (!decimal.TryParse(Value(node, "AMOUNT"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var amount))
                    { errors.Add($"Invalid signed posting amount {guid} / {name}."); continue; }
                    postings.Add(new(index, ledgerGuid, name, amount));
                }
                var amountTotal = Math.Abs(postings.Where(p => p.LedgerGuid.Equals(partyGuid, StringComparison.OrdinalIgnoreCase)).Sum(p => p.SignedAmount));
                active.Add(new(companyId, guid, number, date, type, party, partyGuid, amountTotal, postings.ToArray()));
            }
            start = end.AddDays(1);
        }
        var initialIdentities = metadataSales.Select(Identity).Order(StringComparer.Ordinal).ToArray();
        if (!initialIdentities.SequenceEqual(batchIdentities.Order(StringComparer.Ordinal))) errors.Add("History metadata and monthly posting identities do not reconcile.");
        var metadataAfter = await Export("SafeSalesHistory", "Voucher", "GUID,Date,VoucherTypeName,VoucherNumber,IsCancelled", scope + bounds, token);
        var afterIdentities = SelectSales(metadataAfter, knownTypes, salesTypes, errors).Select(Identity).Order(StringComparer.Ordinal).ToArray();
        if (!initialIdentities.SequenceEqual(afterIdentities)) errors.Add("Source history identities/status changed during export.");
        var finalCompany = await Export("SafeSalesCompanies", "Company", "GUID,Name,BooksFrom,EndingAt", "", token);
        var finalMatch = finalCompany.Descendants("COMPANY").Where(c => Value(c, "GUID").Equals(company.TallyGuid, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (finalMatch.Length != 1 || Value(finalMatch[0], "BOOKSFROM") != Value(matching[0], "BOOKSFROM") || Value(finalMatch[0], "ENDINGAT") != Value(matching[0], "ENDINGAT") || Value(finalMatch[0], "NAME") != Value(matching[0], "NAME")) errors.Add("Company identity/history boundaries changed during export.");
        if (SalesSyncPlanner.Hash(types.Select(t => new { t.Name, t.Parent })) != SalesSyncPlanner.Hash(Types(await Export("SafeSalesTypes", "Voucher Type", "Name,Parent", scope, token)).Select(t => new { t.Name, t.Parent }))) errors.Add("Voucher type classification changed during export.");
        var finalLedgers = await db.Ledgers.AsNoTracking().Where(l => l.CompanyId == companyId).Select(l => new { l.Id, l.Name, l.TallyGuid }).ToListAsync(token);
        if (mappingHash != SalesSyncPlanner.Hash(finalLedgers.OrderBy(l => l.Id))) errors.Add("SQL ledger mapping changed during export.");
        return new(companyId, company.Name, company.TallyGuid, rangeFrom, rangeTo, salesTypes.Order(StringComparer.OrdinalIgnoreCase).ToArray(), active.ToArray(), cancelled.ToArray(), errors.Distinct().Order(StringComparer.Ordinal).ToArray(), mappingHash);
    }
    private async Task<XDocument> Export(string id, string type, string fetch, string variables, CancellationToken token)
    {
        var request = $"""
        <ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>{id}</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>{variables}</STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME="{id}"><TYPE>{type}</TYPE><FETCH>{fetch}</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>
        """;
        var raw = await TallyXmlTransport.PostAsync(http, request, token);
        raw = Regex.Replace(raw, @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);", "");
        raw = Regex.Replace(raw, @"(<\/?)UDF:", "$1");
        var doc = XDocument.Parse(raw);
        if (doc.Descendants("LINEERROR").Any() || !doc.Descendants("STATUS").Any(s => s.Value.Trim() == "1") || !doc.Descendants("COLLECTION").Any())
            throw new InvalidOperationException("Tally export failed or returned no verified collection. No source absence may be inferred.");
        return doc;
    }
    private static XElement[] SelectSales(XDocument doc, HashSet<string> known, HashSet<string> sales, List<string> errors)
    {
        var rows = doc.Descendants("VOUCHER").ToArray();
        foreach (var row in rows.Where(v => !known.Contains(Value(v, "VOUCHERTYPENAME")))) errors.Add("Unknown voucher classification: " + Value(row, "VOUCHERTYPENAME"));
        return rows.Where(v => sales.Contains(Value(v, "VOUCHERTYPENAME"))).ToArray();
    }
    private static (string Name, string Parent)[] Types(XDocument doc) => doc.Descendants("VOUCHERTYPE")
        .Select(t => (Name: ((string?)t.Attribute("NAME") ?? Value(t, "NAME")).Trim(), Parent: Value(t, "PARENT")))
        .OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
    private static string Identity(XElement v) => SalesSyncPlanner.Hash(new { Guid = SalesSyncPlanner.Key(Value(v, "GUID")), Date = Value(v, "DATE"), Type = Value(v, "VOUCHERTYPENAME"), Number = Value(v, "VOUCHERNUMBER"), Cancelled = Value(v, "ISCANCELLED").ToUpperInvariant() });
    private static string Value(XElement node, string name) => node.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
    private static bool TryDate(string text, out DateTime date) => DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static DateTime Date(string text) => TryDate(text, out var date) ? date : throw new InvalidOperationException("Tally company book/history date is unavailable.");
    private static string Range(DateTime from, DateTime to) => $"<SVFROMDATE TYPE=\"Date\">{from:yyyyMMdd}</SVFROMDATE><SVTODATE TYPE=\"Date\">{to:yyyyMMdd}</SVTODATE>";
}
