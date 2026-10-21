using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TallyWebAPI.DTOs;

namespace TallyWebAPI.Services
{
    public class LedgerReportService
    {
        private readonly HttpClient _httpClient;

        public LedgerReportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task VerifyLedgerIdentityAsync(string companyName, string ledgerName, string expectedGuid)
        {
            if (string.IsNullOrWhiteSpace(expectedGuid))
                throw new InvalidDataException("The selected SQL ledger has no Tally GUID. No Ledger Report data was saved.");
            var safeCompany = SecurityElement.Escape(companyName) ?? "";
            var request = $"""
            <ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>WebLedgerIdentity</ID></HEADER>
            <BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT><SVCURRENTCOMPANY>{safeCompany}</SVCURRENTCOMPANY></STATICVARIABLES>
            <TDL><TDLMESSAGE><COLLECTION NAME="WebLedgerIdentity"><TYPE>Ledger</TYPE><FETCH>Name,GUID</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>
            """;
            var doc = XDocument.Parse(CleanXml(await TallyXmlTransport.PostAsync(_httpClient, request)));
            ThrowIfExportFailed(doc);
            var ledgers = doc.Descendants().Where(x => x.Name.LocalName.Equals("LEDGER", StringComparison.OrdinalIgnoreCase))
                .Where(x => string.Equals(x.Attribute("NAME")?.Value.Trim(), ledgerName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (ledgers.Count != 1) throw new InvalidDataException($"TallyPrime did not return one unambiguous ledger identity for '{ledgerName}'. No Ledger Report data was saved.");
            var actual = GetValue(ledgers[0], "GUID");
            if (!string.Equals(actual, expectedGuid.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"TallyPrime ledger identity does not match SQL ledger '{ledgerName}'. No Ledger Report data was saved.");
        }

        public async Task<LedgerReportDto> GetLedgerReportAsync(
            string companyName,
            string ledgerName,
            string? fromDate,
            string? toDate)
        {
            var safeLedgerName =
                SecurityElement.Escape(ledgerName) ?? "";
            var safeCompanyName = SecurityElement.Escape(companyName) ?? "";

            // Omitting Tally's date variables requests the full synchronized company history.
            var dateVariables = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(fromDate))
                dateVariables.Append($"<SVFROMDATE TYPE=\"Date\">{SecurityElement.Escape(fromDate)}</SVFROMDATE>");
            if (!string.IsNullOrWhiteSpace(toDate))
                dateVariables.Append($"<SVTODATE TYPE=\"Date\">{SecurityElement.Escape(toDate)}</SVTODATE>");

            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>LedgerReportCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                            {dateVariables}
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>

                                <COLLECTION NAME="LedgerReportCollection">
                                    <TYPE>Voucher</TYPE>
                                    <FILTER>HasSelectedLedger</FILTER>

                                    <FETCH>Date</FETCH>
                                    <FETCH>VoucherNumber</FETCH>
                                    <FETCH>VoucherTypeName</FETCH>
                                    <FETCH>PartyLedgerName</FETCH>
                                    <FETCH>AllLedgerEntries.*</FETCH>
                                    <FETCH>LedgerEntries.*</FETCH>
                                </COLLECTION>

                                <SYSTEM TYPE="Formulae" NAME="HasSelectedLedger">
                                    ($$FilterCount:AllLedgerEntries:SelectedReportLedger &gt; 0) OR ($$FilterCount:LedgerEntries:SelectedReportLedger &gt; 0)
                                </SYSTEM>
                                <SYSTEM TYPE="Formulae" NAME="SelectedReportLedger">
                                    $LedgerName = "{safeLedgerName}"
                                </SYSTEM>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            var xml = await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);

            xml = CleanXml(xml);

            var document = XDocument.Parse(xml);
            ThrowIfExportFailed(document);

            var report = new LedgerReportDto
            {
                LedgerName = ledgerName
            };

            var vouchers = document
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "VOUCHER",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            foreach (var voucher in vouchers)
            {
                var entries = voucher
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "ALLLEDGERENTRIES.LIST",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .ToList();

                // Some voucher exports use the legacy LEDGERENTRIES list name.
                // Use it only when the canonical ALLLEDGERENTRIES list is absent
                // so the same posting is never counted twice.
                if (entries.Count == 0)
                {
                    entries = voucher
                        .Descendants()
                        .Where(x => x.Name.LocalName.Equals(
                            "LEDGERENTRIES.LIST",
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // A voucher can contain several postings to the selected ledger.
                // Keep debit and credit movements separate rather than netting them.
                var selectedEntries = entries.Where(entry => string.Equals(
                    GetValue(entry, "LEDGERNAME"), ledgerName.Trim(),
                    StringComparison.OrdinalIgnoreCase)).ToList();
                if (selectedEntries.Count == 0)
                    continue;
                var particulars = entries.Select(entry => GetValue(entry, "LEDGERNAME"))
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name) &&
                        !string.Equals(name, ledgerName, StringComparison.OrdinalIgnoreCase))
                    ?? GetValue(voucher, "PARTYLEDGERNAME");

                foreach (var entry in selectedEntries)
                {
                    if (!decimal.TryParse(GetValue(entry, "AMOUNT"), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var amount))
                        throw new InvalidOperationException("A selected-ledger posting has an invalid amount; the report cannot be reconciled.");
                    report.Transactions.Add(new LedgerTransactionDto
                    {
                        Date = GetValue(voucher, "DATE"),
                        Particulars = particulars,
                        VoucherType = GetValue(voucher, "VOUCHERTYPENAME"),
                        VoucherNumber = GetValue(voucher, "VOUCHERNUMBER"),
                        Debit = amount < 0 ? -amount : 0,
                        Credit = amount > 0 ? amount : 0
                    });
                }
            }

            var allTransactions = report.Transactions
                .OrderBy(x => x.Date)
                .ThenBy(x => x.VoucherNumber)
                .ToList();

            var dates = allTransactions.Select(x => ParseTallyDate(x.Date)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
            var earliest = dates.Count > 0 ? dates.Min() : (DateTime?)null;
            var latest = dates.Count > 0 ? dates.Max() : (DateTime?)null;
            var resolvedFrom = ParseRequestDate(fromDate) ?? earliest;
            var resolvedTo = ParseRequestDate(toDate) ?? latest;
            if (resolvedFrom.HasValue && resolvedTo.HasValue)
            {
                report.Transactions = allTransactions.Where(x =>
                {
                    var date = ParseTallyDate(x.Date);
                    return date.HasValue && date.Value >= resolvedFrom.Value && date.Value <= resolvedTo.Value;
                }).ToList();
            }
            report.FromDate = resolvedFrom?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "";
            report.ToDate = resolvedTo?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "";
            report.HistoryComplete = false;
            report.HistoryNote = earliest.HasValue
                ? $"Live Tally transactions returned: {earliest:dd-MMM-yyyy} to {latest:dd-MMM-yyyy}. Synchronized history completeness is not verified."
                : "No live Tally transactions were returned for this period; synchronized history completeness is not verified.";

            report.TotalDebit =
                report.Transactions.Sum(x => x.Debit);

            report.TotalCredit =
                report.Transactions.Sum(x => x.Credit);

            var balances = await GetLedgerBalancesAsync(
                safeCompanyName,
                safeLedgerName,
                ledgerName,
                report.FromDate,
                report.ToDate
            );

            // Tally uses signed ledger balances: negative is Dr, positive is Cr.
            // Debit postings move the signed balance downward; credits move it upward.
            // OpeningBalance is already period-aware from Tally, so don't re-add
            // transactions that aren't present in this date-filtered collection.
            report.OpeningBalance = balances.Opening;
            report.ClosingBalance = LedgerBalanceMath.CalculateClosingBalance(
                report.OpeningBalance,
                report.TotalDebit,
                report.TotalCredit);
            if (report.ClosingBalance != balances.Closing)
                throw new InvalidDataException("Selected-ledger postings do not reconcile to TallyPrime closing balance. No snapshot was saved.");

            return report;
        }

        private async Task<(decimal Opening, decimal Closing)>
            GetLedgerBalancesAsync(
                string safeCompanyName,
                string safeLedgerName,
                string ledgerName,
                string fromDate,
                string toDate)
        {
            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>LedgerBalanceCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                            <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
                            <SVTODATE TYPE="Date">{toDate}</SVTODATE>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>

                                <COLLECTION NAME="LedgerBalanceCollection">
                                    <TYPE>Ledger</TYPE>
                                    <FILTER>SelectedLedgerFilter</FILTER>

                                    <FETCH>Name</FETCH>
                                    <FETCH>OpeningBalance</FETCH>
                                    <FETCH>ClosingBalance</FETCH>
                                </COLLECTION>

                                <SYSTEM
                                    TYPE="Formulae"
                                    NAME="SelectedLedgerFilter">
                                    $Name = "{safeLedgerName}"
                                </SYSTEM>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            var xml = await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);

            xml = CleanXml(xml);

            var document = XDocument.Parse(xml);
            ThrowIfExportFailed(document);

            var ledger = document
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        "LEDGER",
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    string.Equals(
                        x.Attribute("NAME")?.Value?.Trim(),
                        ledgerName.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            if (ledger == null)
                throw new InvalidOperationException("Tally did not return the selected ledger balance; the report cannot be reconciled.");

            decimal.TryParse(
                GetValue(ledger, "OPENINGBALANCE"),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var opening
            );

            decimal.TryParse(
                GetValue(ledger, "CLOSINGBALANCE"),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var closing
            );

            return (opening, closing);
        }

        private static string CleanXml(string xml)
        {
            xml = Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            xml = Regex.Replace(
                xml,
                @"(<\/?)UDF:",
                "$1"
            );

            return xml;
        }

        private static void ThrowIfExportFailed(XDocument document)
        {
            var error = document.Descendants().FirstOrDefault(x =>
                x.Name.LocalName.Equals("LINEERROR", StringComparison.OrdinalIgnoreCase));
            if (error != null)
                throw new InvalidOperationException($"Tally export failed: {error.Value.Trim()}");
        }

        private static DateTime? ParseTallyDate(string value) => ParseRequestDate(value);

        private static DateTime? ParseRequestDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().Replace("-", "").Replace("/", "");
            return DateTime.TryParseExact(normalized, new[] { "yyyyMMdd", "ddMMyyyy", "yyyy-MM-dd", "dd-MM-yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result.Date : null;
        }

        private static string GetValue(
            XElement? parent,
            string elementName)
        {
            if (parent == null)
                return "";

            return parent
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        elementName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.Value
                ?.Trim() ?? "";
        }
    }
}
