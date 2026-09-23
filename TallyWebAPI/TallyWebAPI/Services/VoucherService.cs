using System.Text;
using System.Xml.Linq;
using TallyWebAPI.DTOs;

namespace TallyWebAPI.Services
{
    public class VoucherService
    {
        private readonly HttpClient _httpClient;

        public VoucherService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

   public async Task<string> GetVouchersAsync(string fromDate,string toDate)  
        {
            var xmlRequest = $"""
    <ENVELOPE>
        <HEADER>
            <VERSION>1</VERSION>
            <TALLYREQUEST>Export</TALLYREQUEST>
            <TYPE>Collection</TYPE>
            <ID>VoucherCollection</ID>
        </HEADER>

        <BODY>
            <DESC>

        <STATICVARIABLES>
            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
    <SVENCODINGTYPE>UTF-8</SVENCODINGTYPE>

           <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
    <SVTODATE TYPE="Date">{toDate}</SVTODATE>
        </STATICVARIABLES>
                <TDL>
                    <TDLMESSAGE>

                        <COLLECTION NAME="VoucherCollection">
                            <TYPE>Voucher</TYPE>

                            <FETCH>Date</FETCH>
                            <FETCH>VoucherNumber</FETCH>
                            <FETCH>VoucherTypeName</FETCH>

                            <FETCH>PartyLedgerName</FETCH>
                            <FETCH>PartyGSTIN</FETCH>

                            <FETCH>StateName</FETCH>
                            <FETCH>PlaceOfSupply</FETCH>

                            <FETCH>Narration</FETCH>
                            <FETCH>GUID</FETCH>

                            <FETCH>AllLedgerEntries.*</FETCH>
                            <FETCH>LedgerEntries.*</FETCH>

                        </COLLECTION>

                    </TDLMESSAGE>
                </TDL>
            </DESC>
        </BODY>
    </ENVELOPE>
    """;

            using var content = new StringContent(
                xmlRequest,
                Encoding.UTF8,
                "text/xml"
            );

            var response = await _httpClient.PostAsync(
                "http://127.0.0.1:9000",
                content
            );

            var result = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            return result;
        }



