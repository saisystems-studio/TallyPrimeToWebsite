using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TallyWebSyncAgent;

public class LedgerSyncWorker
{
    private readonly ILogger<LedgerSyncWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public LedgerSyncWorker(
        ILogger<LedgerSyncWorker> logger,
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
        _logger.LogInformation("Ledger sync started.");

        var companies =
            await GetCompaniesAsync(cancellationToken);

        var allLedgers = new List<AgentLedgerDto>();

        foreach (var company in companies)
        {
            if (string.IsNullOrWhiteSpace(company.TallyGuid) ||
                string.IsNullOrWhiteSpace(company.Name))
            {
                continue;
            }

            try
            {
                var ledgers =
                    await GetLedgersAsync(
                        company,
                        cancellationToken);

                allLedgers.AddRange(ledgers);

                _logger.LogInformation(
                    "{Count} ledger(s) fetched for {Company}.",
                    ledgers.Count,
                    company.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ledger fetch failed for {Company}.",
                    company.Name);
            }
        }

        if (allLedgers.Count == 0)
        {
            _logger.LogWarning(
                "No ledgers received from Tally.");

            return;
        }

        await PushToServerAsync(
            allLedgers,
            cancellationToken);
    }

    private async Task<List<TallyCompany>>
        GetCompaniesAsync(
            CancellationToken cancellationToken)
    {
        const string xmlRequest = """
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
                                <FETCH>GUID</FETCH>
                                <FETCH>Name</FETCH>
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

        var document =
            XDocument.Parse(CleanXml(xml));

        return document
            .Descendants()
            .Where(x =>
                x.Name.LocalName.Equals(
                    "COMPANY",
                    StringComparison.OrdinalIgnoreCase)
                && x.Attribute("NAME") != null)
            .Select(x => new TallyCompany
            {
                TallyGuid = GetValue(x, "GUID"),

                Name =
                    x.Attribute("NAME")
                        ?.Value
                        ?.Trim()
                    ?? GetValue(x, "NAME")
            })
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.TallyGuid) &&
                !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
    }

    private async Task<List<AgentLedgerDto>>
        GetLedgersAsync(
            TallyCompany company,
            CancellationToken cancellationToken)
    {
        var safeCompany =
            System.Security.SecurityElement
                .Escape(company.Name) ?? "";

        var xmlRequest = $"""
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
                        <SVCURRENTCOMPANY>{safeCompany}</SVCURRENTCOMPANY>
                    </STATICVARIABLES>

                    <TDL>
                        <TDLMESSAGE>
                            <COLLECTION NAME="LedgerCollection">
                                <TYPE>Ledger</TYPE>

                                <FETCH>Name</FETCH>
                                <FETCH>Parent</FETCH>
                                <FETCH>Alias</FETCH>

                                <FETCH>GUID</FETCH>
                                <FETCH>MasterID</FETCH>
                                <FETCH>AlterID</FETCH>

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

        var xml =
            await PostToTallyAsync(
                xmlRequest,
                cancellationToken);

        var document =
            XDocument.Parse(CleanXml(xml));

        var ledgerElements =
            document
                .Descendants()
                .Where(x =>
                    x.Name.LocalName.Equals(
                        "LEDGER",
                        StringComparison.OrdinalIgnoreCase)
                    && x.Attribute("NAME") != null);

        var result = new List<AgentLedgerDto>();

        foreach (var ledger in ledgerElements)
        {
            var guid = GetValue(ledger, "GUID");

            if (string.IsNullOrWhiteSpace(guid))
                continue;

            var gstDetails = ledger
                .Elements()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        "LEDGSTREGDETAILS.LIST",
                        StringComparison.OrdinalIgnoreCase));

