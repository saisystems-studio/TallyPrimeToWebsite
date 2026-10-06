using System.Globalization;
using System.Net.Http.Json;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TallyWebSyncAgent
{
    public class StockItemSyncWorker
    {
        private readonly ILogger<StockItemSyncWorker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public StockItemSyncWorker(
            ILogger<StockItemSyncWorker> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task SyncAsync(
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Stock Item sync started.");

            var companies =
                await GetCompaniesAsync(cancellationToken);

            if (companies.Count == 0)
            {
                _logger.LogWarning(
                    "No companies found in Tally for Stock Item sync.");

                return;
            }

            var allStockItems =
                new List<AgentStockItemDto>();

            foreach (var company in companies)
            {
                if (string.IsNullOrWhiteSpace(
                        company.TallyGuid) ||
                    string.IsNullOrWhiteSpace(
                        company.Name) ||
                    string.IsNullOrWhiteSpace(
                        company.StartingFrom))
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
                    _logger.LogWarning(
                        "Invalid StartingFrom for company {CompanyName}: {StartingFrom}",
                        company.Name,
                        company.StartingFrom);

                    continue;
                }

                var financialYearEnd =
                    financialYearStart
                        .AddYears(1)
                        .AddDays(-1);

                var fromDate =
                    financialYearStart.ToString(
                        "yyyyMMdd");

                var toDate =
                    financialYearEnd.ToString(
                        "yyyyMMdd");

                try
                {
                    var stockItems =
                        await GetStockItemsAsync(
                            company,
                            fromDate,
                            toDate,
                            cancellationToken);

                    allStockItems.AddRange(
                        stockItems);

                    _logger.LogInformation(
                        "Fetched {Count} stock item(s) from Tally company {CompanyName}.",
                        stockItems.Count,
                        company.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Stock Item fetch failed for company {CompanyName}.",
                        company.Name);
                }
            }

            if (allStockItems.Count == 0)
            {
                _logger.LogInformation(
                    "No Stock Items found to push.");

                return;
            }

            await PushToServerAsync(
                allStockItems,
                cancellationToken);
        }

        // =========================================================
        // GET COMPANIES FROM TALLY
        // =========================================================

        private async Task<List<TallyCompanyInfo>>
            GetCompaniesAsync(
                CancellationToken cancellationToken)
        {
            var xmlRequest = """
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
                                        <FETCH>Name</FETCH>
                                        <FETCH>GUID</FETCH>
                                        <FETCH>StartingFrom</FETCH>
                                    </COLLECTION>
                                </TDLMESSAGE>
                            </TDL>
                        </DESC>
                    </BODY>
                </ENVELOPE>
                """;

            var xml =
                await PostToTallyAsync(
                    xmlRequest,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(xml))
            {
                return new List<TallyCompanyInfo>();
            }

            xml = CleanXml(xml);

            var document =
                XDocument.Parse(xml);

            var result =
                new List<TallyCompanyInfo>();

            var companies =
                document
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "COMPANY",
                            StringComparison.OrdinalIgnoreCase));

            foreach (var company in companies)
            {
                var name =
                    company.Attribute("NAME")
                        ?.Value
                        ?.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    name =
                        GetValue(
                            company,
                            "NAME");
                }

                var guid =
                    GetValue(
                        company,
                        "GUID");

                var startingFrom =
                    GetValue(
                        company,
                        "STARTINGFROM");

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                result.Add(
                    new TallyCompanyInfo
                    {
                        TallyGuid = guid,
                        Name = name,
                        StartingFrom = startingFrom
                    });
            }

            return result;
        }

        // =========================================================
        // GET STOCK ITEMS FROM TALLY
        // =========================================================

        private async Task<List<AgentStockItemDto>>
            GetStockItemsAsync(
                TallyCompanyInfo company,
                string fromDate,
                string toDate,
                CancellationToken cancellationToken)
        {
            var safeCompanyName =
                SecurityElement.Escape(
                    company.Name) ?? "";

            var xmlRequest = $"""
                <ENVELOPE>
                    <HEADER>
                        <VERSION>1</VERSION>
                        <TALLYREQUEST>Export</TALLYREQUEST>
                        <TYPE>Collection</TYPE>
                        <ID>StockSummaryCollection</ID>
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
                                    <COLLECTION NAME="StockSummaryCollection">
                                        <TYPE>StockItem</TYPE>

                                        <FETCH>Name</FETCH>
                                        <FETCH>Parent</FETCH>
                                        <FETCH>BaseUnits</FETCH>

                                        <FETCH>OpeningBalance</FETCH>
                                        <FETCH>ClosingBalance</FETCH>

                                        <FETCH>GUID</FETCH>
                                        <FETCH>MasterID</FETCH>
                                        <FETCH>AlterID</FETCH>

                                        <FETCH>OpeningValue</FETCH>
                                        <FETCH>ClosingValue</FETCH>
                                    </COLLECTION>
                                </TDLMESSAGE>
                            </TDL>
                        </DESC>
                    </BODY>
                </ENVELOPE>
                """;

            var xml =
                await PostToTallyAsync(
                    xmlRequest,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(xml))
            {
                return new List<AgentStockItemDto>();
            }

            xml = CleanXml(xml);

            var document =
                XDocument.Parse(xml);

            var result =
                new List<AgentStockItemDto>();

            var tallyItems =
                document
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "STOCKITEM",
                            StringComparison.OrdinalIgnoreCase));

            foreach (var tallyItem in tallyItems)
            {
                var name =
                    tallyItem.Attribute("NAME")
                        ?.Value
                        ?.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    name =
                        GetValue(
                            tallyItem,
                            "NAME");
                }

                var guid =
                    GetValue(
                        tallyItem,
                        "GUID");

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                var stockGroup =
                    GetValue(
                        tallyItem,
                        "PARENT");

                var unit =
                    GetValue(
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

                result.Add(
                    new AgentStockItemDto
                    {
                        CompanyTallyGuid =
                            company.TallyGuid,

                        TallyGuid = guid,

                        MasterId = masterId,
                        AlterId = alterId,

                        Name = name,
                        StockGroup = stockGroup,
                        Unit = unit,

                        OpeningQuantity =
                            openingQuantity,

                        ClosingQuantity =
                            closingQuantity
                    });
            }

            return result;
        }

        // =========================================================
        // PUSH TO LIVE SERVER
        // =========================================================

        private async Task PushToServerAsync(
            List<AgentStockItemDto> stockItems,
            CancellationToken cancellationToken)
        {
            var baseUrl =
                _configuration[
                    "ServerApi:BaseUrl"];

            var agentKey =
                _configuration[
                    "ServerApi:AgentKey"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    "ServerApi:BaseUrl is missing.");
            }

            if (string.IsNullOrWhiteSpace(agentKey))
            {
                throw new InvalidOperationException(
                    "ServerApi:AgentKey is missing.");
            }

            var endpoint =
                $"{baseUrl.TrimEnd('/')}/agent-sync/stock-items";

            using var client =
                new HttpClient
                {
                    Timeout =
                        TimeSpan.FromSeconds(60)
                };

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    endpoint);

            request.Headers.Add(
                "X-Agent-Key",
                agentKey);

            request.Content =
                JsonContent.Create(
                    new AgentStockItemSyncRequest
                    {
                        StockItems =
                            stockItems
                    });

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            var responseText =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Stock Item server sync failed. HTTP {(int)response.StatusCode}: {responseText}");
            }

            _logger.LogInformation(
                "Stock Item sync completed. Server response: {Response}",
                responseText);
        }

        // =========================================================
        // POST XML TO LOCAL TALLY
        // =========================================================

        private async Task<string> PostToTallyAsync(
            string xmlRequest,
            CancellationToken cancellationToken)
        {
            var client =
                _httpClientFactory
                    .CreateClient("Tally");

            using var content =
                new StringContent(
                    xmlRequest,
                    Encoding.UTF8,
                    "application/xml");

            using var response =
                await client.PostAsync(
                    "",
                    content,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadAsStringAsync(
                    cancellationToken);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string GetValue(
            XElement? parent,
            string elementName)
        {
            if (parent == null)
            {
                return "";
            }

            return parent
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        elementName,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim() ?? "";
        }

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

        private static decimal ParseQuantity(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var text =
                value
                    .Trim()
                    .Replace(",", "");

            var negative =
                text.Contains("(-)") ||
                text.StartsWith("-");

            text =
                text.Replace(
                    "(-)",
                    "");

            var match =
                Regex.Match(
                    text,
                    @"[-+]?\d+(?:\.\d+)?");

            if (!match.Success)
            {
                return 0;
            }

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

        private static string CleanXml(
            string xml)
        {
            xml =
                Regex.Replace(
                    xml,
                    @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                    "");

            xml =
                Regex.Replace(
                    xml,
                    @"(<\/?)UDF:",
                    "$1");

            return xml;
        }

        // =========================================================
        // INTERNAL MODELS
        // =========================================================

        private class TallyCompanyInfo
        {
            public string TallyGuid { get; set; } = "";

            public string Name { get; set; } = "";

            public string StartingFrom { get; set; } = "";
        }

        private class AgentStockItemSyncRequest
        {
            public List<AgentStockItemDto> StockItems
            {
                get;
                set;
            } = new();
        }

        private class AgentStockItemDto
        {
            public string CompanyTallyGuid
            {
                get;
                set;
            } = "";

            public string TallyGuid
            {
                get;
                set;
            } = "";

            public long? MasterId
            {
                get;
                set;
            }

            public long? AlterId
            {
                get;
                set;
            }

            public string Name
            {
                get;
                set;
            } = "";

            public string StockGroup
            {
                get;
                set;
            } = "";

            public string Unit
            {
                get;
                set;
            } = "";

            public decimal OpeningQuantity
            {
                get;
                set;
            }

            public decimal ClosingQuantity
            {
                get;
                set;
            }
        }
    }
}