        public async Task<List<VoucherDto>> GetVoucherListAsync(
            string fromDate,
            string toDate)
        {
            if (!DateTime.TryParseExact(
                    fromDate,
                    "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var startDate))
            {
                throw new ArgumentException("Invalid From Date.");
            }

            if (!DateTime.TryParseExact(
                    toDate,
                    "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var endDate))
            {
                throw new ArgumentException("Invalid To Date.");
            }

            if (startDate > endDate)
            {
                throw new ArgumentException(
                    "From Date cannot be greater than To Date."
                );
            }

            var result = new List<VoucherDto>();

            var currentStart = startDate;

            while (currentStart <= endDate)
            {
                // End of current month
                var currentEnd = new DateTime(
                    currentStart.Year,
                    currentStart.Month,
                    DateTime.DaysInMonth(
                        currentStart.Year,
                        currentStart.Month
                    )
                );

                // Do not go beyond user's To Date
                if (currentEnd > endDate)
                {
                    currentEnd = endDate;
                }

                var batchFromDate =
                    currentStart.ToString("yyyyMMdd");

                var batchToDate =
                    currentEnd.ToString("yyyyMMdd");

                // Fetch only one month from Tally
                var xml = await GetVouchersAsync(
                    batchFromDate,
                    batchToDate
                );

                // Remove invalid XML 1.0 character references
                xml = System.Text.RegularExpressions.Regex.Replace(
                    xml,
                    @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                    ""
                );

                // Remove undeclared UDF prefix
                xml = System.Text.RegularExpressions.Regex.Replace(
                    xml,
                    @"(<\/?)UDF:",
                    "$1"
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
                    // Skip empty/non-voucher placeholder rows
                    var date = GetValue(voucher, "DATE");
                    var voucherNumber =
                        GetValue(voucher, "VOUCHERNUMBER");
                    var voucherType =
                        GetValue(voucher, "VOUCHERTYPENAME");

                    if (string.IsNullOrWhiteSpace(date) &&
                        string.IsNullOrWhiteSpace(voucherNumber) &&
                        string.IsNullOrWhiteSpace(voucherType))
                    {
                        continue;
                    }

                    result.Add(new VoucherDto
                    {
                        Date = date,

                        VoucherNumber = voucherNumber,

                        VoucherType = voucherType,

                        PartyName = GetValue(
                            voucher,
                            "PARTYLEDGERNAME"
                        ),

                        PartyGstin = GetValue(
                            voucher,
                            "PARTYGSTIN"
                        ),

                        State = GetValue(
                            voucher,
                            "STATENAME"
                        ),

                        PlaceOfSupply = GetValue(
                            voucher,
                            "PLACEOFSUPPLY"
                        ),

                        Amount = GetVoucherAmount(voucher),

                        Narration = GetValue(
                            voucher,
                            "NARRATION"
                        ),

                        Guid = GetValue(
                            voucher,
                            "GUID"
                        )
                    });
                }

                // Move to next month
                currentStart = currentEnd.AddDays(1);
            }

            // Safety: remove duplicate vouchers if any
            result = result
                .GroupBy(v =>
                    !string.IsNullOrWhiteSpace(v.Guid)
                        ? v.Guid
                        : $"{v.Date}|{v.VoucherType}|{v.VoucherNumber}"
                )
                .Select(g => g.First())
                .OrderBy(v => v.Date)
                .ThenBy(v => v.VoucherNumber)
                .ToList();

            return result;
        }

        public async Task<string> GetVoucherExportAsync(
            string voucherNumber,
            string voucherType,
            string fromDate,
            string toDate)
        {
            var xmlRequest = $"""
    <ENVELOPE>
        <HEADER>
            <VERSION>1</VERSION>
            <TALLYREQUEST>Export</TALLYREQUEST>
            <TYPE>Data</TYPE>
            <ID>DayBook</ID>
        </HEADER>

        <BODY>
            <DESC>
                <STATICVARIABLES>
                    <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                    <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
                    <SVTODATE TYPE="Date">{toDate}</SVTODATE>
                </STATICVARIABLES>
            </DESC>
        </BODY>
    </ENVELOPE>
    """;

            using var content = new StringContent(
                xmlRequest,
                Encoding.UTF8,
                "text/xml"
            );

            var response = await _httpClient.PostAsync(
                "http://127.0.0.1:9000",
                content
            );

            var bytes = await response.Content.ReadAsByteArrayAsync();

            response.EnsureSuccessStatusCode();

            return Encoding.UTF8.GetString(bytes);
        }


        public async Task<VoucherDetailDto?> GetVoucherDetailAsync(
    string guid,
    string fromDate,
    string toDate)
        {
            var xml = await GetVouchersAsync(fromDate, toDate);

            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"(<\/?)UDF:",
                "$1"
            );

            var document = XDocument.Parse(xml);

            var voucher = document
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        "VOUCHER",
                        StringComparison.OrdinalIgnoreCase
                    )
                    &&
                    GetValue(x, "GUID").Equals(
                        guid,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            if (voucher == null)
                return null;

            var detail = new VoucherDetailDto
            {
                Date = GetValue(voucher, "DATE"),
                VoucherNumber = GetValue(voucher, "VOUCHERNUMBER"),
                VoucherType = GetValue(voucher, "VOUCHERTYPENAME"),

                PartyName = GetValue(voucher, "PARTYLEDGERNAME"),
                PartyGstin = GetValue(voucher, "PARTYGSTIN"),

                State = GetValue(voucher, "STATENAME"),
                PlaceOfSupply = GetValue(voucher, "PLACEOFSUPPLY"),

                Reference = GetValue(voucher, "REFERENCE"),
                Narration = GetValue(voucher, "NARRATION"),
                Guid = GetValue(voucher, "GUID"),

                TotalAmount = GetVoucherAmount(voucher)
            };

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
                var ledgerName = GetValue(entry, "LEDGERNAME");
                var amount = GetValue(entry, "AMOUNT");

                if (string.IsNullOrWhiteSpace(ledgerName))
                    continue;

                detail.LedgerEntries.Add(new VoucherLedgerEntryDto
                {
                    LedgerName = ledgerName,
                    Amount = amount
                });
            }

            var inventoryEntries = voucher
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "ALLINVENTORYENTRIES.LIST",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            foreach (var entry in inventoryEntries)
            {
                var itemName = GetValue(entry, "STOCKITEMNAME");

                if (string.IsNullOrWhiteSpace(itemName))
                    continue;

                detail.Items.Add(new VoucherItemEntryDto
                {
                    ItemName = itemName,
                    Quantity = GetValue(entry, "ACTUALQTY"),
                    Rate = GetValue(entry, "RATE"),
                    Amount = CleanAmount(GetValue(entry, "AMOUNT"))
                });
            }

            return detail;
        }
        private static string GetVoucherAmount(XElement voucher)
        {
            // Tally voucher total is normally represented by
            // the Party Ledger entry amount.
            var partyLedgerName = GetValue(voucher, "PARTYLEDGERNAME");

            var ledgerEntries = voucher
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "ALLLEDGERENTRIES.LIST",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    x.Name.LocalName.Equals(
                        "LEDGERENTRIES.LIST",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // First preference:
            // Amount belonging to PARTYLEDGERNAME
            foreach (var entry in ledgerEntries)
            {
                var ledgerName = GetValue(entry, "LEDGERNAME");

                if (!string.IsNullOrWhiteSpace(partyLedgerName) &&
                    ledgerName.Equals(
                        partyLedgerName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    var amount = GetValue(entry, "AMOUNT");

                    if (!string.IsNullOrWhiteSpace(amount))
                        return CleanAmount(amount);
                }
            }

            // Fallback:
            // First available ledger amount
            foreach (var entry in ledgerEntries)
            {
                var amount = GetValue(entry, "AMOUNT");

                if (!string.IsNullOrWhiteSpace(amount))
                    return CleanAmount(amount);
            }

            return "";
        }

        private static string CleanAmount(string amount)
        {
            if (decimal.TryParse(
                amount,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            {
                return Math.Abs(value).ToString(
                    "0.00",
                    System.Globalization.CultureInfo.InvariantCulture
                );
            }

            return amount;
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


        public async Task<string> GetTamilVoucherTestAsync(
    string fromDate,
    string toDate)
        {
            var xmlRequest = $"""
    <ENVELOPE>
        <HEADER>
            <VERSION>1</VERSION>
            <TALLYREQUEST>Export</TALLYREQUEST>
            <TYPE>Collection</TYPE>
            <ID>TamilVoucherCollection</ID>
        </HEADER>

        <BODY>
            <DESC>
                <STATICVARIABLES>
                    <SVCURRENTCOMPANY>JAYA JOTHI MALIGAI</SVCURRENTCOMPANY>
                    <SVFROMDATE TYPE="Date">{fromDate}</SVFROMDATE>
                    <SVTODATE TYPE="Date">{toDate}</SVTODATE>
                    <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                </STATICVARIABLES>

                <TDL>
                    <TDLMESSAGE>

                        <COLLECTION NAME="TamilVoucherCollection">
                            <TYPE>Voucher</TYPE>

                            <FETCH>Date</FETCH>
                            <FETCH>VoucherNumber</FETCH>
                            <FETCH>VoucherTypeName</FETCH>
                            <FETCH>GUID</FETCH>

                            <FETCH>AllInventoryEntries.StockItemName</FETCH>
                            <FETCH>AllInventoryEntries.ActualQty</FETCH>
                            <FETCH>AllInventoryEntries.Rate</FETCH>
                            <FETCH>AllInventoryEntries.Amount</FETCH>
                        </COLLECTION>

                    </TDLMESSAGE>
                </TDL>
            </DESC>
        </BODY>
    </ENVELOPE>
    """;

            using var content = new StringContent(
                xmlRequest,
                Encoding.UTF8,
                "text/xml"
            );

            content.Headers.ContentType!.CharSet = "utf-8";

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "http://127.0.0.1:9000"
            );

            request.Content = content;

            request.Headers.TryAddWithoutValidation(
                "Accept",
                "application/xml"
            );

            request.Headers.TryAddWithoutValidation(
                "Accept-Charset",
                "utf-8"
            );

            var response = await _httpClient.SendAsync(request);

            var bytes = await response.Content.ReadAsByteArrayAsync();

            response.EnsureSuccessStatusCode();

            return Encoding.UTF8.GetString(bytes);
        }
    }
}