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
    public class VoucherSyncWorker
    {
        private readonly ILogger<VoucherSyncWorker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public VoucherSyncWorker(
            ILogger<VoucherSyncWorker> logger,
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
            _logger.LogInformation("Voucher sync started.");

            var companies =
                await GetCompaniesAsync(cancellationToken);

            if (companies.Count == 0)
            {
                _logger.LogWarning(
                    "No companies found in Tally for Voucher sync.");

                return;
            }

            var allVouchers =
                new List<AgentVoucherDto>();

            foreach (var company in companies)
            {
                if (string.IsNullOrWhiteSpace(company.TallyGuid) ||
                    string.IsNullOrWhiteSpace(company.Name) ||
                    string.IsNullOrWhiteSpace(company.StartingFrom))
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

                try
                {
                    var vouchers =
                        await GetVouchersAsync(
                            company,
                            financialYearStart,
                            financialYearEnd,
                            cancellationToken);

                    allVouchers.AddRange(vouchers);

                    _logger.LogInformation(
                        "Fetched {Count} voucher(s) from Tally company {CompanyName}.",
                        vouchers.Count,
                        company.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Voucher fetch failed for company {CompanyName}.",
                        company.Name);
                }
            }

            if (allVouchers.Count == 0)
            {
                _logger.LogInformation(
                    "No Vouchers found to push.");

                return;
            }

            // Safety: remove duplicate voucher GUIDs
            // within the same company before sending.
            allVouchers =
                allVouchers
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x.CompanyTallyGuid) &&
                        !string.IsNullOrWhiteSpace(
                            x.TallyGuid))
                    .GroupBy(
                        x => $"{x.CompanyTallyGuid}|{x.TallyGuid}",
                        StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();

            await PushToServerAsync(
                allVouchers,
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
        // GET VOUCHERS - MONTH BY MONTH
        // =========================================================

        private async Task<List<AgentVoucherDto>>
            GetVouchersAsync(
                TallyCompanyInfo company,
                DateTime startDate,
                DateTime endDate,
                CancellationToken cancellationToken)
        {
            var result =
                new List<AgentVoucherDto>();

            var currentStart = startDate;

            while (currentStart <= endDate)
            {
                var currentEnd =
                    new DateTime(
                        currentStart.Year,
                        currentStart.Month,
                        DateTime.DaysInMonth(
                            currentStart.Year,
                            currentStart.Month));

                if (currentEnd > endDate)
                {
                    currentEnd = endDate;
                }

                var fromDate =
                    currentStart.ToString(
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture);

                var toDate =
                    currentEnd.ToString(
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture);

                var monthlyVouchers =
                    await GetVoucherMonthAsync(
                        company,
                        fromDate,
                        toDate,
                        cancellationToken);

                result.AddRange(monthlyVouchers);

                _logger.LogInformation(
                    "Voucher batch {CompanyName}: {FromDate} - {ToDate}, {Count} voucher(s).",
                    company.Name,
                    fromDate,
                    toDate,
                    monthlyVouchers.Count);

                currentStart =
                    currentEnd.AddDays(1);
            }

            return result
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.TallyGuid))
                .GroupBy(
                    x => x.TallyGuid,
                    StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.VoucherDate)
                .ThenBy(x => x.VoucherNumber)
                .ToList();
        }

        // =========================================================
        // GET ONE MONTH OF VOUCHERS
        // =========================================================

        private async Task<List<AgentVoucherDto>>
            GetVoucherMonthAsync(
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
                        <ID>VoucherCollection</ID>
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

            var xml =
                await PostToTallyAsync(
                    xmlRequest,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(xml))
            {
                return new List<AgentVoucherDto>();
            }

            xml = CleanXml(xml);

            var document =
                XDocument.Parse(xml);

            var result =
                new List<AgentVoucherDto>();

            var vouchers =
                document
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "VOUCHER",
                            StringComparison.OrdinalIgnoreCase));

            foreach (var voucher in vouchers)
            {
                var date =
                    GetValue(
                        voucher,
                        "DATE");

                var voucherNumber =
                    GetValue(
                        voucher,
                        "VOUCHERNUMBER");

                var voucherType =
                    GetValue(
                        voucher,
                        "VOUCHERTYPENAME");

                // Ignore placeholder/non-voucher rows.
                if (string.IsNullOrWhiteSpace(date) &&
                    string.IsNullOrWhiteSpace(voucherNumber) &&
                    string.IsNullOrWhiteSpace(voucherType))
                {
                    continue;
                }

                var guid =
                    GetValue(
                        voucher,
                        "GUID");

                // Server duplicate protection requires GUID.
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                result.Add(
                    new AgentVoucherDto
                    {
                        CompanyTallyGuid =
                            company.TallyGuid,

                        TallyGuid =
                            guid,

                        VoucherNumber =
                            voucherNumber,

                        VoucherType =
                            voucherType,

                        VoucherDate =
                            date,

                        PartyName =
                            GetValue(
                                voucher,
                                "PARTYLEDGERNAME"),

                        PartyGstin =
                            GetValue(
                                voucher,
                                "PARTYGSTIN"),

                        State =
                            GetValue(
                                voucher,
                                "STATENAME"),

                        PlaceOfSupply =
                            GetValue(
                                voucher,
                                "PLACEOFSUPPLY"),

                        Amount =
                            GetVoucherAmount(
                                voucher),

                        Narration =
                            GetValue(
                                voucher,
                                "NARRATION")
                    });
            }

            return result;
        }

        // =========================================================
        // VOUCHER AMOUNT
        // =========================================================

        private static decimal?
            GetVoucherAmount(
                XElement voucher)
        {
            var partyLedgerName =
                GetValue(
                    voucher,
                    "PARTYLEDGERNAME");

            var ledgerEntries =
                voucher
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "ALLLEDGERENTRIES.LIST",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        x.Name.LocalName.Equals(
                            "LEDGERENTRIES.LIST",
                            StringComparison.OrdinalIgnoreCase));

            // First preference:
            // amount belonging to PARTYLEDGERNAME.
            foreach (var entry in ledgerEntries)
            {
                var ledgerName =
                    GetValue(
                        entry,
                        "LEDGERNAME");

                if (!string.IsNullOrWhiteSpace(
                        partyLedgerName) &&
                    ledgerName.Equals(
                        partyLedgerName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var amount =
                        ParseAmount(
                            GetValue(
                                entry,
                                "AMOUNT"));

                    if (amount.HasValue)
                    {
                        return Math.Abs(
                            amount.Value);
                    }
                }
            }

            // Fallback:
            // first available ledger amount.
            foreach (var entry in ledgerEntries)
            {
                var amount =
                    ParseAmount(
                        GetValue(
                            entry,
                            "AMOUNT"));

                if (amount.HasValue)
                {
                    return Math.Abs(
                        amount.Value);
                }
            }

            return null;
        }

        private static decimal?
            ParseAmount(
                string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
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
                return null;
            }

            if (!decimal.TryParse(
                    match.Value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                return null;
            }

            amount =
                Math.Abs(amount);

            return negative
                ? -amount
                : amount;
        }

        // =========================================================
        // PUSH TO LIVE SERVER
        // =========================================================

        private async Task PushToServerAsync(
            List<AgentVoucherDto> vouchers,
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
                $"{baseUrl.TrimEnd('/')}/agent-sync/vouchers";

            using var client =
                new HttpClient
                {
                    Timeout =
                        TimeSpan.FromMinutes(3)
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
                    new AgentVoucherSyncRequest
                    {
                        Vouchers =
                            vouchers
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
                    $"Voucher server sync failed. HTTP {(int)response.StatusCode}: {responseText}");
            }

            _logger.LogInformation(
                "Voucher sync completed. Server response: {Response}",
                responseText);
        }

        // =========================================================
        // POST XML TO LOCAL TALLY - UTF-16 FOR TAMIL
        // =========================================================

        private async Task<string> PostToTallyAsync(
            string xmlRequest,
            CancellationToken cancellationToken)
        {
            var client =
                _httpClientFactory
                    .CreateClient("Tally");

            // Tamil/multilingual Tally data:
            // send request as UTF-16.
            var requestBytes =
                Encoding.Unicode.GetBytes(
                    xmlRequest);

            using var content =
                new ByteArrayContent(
                    requestBytes);

            content.Headers.ContentType =
                new System.Net.Http.Headers
                    .MediaTypeHeaderValue(
                        "text/xml");

            content.Headers.ContentType.CharSet =
                "utf-16";

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

        private class AgentVoucherSyncRequest
        {
            public List<AgentVoucherDto> Vouchers
            {
                get;
                set;
            } = new();
        }

        private class AgentVoucherDto
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

            public string? VoucherNumber
            {
                get;
                set;
            }

            public string? VoucherType
            {
                get;
                set;
            }

            public string? VoucherDate
            {
                get;
                set;
            }

            public string? PartyName
            {
                get;
                set;
            }

            public string? PartyGstin
            {
                get;
                set;
            }

            public string? State
            {
                get;
                set;
            }

            public string? PlaceOfSupply
            {
                get;
                set;
            }

            public decimal? Amount
            {
                get;
                set;
            }

            public string? Narration
            {
                get;
                set;
            }
        }
    }
}