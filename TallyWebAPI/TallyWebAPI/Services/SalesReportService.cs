using System.Text;
using System.Xml.Linq;
using TallyWebAPI.DTOs;

namespace TallyWebAPI.Services
{
    public class SalesReportService
    {
        private readonly HttpClient _httpClient;

        public SalesReportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<SalesReportDto>> GetSalesReportAsync(
            string fromDate,
            string toDate)
        {
            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>SalesReportCollection</ID>
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

                                <COLLECTION NAME="SalesReportCollection">
                                    <TYPE>Voucher</TYPE>

                                    <FILTER>SalesVoucherFilter</FILTER>

                                    <FETCH>Date</FETCH>
                                    <FETCH>VoucherNumber</FETCH>
                                    <FETCH>VoucherTypeName</FETCH>
                                    <FETCH>PartyLedgerName</FETCH>
                                    <FETCH>AllLedgerEntries.*</FETCH>
                                    <FETCH>LedgerEntries.*</FETCH>
                                </COLLECTION>

                                <SYSTEM TYPE="Formulae"
                                        NAME="SalesVoucherFilter">
                                    $VoucherTypeName = "Sales"
                                </SYSTEM>

                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            var xml = await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);

            // Remove invalid XML 1.0 control character references
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            // Remove undeclared Tally UDF prefix
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"(<\/?)UDF:",
                "$1"
            );

            var result = new List<SalesReportDto>();

            var balanceCache =
            new Dictionary<string, (decimal Opening, decimal Closing)>(
                StringComparer.OrdinalIgnoreCase
            );

            var document = XDocument.Parse(xml);

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
                var date = GetValue(voucher, "DATE");
                var voucherNumber = GetValue(
                    voucher,
                    "VOUCHERNUMBER"
                );

                var voucherType = GetValue(
                    voucher,
                    "VOUCHERTYPENAME"
                );
                if (string.IsNullOrWhiteSpace(date) ||
                string.IsNullOrWhiteSpace(voucherType) ||
                !voucherType.Equals(
                    "Sales",
                    StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                var partyLedger = GetValue(
                    voucher,
                    "PARTYLEDGERNAME"
                );

                if (!balanceCache.TryGetValue(
                    partyLedger,
                    out var balances))
                {
                    balances = await GetLedgerBalancesAsync(
                        partyLedger,
                        fromDate,
                        toDate
                    );

                    balanceCache[partyLedger] = balances;
                }

                decimal debit = 0;
                decimal credit = 0;

                var ledgerEntries = voucher
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "ALLLEDGERENTRIES.LIST",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                foreach (var entry in ledgerEntries)
                {
                    var ledgerName = GetValue(
                        entry,
                        "LEDGERNAME"
                    );

                    // Report should show party ledger amount
                    if (!string.Equals(
                            ledgerName,
                            partyLedger,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var amountText = GetValue(
                        entry,
                        "AMOUNT"
                    );

                    if (!decimal.TryParse(
                            amountText,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var amount))
                    {
                        continue;
                    }

                    /*
                       Tally voucher amount convention used here:
                       Negative party amount = Debit
                       Positive party amount = Credit
                    */

                    if (amount < 0)
                    {
                        debit += Math.Abs(amount);
                    }
                    else if (amount > 0)
                    {
                        credit += amount;
                    }
                }

                result.Add(new SalesReportDto
                {
                    Date = date,
                    LedgerName = partyLedger,
                    VoucherType = voucherType,
                    VoucherNumber = voucherNumber,
                    Debit = debit,
                    Credit = credit,

                    OpeningBalance = balances.Opening,
                    ClosingBalance = balances.Closing
                });
            }

            return result
                .OrderBy(x => x.Date)
                .ThenBy(x => x.VoucherNumber)
                .ToList();
        }


        public async Task<string> GetLedgerBalanceTestAsync(
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

                        <SYSTEM TYPE="Formulae"
                                NAME="SelectedLedgerFilter">
                            $Name = "{System.Security.SecurityElement.Escape(ledgerName)}"
                        </SYSTEM>

                    </TDLMESSAGE>
                </TDL>
            </DESC>
        </BODY>
    </ENVELOPE>
    """;

            return await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);
        }


        private async Task<(decimal Opening, decimal Closing)>
    GetLedgerBalancesAsync(
        string ledgerName,
        string fromDate,
        string toDate)
        {
            var xml = await GetLedgerBalanceTestAsync(
                ledgerName,
                fromDate,
                toDate
            );

            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

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

            var openingText = GetValue(
                ledger,
                "OPENINGBALANCE"
            );

            var closingText = GetValue(
                ledger,
                "CLOSINGBALANCE"
            );

            decimal.TryParse(
                openingText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var opening
            );

            decimal.TryParse(
                closingText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var closing
            );

            return (opening, closing);
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
