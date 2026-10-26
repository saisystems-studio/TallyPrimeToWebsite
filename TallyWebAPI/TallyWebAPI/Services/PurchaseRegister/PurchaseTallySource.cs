using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Services;

namespace TallyWebAPI.Services.PurchaseRegister;

public sealed class PurchaseTallySource(AppDbContext db, HttpClient http)
{
    public async Task<PurchaseSourceSnapshot> ReadAsync(int companyId, DateTime from, DateTime to, CancellationToken token)
    {
        from = from.Date; to = to.Date;
        if (companyId <= 0 || from > to || (to - from).TotalDays > 400)
            throw new ArgumentException("Supply a valid company and ordered date range of at most 401 days.");
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId, token)
            ?? throw new InvalidOperationException("Selected SQL company does not exist.");
        var companyDoc = await Export("PurchaseCompanies", "Company", "GUID,Name,BooksFrom,EndingAt", "", token);
        var matching = companyDoc.Descendants("COMPANY").Where(x => Value(x, "GUID").Equals(company.TallyGuid, StringComparison.OrdinalIgnoreCase) &&
            ((string?)x.Attribute("NAME") ?? Value(x, "NAME")).Equals(company.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matching.Length != 1) throw new InvalidOperationException("SQL company name/GUID must match exactly one active Tally company.");
        var booksFrom = ParseDate(Value(matching[0], "BOOKSFROM"));
        var booksTo = ParseDate(Value(matching[0], "ENDINGAT"));
        if (from < booksFrom || to > booksTo) throw new ArgumentException("Requested dates must fall within the selected Tally company's books.");

        var scope = $"<SVCURRENTCOMPANY>{SecurityElement.Escape(company.Name)}</SVCURRENTCOMPANY>";
        var errors = new List<string>();
        var typeDoc = await Export("PurchaseVoucherTypes", "Voucher Type", "Name,Parent", scope, token);
        var types = typeDoc.Descendants("VOUCHERTYPE").Select(x => (Name: (string?)x.Attribute("NAME") ?? Value(x, "NAME"), Parent: Value(x, "PARENT")))
            .Select(x => (x.Name.Trim(), x.Parent.Trim())).Where(x => x.Item1.Length > 0).ToArray();
        if (types.GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) errors.Add("Tally has duplicate voucher type names; Purchase classification is ambiguous.");
        var purchaseTypes = Descendants("Purchase", types);
        if (purchaseTypes.Count == 0) errors.Add("Tally did not provide a Purchase voucher type classification.");
        var knownTypes = types.Select(x => x.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var groupDoc = await Export("PurchaseLedgerGroups", "Group", "Name,Parent", scope, token);
        var purchaseGroups = Descendants("Purchase Accounts", groupDoc.Descendants("GROUP")
            .Select(x => ((string?)x.Attribute("NAME") ?? Value(x, "NAME"), Value(x, "PARENT"))).ToArray());
        if (purchaseGroups.Count == 0) errors.Add("Tally Purchase Accounts group hierarchy was not found.");
        var tallyLedgerDoc = await Export("PurchaseLedgers", "Ledger", "Name,Parent,GUID", scope, token);
        var tallyLedgers = tallyLedgerDoc.Descendants("LEDGER").Select(x => new TallyLedger(
            ((string?)x.Attribute("NAME") ?? Value(x, "NAME")).Trim(), Value(x, "PARENT"), Value(x, "GUID"))).ToArray();
        var sqlLedgers = await db.Ledgers.AsNoTracking().Where(x => x.CompanyId == companyId)
            .Select(x => new { x.Id, x.Name, x.TallyGuid }).ToListAsync(token);
        var ledgerFingerprint = Hash(sqlLedgers.OrderBy(x => x.Id).ToArray());
        var sqlByName = sqlLedgers.Where(x => !string.IsNullOrWhiteSpace(x.TallyGuid))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var tallyByName = tallyLedgers.Where(x => x.Name.Length > 0)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var purchaseLedgerGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ledger in tallyLedgers.Where(x => purchaseGroups.Contains(x.Parent)))
        {
            if (string.IsNullOrWhiteSpace(ledger.Guid) || !sqlByName.TryGetValue(ledger.Name, out var sqlMatches) || sqlMatches.Length != 1 ||
                !sqlMatches[0].TallyGuid!.Equals(ledger.Guid, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Purchase ledger mapping is missing, ambiguous or differs from Tally: {ledger.Name}.");
            else purchaseLedgerGuids.Add(ledger.Guid);
        }

        var range = Range(from, to);
        var metadata = await Export("PurchaseVoucherMetadata", "Voucher", "GUID,Date,VoucherTypeName,VoucherNumber,IsCancelled", scope + range, token);
        var metadataPurchases = SelectPurchase(metadata, knownTypes, purchaseTypes, errors);
        var active = new List<PurchaseVoucher>(); var cancelled = new List<CancelledPurchase>(); var monthlyIdentities = new List<string>();
        for (var cursor = from; cursor <= to;)
        {
            token.ThrowIfCancellationRequested();
            var monthEnd = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            if (monthEnd > to) monthEnd = to;
            var document = await Export("PurchaseVoucherPostings", "Voucher",
                "GUID,Date,VoucherTypeName,VoucherNumber,PartyLedgerName,IsCancelled,Narration,AllLedgerEntries.LedgerName,AllLedgerEntries.Amount,LedgerEntries.LedgerName,LedgerEntries.Amount",
                scope + Range(cursor, monthEnd), token);
            foreach (var voucher in SelectPurchase(document, knownTypes, purchaseTypes, errors))
            {
                monthlyIdentities.Add(Identity(voucher));
                var guid = Value(voucher, "GUID"); var number = Value(voucher, "VOUCHERNUMBER");
                var type = Value(voucher, "VOUCHERTYPENAME"); var status = Value(voucher, "ISCANCELLED");
                if (string.IsNullOrWhiteSpace(guid) || guid.Length > 100 || string.IsNullOrWhiteSpace(number) || number.Length > 100)
                { errors.Add("Purchase voucher is missing a valid GUID or number."); continue; }
                if (!TryDate(Value(voucher, "DATE"), out var date) || date < cursor || date > monthEnd)
                { errors.Add($"Purchase voucher date is invalid or outside its XML batch: {guid}."); continue; }
                if (status.Equals("Yes", StringComparison.OrdinalIgnoreCase)) { cancelled.Add(new(guid, date, number, type)); continue; }
                if (!status.Equals("No", StringComparison.OrdinalIgnoreCase)) { errors.Add($"Purchase voucher has unknown ISCANCELLED status: {guid}."); continue; }

                var partyName = Value(voucher, "PARTYLEDGERNAME");
                if (!sqlByName.TryGetValue(partyName, out var partyRows) || partyRows.Length != 1 ||
                    !tallyByName.TryGetValue(partyName, out var tallyParty) || tallyParty.Length != 1 ||
                    string.IsNullOrWhiteSpace(tallyParty[0].Guid) || !partyRows[0].TallyGuid!.Equals(tallyParty[0].Guid, StringComparison.OrdinalIgnoreCase))
                { errors.Add($"Party ledger mapping is missing, ambiguous or differs from Tally: {guid} / {partyName}."); continue; }

                var entryNodes = voucher.Elements("ALLLEDGERENTRIES.LIST").ToArray();
                if (entryNodes.Length == 0) entryNodes = voucher.Elements("LEDGERENTRIES.LIST").ToArray();
                var postings = new List<PurchasePosting>(); var invalidPosting = false;
                foreach (var (node, sequence) in entryNodes.Select((node, index) => (node, index)))
                {
                    var ledgerName = Value(node, "LEDGERNAME");
                    if (!sqlByName.TryGetValue(ledgerName, out var mapped) || mapped.Length != 1 ||
                        !tallyByName.TryGetValue(ledgerName, out var tallyRows) || tallyRows.Length != 1 ||
                        string.IsNullOrWhiteSpace(tallyRows[0].Guid) || !mapped[0].TallyGuid!.Equals(tallyRows[0].Guid, StringComparison.OrdinalIgnoreCase))
                    { errors.Add($"Posting ledger mapping is missing, ambiguous or differs from Tally: {guid} / {ledgerName}."); invalidPosting = true; continue; }
                    if (!decimal.TryParse(Value(node, "AMOUNT"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var signed))
                    { errors.Add($"Purchase voucher has an invalid signed ledger posting: {guid} / {ledgerName}."); invalidPosting = true; continue; }
                    postings.Add(new(sequence, mapped[0].TallyGuid!, ledgerName, signed, purchaseLedgerGuids.Contains(mapped[0].TallyGuid!)));
                }
                if (invalidPosting) continue;
                if (postings.Count == 0 || decimal.Round(postings.Sum(x => x.SignedAmount), 4) != 0)
                { errors.Add($"Purchase voucher postings are missing or unbalanced: {guid}."); continue; }
                var purchasePostings = postings.Where(x => x.IsPurchaseLedger).ToArray();
                if (purchasePostings.Length == 0)
                { errors.Add($"Voucher classified as Purchase has no posting in Tally's Purchase Accounts hierarchy: {guid}."); continue; }
                var debit = decimal.Round(purchasePostings.Where(x => x.SignedAmount < 0).Sum(x => -x.SignedAmount), 2);
                var credit = decimal.Round(purchasePostings.Where(x => x.SignedAmount > 0).Sum(x => x.SignedAmount), 2);
                var purchaseNames = purchasePostings.Select(x => x.LedgerName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var purchaseGuids = purchasePostings.Select(x => x.LedgerGuid).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var narration = Value(voucher, "NARRATION");
                if (partyName.Length > 250 || narration.Length > 2000 || purchaseNames.Sum(x => x.Length) > 990 || purchaseGuids.Sum(x => x.Length) > 990)
                { errors.Add($"Purchase voucher text values exceed storage limits: {guid}."); continue; }
                active.Add(new(companyId, guid, number, date, type, partyName, partyRows[0].TallyGuid!, purchaseNames, purchaseGuids,
                    Math.Abs(debit - credit), debit, credit, narration, postings.ToArray()));
            }
            cursor = monthEnd.AddDays(1);
        }

        if (!metadataPurchases.Select(Identity).Order(StringComparer.Ordinal).SequenceEqual(monthlyIdentities.Order(StringComparer.Ordinal)))
            errors.Add("Purchase history metadata and monthly posting exports did not reconcile.");
        var metadataAfter = await Export("PurchaseVoucherMetadataAfter", "Voucher", "GUID,Date,VoucherTypeName,VoucherNumber,IsCancelled", scope + range, token);
        if (!metadataPurchases.Select(Identity).Order(StringComparer.Ordinal).SequenceEqual(SelectPurchase(metadataAfter, knownTypes, purchaseTypes, errors).Select(Identity).Order(StringComparer.Ordinal)))
            errors.Add("Tally Purchase voucher identities or cancellation states changed during preview.");
        var finalCompanyDoc = await Export("PurchaseCompaniesAfter", "Company", "GUID,Name,BooksFrom,EndingAt", "", token);
        var finalCompany = finalCompanyDoc.Descendants("COMPANY").Where(x => Value(x, "GUID").Equals(company.TallyGuid, StringComparison.OrdinalIgnoreCase) && Value(x, "NAME").Equals(company.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (finalCompany.Length != 1 || Value(finalCompany[0], "BOOKSFROM") != Value(matching[0], "BOOKSFROM") || Value(finalCompany[0], "ENDINGAT") != Value(matching[0], "ENDINGAT"))
            errors.Add("Tally company identity or book dates changed during preview.");
        var finalSqlLedgers = await db.Ledgers.AsNoTracking().Where(x => x.CompanyId == companyId).Select(x => new { x.Id, x.Name, x.TallyGuid }).ToListAsync(token);
        if (ledgerFingerprint != Hash(finalSqlLedgers.OrderBy(x => x.Id).ToArray())) errors.Add("SQL ledger mappings changed during preview.");
        if (active.GroupBy(x => PurchaseImportPlan.Key(x.TallyGuid)).Any(g => g.Count() > 1) || cancelled.GroupBy(x => PurchaseImportPlan.Key(x.TallyGuid)).Any(g => g.Count() > 1))
            errors.Add("Tally returned duplicate Purchase voucher GUIDs.");

        return new(companyId, company.Name, company.TallyGuid, from, to, purchaseTypes.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            active.ToArray(), cancelled.ToArray(), errors.Distinct().Order(StringComparer.Ordinal).ToArray(),
            Hash(new { company.TallyGuid, from, to, purchaseTypes = purchaseTypes.Order(StringComparer.OrdinalIgnoreCase), active = active.OrderBy(x => x.TallyGuid).ToArray(), cancelled = cancelled.OrderBy(x => x.TallyGuid).ToArray(), ledgerFingerprint }));
    }

    private async Task<XDocument> Export(string id, string type, string fetch, string variables, CancellationToken token)
    {
        var request = $"<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>{id}</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>{variables}</STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME=\"{id}\"><TYPE>{type}</TYPE><FETCH>{fetch}</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>";
        var raw = await TallyXmlTransport.PostAsync(http, request, token);
        raw = Regex.Replace(raw, @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);", "");
        raw = Regex.Replace(raw, @"(</?)UDF:", "$1");
        var doc = XDocument.Parse(raw);
        if (doc.Descendants("LINEERROR").Any() || !doc.Descendants("STATUS").Any(x => x.Value.Trim() == "1") || !doc.Descendants("COLLECTION").Any())
            throw new InvalidOperationException("Tally XML export failed or returned no verified collection.");
        return doc;
    }
    private static HashSet<string> Descendants(string root, (string Name, string Parent)[] rows)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        bool changed;
        do { changed = false; foreach (var row in rows) if (result.Contains(row.Parent)) changed |= result.Add(row.Name); } while (changed);
        return result;
    }
    private static XElement[] SelectPurchase(XDocument doc, HashSet<string> known, HashSet<string> purchase, List<string> errors)
    {
        // Tally puts a scalar <VOUCHER>0</VOUCHER> count under CMPINFO in
        // collection responses. Only treat structurally complete records as
        // vouchers; real rows still reach the unknown-type check if their type
        // name is blank or missing.
        var vouchers = doc.Descendants("VOUCHER")
            .Where(x => x.Elements().Any(e => e.Name.LocalName.Equals("GUID", StringComparison.OrdinalIgnoreCase)) &&
                        x.Elements().Any(e => e.Name.LocalName.Equals("DATE", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        foreach (var v in vouchers.Where(x => !known.Contains(Value(x, "VOUCHERTYPENAME")))) errors.Add("Unknown Tally voucher type prevents safe Purchase classification: " + Value(v, "VOUCHERTYPENAME"));
        return vouchers.Where(v => purchase.Contains(Value(v, "VOUCHERTYPENAME"))).ToArray();
    }
    private static string Identity(XElement x) => Hash(new { Guid = Value(x, "GUID").Trim().ToUpperInvariant(), Date = Value(x, "DATE"), Type = Value(x, "VOUCHERTYPENAME"), Number = Value(x, "VOUCHERNUMBER"), Cancelled = Value(x, "ISCANCELLED").ToUpperInvariant() });
    private static string Value(XElement x, string name) => x.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
    private static bool TryDate(string s, out DateTime d) => DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    private static DateTime ParseDate(string s) => TryDate(s, out var d) ? d : throw new InvalidOperationException("Tally company book date is unavailable.");
    private static string Range(DateTime from, DateTime to) => $"<SVFROMDATE TYPE=\"Date\">{from:yyyyMMdd}</SVFROMDATE><SVTODATE TYPE=\"Date\">{to:yyyyMMdd}</SVTODATE>";
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private sealed record TallyLedger(string Name, string Parent, string Guid);
}