            result.Add(new AgentLedgerDto
            {
                CompanyTallyGuid = company.TallyGuid,

                TallyGuid = guid,

                MasterId =
                    long.TryParse(
                        GetValue(ledger, "MASTERID"),
                        out var masterId)
                        ? masterId
                        : null,

                AlterId =
                    long.TryParse(
                        GetValue(ledger, "ALTERID"),
                        out var alterId)
                        ? alterId
                        : null,

                Name =
                    ledger.Attribute("NAME")
                        ?.Value
                        ?.Trim()
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
                    GetValue(
                        ledger,
                        "LEDGERSTATENAME"),

                Country =
                    GetValue(
                        ledger,
                        "COUNTRYNAME"),

                Pincode =
                    GetValue(ledger, "PINCODE"),

                Pan =
                    GetValue(
                        ledger,
                        "INCOMETAXNUMBER"),

                Gstin =
                    GetValue(
                        gstDetails,
                        "GSTIN"),

                RegistrationType =
                    GetValue(
                        gstDetails,
                        "GSTREGISTRATIONTYPE"),

                BillByBill =
                    GetValue(
                        ledger,
                        "ISBILLWISEON"),

                CreditPeriod =
                    GetValue(
                        ledger,
                        "BILLCREDITPERIOD"),

                OpeningBalance =
                    GetValue(
                        ledger,
                        "OPENINGBALANCE"),

                ClosingBalance =
                    GetValue(
                        ledger,
                        "CLOSINGBALANCE")
            });
        }

        return result;
    }

    private async Task<string> PostToTallyAsync(
        string xmlRequest,
        CancellationToken cancellationToken)
    {
        var tallyClient =
            _httpClientFactory.CreateClient("Tally");

        using var content =
            new StringContent(
                xmlRequest,
                Encoding.UTF8,
                "application/xml");

        using var response =
            await tallyClient.PostAsync(
                "",
                content,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadAsStringAsync(cancellationToken);
    }

    private async Task PushToServerAsync(
        List<AgentLedgerDto> ledgers,
        CancellationToken cancellationToken)
    {
        var baseUrl =
            _configuration["ServerApi:BaseUrl"];

        var agentKey =
            _configuration["ServerApi:AgentKey"];

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
            new Uri(
                new Uri(
                    baseUrl.TrimEnd('/') + "/"),
                "agent-sync/ledgers");

        var client =
            _httpClientFactory.CreateClient();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                endpoint);

        request.Headers.Add(
            "X-Agent-Key",
            agentKey);

        request.Content =
            JsonContent.Create(
                new AgentLedgerSyncRequest
                {
                    Ledgers = ledgers
                });

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        var responseBody =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Server rejected ledger sync. " +
                $"HTTP {(int)response.StatusCode}: " +
                responseBody);
        }

        _logger.LogInformation(
            "Ledger sync completed. Server response: {Response}",
            responseBody);
    }

    private static string CleanXml(string xml)
    {
        return Regex.Replace(
            xml,
            @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
            "");
    }

    private static string GetValue(
        XElement? element,
        string name)
    {
        if (element == null)
            return "";

        return element
            .Descendants()
            .FirstOrDefault(x =>
                x.Name.LocalName.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim() ?? "";
    }

    private sealed class TallyCompany
    {
        public string TallyGuid { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class AgentLedgerSyncRequest
    {
        public List<AgentLedgerDto> Ledgers { get; set; } = new();
    }

    private sealed class AgentLedgerDto
    {
        public string CompanyTallyGuid { get; set; } = "";
        public string TallyGuid { get; set; } = "";

        public long? MasterId { get; set; }
        public long? AlterId { get; set; }

        public string Name { get; set; } = "";
        public string? Parent { get; set; }
        public string? Alias { get; set; }
        public string? MailingName { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? Pan { get; set; }
        public string? Gstin { get; set; }
        public string? RegistrationType { get; set; }
        public string? CreditPeriod { get; set; }
        public string? BillByBill { get; set; }
        public string? OpeningBalance { get; set; }
        public string? ClosingBalance { get; set; }
    }
}