using System.Text;
using System.Xml.Linq;
using TallyWebAPI.DTOs;

namespace TallyWebAPI.Services
{
    public class TallyService
    {
        private readonly HttpClient _httpClient;

        public TallyService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // =========================================================
        // COMPANY RAW DATA
        // =========================================================
        public async Task<string> GetCompaniesAsync()
        {
            var xmlRequest = BuildCompanyRequest();

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


        // =========================================================
        // GST REGISTRATION RAW DATA
        // Temporary method - keep for now
        // =========================================================
        public async Task<string> GetGstRegistrationsAsync()
        {
            var xmlRequest = """
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>GSTRegistrationCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>PMP Rice Mill</SVCURRENTCOMPANY>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="GSTRegistrationCollection">
                                    <TYPE>TaxUnit</TYPE>
                                    <FETCH>*</FETCH>
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


        // =========================================================
        // LEDGERS RAW DATA
        // =========================================================
        public async Task<string> GetLedgersAsync()
        {
            var xmlRequest = """
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>LedgerCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="LedgerCollection">
                                    <TYPE>Ledger</TYPE>

                                    <FETCH>Name</FETCH>
                                    <FETCH>Parent</FETCH>
                                    <FETCH>Alias</FETCH>

                                    <FETCH>MailingName</FETCH>
                                    <FETCH>Address</FETCH>
                                    <FETCH>StateName</FETCH>
                                    <FETCH>CountryName</FETCH>
                                    <FETCH>PinCode</FETCH>

                                    <FETCH>LedgerStateName</FETCH>
                                    <FETCH>LEDGSTREGDETAILS.*</FETCH>
                                    <FETCH>LEDMAILINGDETAILS.*</FETCH>
                                    <FETCH>IncomeTaxNumber</FETCH>
                                    <FETCH>PartyIncomeTaxNumber</FETCH>

                                    <FETCH>IsBillWiseOn</FETCH>
                                    <FETCH>BillCreditPeriod</FETCH>

                                    <FETCH>OpeningBalance</FETCH>
                                    <FETCH>ClosingBalance</FETCH>
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

        // =========================================================
        // STOCK ITEMS RAW DATA
        // =========================================================
        public async Task<string> GetStockItemsAsync()
        {
            var xmlRequest = """
    <ENVELOPE>
        <HEADER>
            <VERSION>1</VERSION>
            <TALLYREQUEST>Export</TALLYREQUEST>
            <TYPE>Collection</TYPE>
            <ID>StockItemCollection</ID>
        </HEADER>

        <BODY>
            <DESC>
                <STATICVARIABLES>
                    <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                </STATICVARIABLES>

                <TDL>
                    <TDLMESSAGE>
                        <COLLECTION NAME="StockItemCollection">
                            <TYPE>StockItem</TYPE>
                            <FETCH>*</FETCH>
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


        public async Task<List<LedgerDto>> GetLedgerListAsync()
        {
            var xml = await GetLedgersAsync();

            var ledgers = new List<LedgerDto>();

            if (string.IsNullOrWhiteSpace(xml))
                return ledgers;

            // Tally sometimes returns XML 1.0 invalid control character references
            // Example: &#4;
            // Remove them before parsing.
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            var document = XDocument.Parse(xml);

            var ledgerElements = document
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "LEDGER",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && x.Attribute("NAME") != null
                );

            foreach (var ledger in ledgerElements)
            {
                ledgers.Add(new LedgerDto
                {
                    Name =
                        ledger.Attribute("NAME")?.Value?.Trim()
                        ?? GetValue(ledger, "NAME"),

                    Parent =
                        GetValue(ledger, "PARENT"),

                    Alias =
                        GetValue(ledger, "ALIAS"),

                    MailingName =
                        GetValue(ledger, "MAILINGNAME"),

                    Address =
                        GetValue(ledger, "ADDRESS"),

                    State =
                        GetValue(ledger, "LEDGERSTATENAME"),

                    Country =
                        GetValue(ledger, "COUNTRYNAME"),

                    Pincode =
                        GetValue(ledger, "PINCODE"),

                    Pan =
                        GetValue(ledger, "INCOMETAXNUMBER"),

                    Gstin =
                        ledger
                            .Elements()
                            .FirstOrDefault(x =>
                                x.Name.LocalName.Equals(
                                    "LEDGSTREGDETAILS.LIST",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )?
                            .Elements()
                            .FirstOrDefault(x =>
                                x.Name.LocalName.Equals(
                                    "GSTIN",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )?.Value?.Trim() ?? "",

                    RegistrationType =
                        ledger
                            .Elements()
                            .FirstOrDefault(x =>
                                x.Name.LocalName.Equals(
                                    "LEDGSTREGDETAILS.LIST",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )?
                            .Elements()
                            .FirstOrDefault(x =>
                                x.Name.LocalName.Equals(
                                    "GSTREGISTRATIONTYPE",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )?.Value?.Trim() ?? "",

                    BillByBill =
                        GetValue(ledger, "ISBILLWISEON"),

                    CreditPeriod =
                        GetValue(ledger, "BILLCREDITPERIOD"),

                    OpeningBalance =
                        GetValue(ledger, "OPENINGBALANCE"),

                    ClosingBalance =
                        GetValue(ledger, "CLOSINGBALANCE")
                });
            }

            return ledgers;
        }


        // =========================================================
        // STOCK GROUPS RAW DATA
        // =========================================================
        public async Task<string> GetStockGroupsAsync()
        {
            var xmlRequest = """
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>StockGroupCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="StockGroupCollection">
                                    <TYPE>StockGroup</TYPE>
                                    <FETCH>*</FETCH>
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

        // =========================================================
        // CURRENT COMPANY
        // =========================================================
        public async Task<CompanyDetailsDto?> GetCurrentCompanyAsync()
        {
            var xml = await GetCompaniesAsync();

            if (string.IsNullOrWhiteSpace(xml))
                return null;

            var document = XDocument.Parse(xml);

            // CMPINFO <COMPANY> element should not be selected.
            // Select actual company master having NAME attribute.
            var company = document
                .Descendants("COMPANY")
                .FirstOrDefault(x => x.Attribute("NAME") != null);

            if (company == null)
                return null;

            return new CompanyDetailsDto
            {
                Name =
                    company.Attribute("NAME")?.Value?.Trim()
                    ?? company.Element("NAME")?.Value?.Trim()
                    ?? "",

                MailingName =
                    GetValue(company, "BASICCOMPANYFORMALNAME"),

                Address =
                    GetValue(company, "ADDRESS"),

                State =
                    GetValue(company, "STATENAME"),

                Country =
                    GetValue(company, "COUNTRYNAME"),

                Pincode =
                    GetValue(company, "PINCODE"),

                Telephone =
                    GetValue(company, "PHONENUMBER"),

                Mobile =
                    GetValue(company, "MOBILENUMBER"),

                Email =
                    GetValue(company, "EMAIL"),

                Website =
                    GetValue(company, "WEBSITE"),

                StartingFrom =
                    GetValue(company, "STARTINGFROM"),

                BooksFrom =
                    GetValue(company, "BOOKSFROM"),

                // Top-level Company XML currently returns this empty.
                // Do not hardcode GSTIN.
                Gstin =
                    GetValue(company, "GSTREGISTRATIONNUMBER"),

                GstRegistrationType =
                    GetValue(company, "GSTREGISTRATIONTYPE")
            };
        }


        // =========================================================
        // XML VALUE HELPER
        // =========================================================
        private static string GetValue(
            XElement? parent,
            string elementName)
        {
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


        // =========================================================
        // COMPANY XML REQUEST
        // =========================================================
        private static string BuildCompanyRequest()
        {
            return """
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>CompanyCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="CompanyCollection">
                                    <TYPE>Company</TYPE>
                                    <FETCH>*</FETCH>
                                </COLLECTION>
                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;
        }

        public async Task<List<StockItemDto>> GetStockItemListAsync()
        {
            var stockItemXml = await GetStockItemsAsync();
            var stockGroupXml = await GetStockGroupsAsync();

            // Tally invalid XML control characters remove
            stockItemXml = System.Text.RegularExpressions.Regex.Replace(
                stockItemXml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            stockGroupXml = System.Text.RegularExpressions.Regex.Replace(
                stockGroupXml,
                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                ""
            );

            var itemDocument = XDocument.Parse(stockItemXml);
            var groupDocument = XDocument.Parse(stockGroupXml);

            var groups = groupDocument
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "STOCKGROUP",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && x.Attribute("NAME") != null
                )
                .ToDictionary(
                    x => x.Attribute("NAME")!.Value.Trim(),
                    x => x,
                    StringComparer.OrdinalIgnoreCase
                );

            var result = new List<StockItemDto>();

            var items = itemDocument
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "STOCKITEM",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && x.Attribute("NAME") != null
                );

            foreach (var item in items)
            {
                var name = item.Attribute("NAME")?.Value?.Trim() ?? "";
                var stockGroup = GetValue(item, "PARENT");

                var itemHsn = item
                    .Elements()
                    .FirstOrDefault(x =>
                        x.Name.LocalName.Equals(
                            "HSNDETAILS.LIST",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                var hsnCode = GetValue(itemHsn, "HSNCODE");
                var hsnSource = GetValue(itemHsn, "SRCOFHSNDETAILS");

                // HSN not stored in item -> inherit from Stock Group
                if (string.IsNullOrWhiteSpace(hsnCode) &&
                    groups.TryGetValue(stockGroup, out var group))
                {
                    var groupHsn = group
                        .Elements()
                        .FirstOrDefault(x =>
                            x.Name.LocalName.Equals(
                                "HSNDETAILS.LIST",
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                    hsnCode = GetValue(groupHsn, "HSNCODE");

                    if (!string.IsNullOrWhiteSpace(hsnCode))
                        hsnSource = $"Stock Group: {stockGroup}";
                }

                result.Add(new StockItemDto
                {
                    Name = name,
                    StockGroup = stockGroup,
                    Unit = GetValue(item, "BASEUNITS"),

                    GstApplicable = GetValue(item, "GSTAPPLICABLE"),
                    TypeOfSupply = GetValue(item, "GSTTYPEOFSUPPLY"),

                    HsnCode = hsnCode,
                    HsnSource = hsnSource,

                    GstSource = GetValue(item, "SRCOFGSTDETAILS"),

                    OpeningBalance = GetValue(item, "OPENINGBALANCE"),
                    OpeningRate = GetValue(item, "OPENINGRATE"),
                    OpeningValue = GetValue(item, "OPENINGVALUE")
                });
            }

            return result;
        }
    }
}