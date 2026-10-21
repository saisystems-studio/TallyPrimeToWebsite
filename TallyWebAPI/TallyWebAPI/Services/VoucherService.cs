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

        public async Task<string> GetVouchersAsync(string fromDate, string toDate, string companyName)
        {

            var safeCompanyName = System.Security.SecurityElement.Escape(companyName) ?? "";

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

        <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>

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

            return await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);
        }



        public async Task<List<VoucherDto>> GetVoucherListAsync(
            string fromDate,
            string toDate,
            string companyName)
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
                    batchToDate,
                    companyName
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
                var exportError = document.Descendants().FirstOrDefault(x =>
                    x.Name.LocalName.Equals("LINEERROR", StringComparison.OrdinalIgnoreCase));
                if (exportError != null)
                    throw new InvalidOperationException($"Tally voucher export failed: {exportError.Value.Trim()}");

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

                        Postings = ParsePostings(voucher),
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

            return await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);
        }


        public async Task<VoucherDetailDto?> GetVoucherDetailAsync(
            string guid,
            string fromDate,
            string toDate,
            string companyName)
        {
            var safeCompanyName = System.Security.SecurityElement.Escape(companyName) ?? "";
            var safeGuid = System.Security.SecurityElement.Escape(guid) ?? "";

            var xmlRequest = $"""
                <ENVELOPE>
                    <HEADER>
                        <VERSION>1</VERSION>
                        <TALLYREQUEST>Export</TALLYREQUEST>
                        <TYPE>Collection</TYPE>
                        <ID>VoucherDetailCollection</ID>
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
                                    <COLLECTION NAME="VoucherDetailCollection">
                                        <TYPE>Voucher</TYPE>
                                        <FILTER>VoucherDetailGuidFilter</FILTER>
                                        <FETCH>Date</FETCH>
                                        <FETCH>VoucherNumber</FETCH>
                                        <FETCH>VoucherTypeName</FETCH>
                                        <FETCH>PartyLedgerName</FETCH>
                                        <FETCH>PartyGSTIN</FETCH>
                                        <FETCH>StateName</FETCH>
                                        <FETCH>PlaceOfSupply</FETCH>
                                        <FETCH>Reference</FETCH>
                                        <FETCH>ReferenceDate,PartyName,PartyMailingName,Address,Address.*,PartyPincode,PartyCity,CountryOfResidence,PartyStateCode</FETCH>
                                        <FETCH>BasicBuyerName,BasicBuyerAddress.*,ConsigneeMailingName,ConsigneeAddress.*,ConsigneeGSTIN,ConsigneeStateName,ConsigneeStateCode,ConsigneePincode,ConsigneeCity,ConsigneeCountryName,ConsigneeMail,ConsigneeMobileNumber</FETCH>
                                        <FETCH>CmpGSTIN,CmpGSTState,BasicOrderRef</FETCH>
                                        <FETCH>AllLedgerEntries.*,LedgerEntries.*</FETCH>
                                        <FETCH>Narration</FETCH>
                                        <FETCH>GUID</FETCH>
                                        <FETCH>AllInventoryEntries.*</FETCH>
                                        <FETCH>InventoryEntries.*</FETCH>
                                    </COLLECTION>
                                    <SYSTEM TYPE="Formulae" NAME="VoucherDetailGuidFilter">
                                        $GUID = "{safeGuid}"
                                    </SYSTEM>
                                </TDLMESSAGE>
                            </TDL>
                        </DESC>
                    </BODY>
                </ENVELOPE>
                """;

            var xml = await TallyXmlTransport.PostAsync(_httpClient, xmlRequest);

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

            var partyGstin = InvoiceValue(voucher, "PARTYGSTIN");
            var consigneeGstin = InvoiceValue(voucher, "CONSIGNEEGSTIN");
            var detail = new VoucherDetailDto
            {
                Date = GetValue(voucher, "DATE"),
                VoucherNumber = GetValue(voucher, "VOUCHERNUMBER"),
                VoucherType = GetValue(voucher, "VOUCHERTYPENAME"),

                PartyName = FirstInvoiceValue(voucher, "PARTYNAME", "PARTYLEDGERNAME"),
                PartyLedgerName = InvoiceValue(voucher, "PARTYLEDGERNAME"),
                PartyAddress = InvoiceAddress(voucher, "ADDRESS.LIST", "ADDRESS"),
                PartyCity = InvoiceValue(voucher, "PARTYCITY"),
                PartyPincode = InvoiceValue(voucher, "PARTYPINCODE"),
                PartyCountry = InvoiceValue(voucher, "COUNTRYOFRESIDENCE"),
                PartyStateCode = InvoiceStateCode(InvoiceValue(voucher, "PARTYSTATECODE"), partyGstin),
                PartyGstin = partyGstin,
                CompanyGstin = InvoiceValue(voucher, "CMPGSTIN"),
                CompanyState = InvoiceValue(voucher, "CMPGSTSTATE"),
                Consignee = new InvoicePartyDto
                {
                    Name = FirstInvoiceValue(voucher, "CONSIGNEEMAILINGNAME", "BASICBUYERNAME"),
                    Address = FirstNonEmpty(InvoiceAddress(voucher, "CONSIGNEEADDRESS.LIST", "CONSIGNEEADDRESS"), InvoiceAddress(voucher, "BASICBUYERADDRESS.LIST", "BASICBUYERADDRESS")),
                    City = InvoiceValue(voucher, "CONSIGNEECITY"),
                    Gstin = consigneeGstin,
                    State = InvoiceValue(voucher, "CONSIGNEESTATENAME"),
                    StateCode = InvoiceStateCode(InvoiceValue(voucher, "CONSIGNEESTATECODE"), consigneeGstin),
                    Pincode = InvoiceValue(voucher, "CONSIGNEEPINCODE"),
                    Country = InvoiceValue(voucher, "CONSIGNEECOUNTRYNAME"),
                    Email = InvoiceValue(voucher, "CONSIGNEEMAIL"),
                    Mobile = InvoiceValue(voucher, "CONSIGNEEMOBILENUMBER")
                },

                State = GetValue(voucher, "STATENAME"),
                PlaceOfSupply = GetValue(voucher, "PLACEOFSUPPLY"),

                Reference = GetValue(voucher, "REFERENCE"),
                ReferenceDate = InvoiceValue(voucher, "REFERENCEDATE"),
                OtherReferences = InvoiceValue(voucher, "BASICORDERREF"),
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

            var inventoryEntries = voucher.Elements().Where(x => x.Name.LocalName.Equals("ALLINVENTORYENTRIES.LIST", StringComparison.OrdinalIgnoreCase)).ToList();
            if (inventoryEntries.Count == 0)
                inventoryEntries = voucher.Elements().Where(x => x.Name.LocalName.Equals("INVENTORYENTRIES.LIST", StringComparison.OrdinalIgnoreCase)).ToList();

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
                    Amount = CleanAmount(GetValue(entry, "AMOUNT")),
                    Discount = InvoiceValue(entry, "DISCOUNT")
                });
            }

            return detail;
        }

        // Invoice-only Company object export: the Company collection does not expose
        // its postal address in current TallyPrime. This never invokes sync or SQL writes.
        public async Task<InvoiceCompanyDto?> GetInvoiceCompanyAsync(string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Company name is required.");
            var name = System.Security.SecurityElement.Escape(companyName) ?? "";
            var request = $"""
            <ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Object</TYPE><SUBTYPE>Company</SUBTYPE><ID TYPE="Name">{name}</ID></HEADER><BODY><DESC><STATICVARIABLES><SVCURRENTCOMPANY>{name}</SVCURRENTCOMPANY><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT></STATICVARIABLES><FETCHLIST><FETCH>GUID</FETCH><FETCH>Name</FETCH><FETCH>Address</FETCH><FETCH>StateName</FETCH><FETCH>StateCode</FETCH><FETCH>CountryName</FETCH><FETCH>Pincode</FETCH><FETCH>Email</FETCH><FETCH>PhoneNumber</FETCH><FETCH>MobileNumbers</FETCH><FETCH>City</FETCH><FETCH>GSTRegistrationNumber</FETCH><FETCH>Address.*</FETCH></FETCHLIST></DESC></BODY></ENVELOPE>
            """;
            var xml = await TallyXmlTransport.PostAsync(_httpClient, request);
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);", "");
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"(<\/?)UDF:", "$1");
            var doc = XDocument.Parse(xml);
            var company = doc.Descendants("COMPANY").SingleOrDefault(x => string.Equals((string?)x.Attribute("NAME"), companyName, StringComparison.OrdinalIgnoreCase));
            if (company is null) return null;
            var gstin = InvoiceValue(company, "GSTREGISTRATIONNUMBER");
            return new InvoiceCompanyDto
            {
                Name = (string?)company.Attribute("NAME") ?? "", TallyGuid = InvoiceValue(company, "GUID"),
                Address = InvoiceAddress(company, "ADDRESS.LIST", "ADDRESS"), City = InvoiceValue(company, "CITY"),
                State = InvoiceValue(company, "STATENAME"), StateCode = InvoiceStateCode(InvoiceValue(company, "STATECODE"), gstin),
                Country = InvoiceValue(company, "COUNTRYNAME"), Pincode = InvoiceValue(company, "PINCODE"), Gstin = gstin,
                Email = InvoiceValue(company, "EMAIL"), Phone = InvoiceValue(company, "PHONENUMBER"),
                Mobile = InvoiceAddress(company, "MOBILENUMBERS.LIST", "MOBILENUMBERS")
            };
        }

        private static string InvoiceValue(XElement parent, string tag) => parent.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(tag, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
        private static string FirstInvoiceValue(XElement parent, params string[] tags) => FirstNonEmpty(tags.Select(tag => InvoiceValue(parent, tag)).ToArray());
        private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
        private static string InvoiceAddress(XElement parent, string list, string tag) => string.Join("\n", parent.Elements().Where(x => x.Name.LocalName.Equals(list, StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Elements()).Where(x => x.Name.LocalName.Equals(tag, StringComparison.OrdinalIgnoreCase)).Select(x => x.Value.Trim()).Where(x => x.Length > 0));
        private static string InvoiceStateCode(string explicitCode, string gstin) => !string.IsNullOrWhiteSpace(explicitCode) ? explicitCode : System.Text.RegularExpressions.Regex.IsMatch(gstin, @"^\d{2}[A-Z0-9]{13}$") ? gstin[..2] : "";
        private static List<VoucherPostingDto> ParsePostings(XElement voucher)
        {
            var entries = voucher.Elements().Where(x => x.Name.LocalName.Equals(
                "ALLLEDGERENTRIES.LIST", StringComparison.OrdinalIgnoreCase)).ToList();
            if (entries.Count == 0)
                entries = voucher.Elements().Where(x => x.Name.LocalName.Equals(
                    "LEDGERENTRIES.LIST", StringComparison.OrdinalIgnoreCase)).ToList();
            return entries.Select((entry, index) =>
            {
                var name = GetValue(entry, "LEDGERNAME");
                if (string.IsNullOrWhiteSpace(name) || !decimal.TryParse(
                        GetValue(entry, "AMOUNT"), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var amount))
                    throw new InvalidOperationException("Invalid voucher ledger posting; synchronization was stopped.");
                return new VoucherPostingDto { LedgerName = name, Amount = amount, EntryIndex = index };
            }).ToList();
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

            // IMPORTANT: send request to Tally as UTF-16
            var utf16 = Encoding.Unicode;

            var bytes = utf16.GetBytes(xmlRequest);

            using var content = new ByteArrayContent(bytes);

            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");

            content.Headers.ContentType.CharSet = "utf-16";

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
                "utf-16"
            );

            var response = await _httpClient.SendAsync(request);

            var responseBytes =
                await response.Content.ReadAsByteArrayAsync();

            response.EnsureSuccessStatusCode();

            // Detect UTF-16 response
            if (responseBytes.Length >= 2)
            {
                // UTF-16 LE BOM
                if (responseBytes[0] == 0xFF &&
                    responseBytes[1] == 0xFE)
                {
                    return Encoding.Unicode.GetString(responseBytes);
                }

                // UTF-16 BE BOM
                if (responseBytes[0] == 0xFE &&
                    responseBytes[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(responseBytes);
                }
            }

            // Otherwise Tally returned UTF-8
            return Encoding.UTF8.GetString(responseBytes);
        }


    }
}
