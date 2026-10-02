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

        public async Task<LedgerReportDto> GetLedgerReportAsync(
            string ledgerName,
            string fromDate,
            string toDate)
        {
            var safeLedgerName =
                SecurityElement.Escape(ledgerName) ?? "";

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
                            <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
                            <SVTODATE TYPE="Date">{toDate}</SVTODATE>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>

                                <COLLECTION NAME="LedgerReportCollection">
                                    <TYPE>Voucher</TYPE>

                                    <FETCH>Date</FETCH>
                                    <FETCH>VoucherNumber</FETCH>
                                    <FETCH>VoucherTypeName</FETCH>
                                    <FETCH>PartyLedgerName</FETCH>
                                    <FETCH>AllLedgerEntries.*</FETCH>
                                </COLLECTION>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            var xml = await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);

            xml = CleanXml(xml);

            var document = XDocument.Parse(xml);

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

                // Find selected ledger inside this voucher.
                var selectedEntry = entries.FirstOrDefault(entry =>
                    string.Equals(
                        GetValue(entry, "LEDGERNAME"),
                        ledgerName,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

                // Voucher does not belong to selected ledger.
                if (selectedEntry == null)
                    continue;

                var amountText = GetValue(
                    selectedEntry,
                    "AMOUNT"
                );

                if (!decimal.TryParse(
                        amountText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var amount))
                {
                    continue;
                }

                decimal debit = 0;
                decimal credit = 0;

                /*
                    Tally voucher convention:
                    Negative selected-ledger amount = Debit
                    Positive selected-ledger amount = Credit
                */
                if (amount < 0)
                {
                    debit = Math.Abs(amount);
                }
                else if (amount > 0)
                {
                    credit = amount;
                }

                // Find opposite / corresponding ledger.
                var particulars = entries
                    .Select(entry => GetValue(
                        entry,
                        "LEDGERNAME"
                    ))
                    .FirstOrDefault(name =>
                        !string.IsNullOrWhiteSpace(name) &&
                        !string.Equals(
                            name,
                            ledgerName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    ) ?? "";

                var transaction = new LedgerTransactionDto
                {
                    Date = GetValue(voucher, "DATE"),
                    Particulars = particulars,
                    VoucherType = GetValue(
                        voucher,
                        "VOUCHERTYPENAME"
                    ),
                    VoucherNumber = GetValue(
                        voucher,
                        "VOUCHERNUMBER"
                    ),
                    Debit = debit,
                    Credit = credit
                };

                report.Transactions.Add(transaction);
            }

            report.Transactions = report.Transactions
                .OrderBy(x => x.Date)
                .ThenBy(x => x.VoucherNumber)
                .ToList();

            report.TotalDebit =
                report.Transactions.Sum(x => x.Debit);

            report.TotalCredit =
                report.Transactions.Sum(x => x.Credit);

            var balances = await GetLedgerBalancesAsync(
                safeLedgerName,
                ledgerName,
                fromDate,
                toDate
            );

            report.OpeningBalance = Math.Abs(balances.Opening);
            report.ClosingBalance = Math.Abs(balances.Closing);

            return report;
        }

        private async Task<(decimal Opening, decimal Closing)>
            GetLedgerBalancesAsync(
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
                return (0, 0);

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
