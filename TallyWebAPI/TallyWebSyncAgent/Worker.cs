using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TallyWebSyncAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly LedgerSyncWorker _ledgerSyncWorker;
    private readonly StockItemSyncWorker _stockItemSyncWorker;
    private readonly VoucherSyncWorker _voucherSyncWorker;
    private readonly OutstandingSyncWorker _outstandingSyncWorker;

    public Worker(
        ILogger<Worker> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        LedgerSyncWorker ledgerSyncWorker,
        StockItemSyncWorker stockItemSyncWorker,
        VoucherSyncWorker voucherSyncWorker,
        OutstandingSyncWorker outstandingSyncWorker)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _ledgerSyncWorker = ledgerSyncWorker;
        _stockItemSyncWorker = stockItemSyncWorker;
        _voucherSyncWorker = voucherSyncWorker;
        _outstandingSyncWorker = outstandingSyncWorker;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Tally Web Sync Agent started.");

        // Give Tally / network a few seconds after startup.
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncCompaniesAsync(stoppingToken);
                await _ledgerSyncWorker.SyncAsync(stoppingToken);
                await _stockItemSyncWorker.SyncAsync(stoppingToken);
                await _voucherSyncWorker.SyncAsync(stoppingToken);
                await _outstandingSyncWorker.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Company sync cycle failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncCompaniesAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Company sync started.");

        var companies =
            await GetCompaniesFromTallyAsync(
                cancellationToken);

        if (companies.Count == 0)
        {
            _logger.LogWarning(
                "No companies received from Tally.");

            return;
        }

        // Same behavior as existing CompanySyncService:
        // fetch GST registration separately for each company.
        foreach (var company in companies)
        {
            try
            {
                var gst =
                    await GetGstDetailsAsync(
                        company.Name,
                        cancellationToken);

                company.Gstin = gst.Gstin;
                company.GstRegistrationType =
                    gst.GstRegistrationType;
            }
            catch (Exception ex)
            {
                // GST failure must not stop company sync.
                _logger.LogWarning(
                    ex,
                    "GST details could not be read for company {CompanyName}.",
                    company.Name);

                company.Gstin = "";
                company.GstRegistrationType = "";
            }
        }

        await PushCompaniesToServerAsync(
            companies,
            cancellationToken);
    }

    private async Task<List<AgentCompanyDto>>
        GetCompaniesFromTallyAsync(
            CancellationToken cancellationToken)
    {
        var tallyClient =
            _httpClientFactory.CreateClient("Tally");

        var xmlRequest = BuildCompanyRequest();

        using var content = new StringContent(
            xmlRequest,
            Encoding.UTF8,
            "application/xml");

        using var response =
            await tallyClient.PostAsync(
                "",
                content,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var xml =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(xml))
        {
            return new List<AgentCompanyDto>();
        }

        xml = CleanTallyXml(xml);

        var document = XDocument.Parse(xml);

        var companies = document
            .Descendants()
            .Where(x =>
                x.Name.LocalName.Equals(
                    "COMPANY",
                    StringComparison.OrdinalIgnoreCase)
                &&
                x.Attribute("NAME") != null)
            .Select(x => new AgentCompanyDto
            {
                TallyGuid =
                    GetValue(x, "GUID"),

                Name =
                    x.Attribute("NAME")
                        ?.Value
                        ?.Trim()
                    ?? GetValue(x, "NAME"),

                FormalName =
                    GetValue(
                        x,
                        "BASICCOMPANYFORMALNAME"),

                State =
                    GetValue(x, "STATENAME"),

                Country =
                    GetValue(x, "COUNTRYNAME"),

                Pincode =
                    GetValue(x, "PINCODE"),

                Email =
                    GetValue(x, "EMAIL"),

                Phone =
                    GetValue(x, "PHONENUMBER"),

                StartingFrom =
                    GetValue(x, "STARTINGFROM"),

                BooksFrom =
                    GetValue(x, "BOOKSFROM")
            })
            .Where(x =>
                !string.IsNullOrWhiteSpace(
                    x.TallyGuid))
            .ToList();

        _logger.LogInformation(
            "Fetched {Count} company(s) from Tally.",
            companies.Count);

        return companies;
    }

    private async Task<GstDetails>
        GetGstDetailsAsync(
            string companyName,
            CancellationToken cancellationToken)
    {
        var tallyClient =
            _httpClientFactory.CreateClient("Tally");

        var xmlRequest =
            BuildGstRegistrationRequest(companyName);

        using var content = new StringContent(
            xmlRequest,
            Encoding.UTF8,
            "application/xml");

        using var response =
            await tallyClient.PostAsync(
                "",
                content,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var xml =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(xml))
        {
            return new GstDetails();
        }

        xml = CleanTallyXml(xml);

        var document = XDocument.Parse(xml);

        var gstTaxUnit = document
            .Descendants()
            .FirstOrDefault(x =>
                x.Name.LocalName.Equals(
                    "TAXUNIT",
                    StringComparison.OrdinalIgnoreCase)
                &&
                (
                    string.Equals(
                        x.Attribute("TAXTYPE")?.Value,
                        "GST",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    !string.IsNullOrWhiteSpace(
                        x.Attribute(
                            "TAXREGISTRATION")
                            ?.Value)
                ));

        if (gstTaxUnit == null)
        {
            return new GstDetails();
        }

        var gstin =
            gstTaxUnit
                .Attribute("TAXREGISTRATION")
                ?.Value
                ?.Trim()
            ?? "";

        if (string.IsNullOrWhiteSpace(gstin))
        {
            gstin =
                GetValue(
                    gstTaxUnit,
                    "GSTREGNUMBER");
        }

        var gstRegistrationType = "";

        var registrationDetails =
            gstTaxUnit
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        "GSTREGISTRATIONDETAILS.LIST",
                        StringComparison.OrdinalIgnoreCase));

        if (registrationDetails != null)
        {
            gstRegistrationType =
                GetValue(
                    registrationDetails,
                    "REGISTRATIONTYPE");
        }

        if (string.IsNullOrWhiteSpace(
            gstRegistrationType))
        {
            gstRegistrationType =
                GetValue(
                    gstTaxUnit,
                    "GSTREGISTRATIONTYPE");
        }

        return new GstDetails
        {
            Gstin = gstin,
            GstRegistrationType =
                gstRegistrationType
        };
    }

    private async Task PushCompaniesToServerAsync(
        List<AgentCompanyDto> companies,
        CancellationToken cancellationToken)
    {
        var agentKey =
            _configuration["ServerApi:AgentKey"];

        if (string.IsNullOrWhiteSpace(agentKey))
        {
            throw new InvalidOperationException(
                "ServerApi:AgentKey is missing in appsettings.json.");
        }

        var serverClient =
            _httpClientFactory.CreateClient();

        var baseUrl =
            _configuration["ServerApi:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "ServerApi:BaseUrl is missing in appsettings.json.");
        }

        baseUrl = baseUrl.TrimEnd('/') + "/";

        var endpoint =
            new Uri(
                new Uri(baseUrl),
                "agent-sync/companies");

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                endpoint);

        request.Headers.Add(
            "X-Agent-Key",
            agentKey);

        request.Content =
            JsonContent.Create(
                new AgentCompanySyncRequest
                {
                    Companies = companies
                });

        using var response =
            await serverClient.SendAsync(
                request,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Server rejected company sync. " +
                $"HTTP {(int)response.StatusCode}: " +
                responseBody);
        }

        _logger.LogInformation(
            "Company sync completed. Server response: {Response}",
            responseBody);
    }

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

    private static string
        BuildGstRegistrationRequest(
            string companyName)
    {
        var safeCompanyName =
            System.Security.SecurityElement
                .Escape(companyName)
            ?? "";

        return $"""
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
                        <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
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
    }

    private static string CleanTallyXml(
        string xml)
    {
        return Regex.Replace(
            xml,
            @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
            "");
    }

    private static string GetValue(
        XElement element,
        string name)
    {
        return element
            .Descendants()
            .FirstOrDefault(x =>
                x.Name.LocalName.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim()
            ?? "";
    }

    private sealed class GstDetails
    {
        public string Gstin { get; set; } = "";

        public string GstRegistrationType
        {
            get;
            set;
        } = "";
    }

    private sealed class AgentCompanySyncRequest
    {
        public List<AgentCompanyDto> Companies
        {
            get;
            set;
        } = new();
    }

    private sealed class AgentCompanyDto
    {
        public string TallyGuid { get; set; } = "";

        public string Name { get; set; } = "";

        public string? FormalName { get; set; }

        public string? State { get; set; }

        public string? Country { get; set; }

        public string? Pincode { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Gstin { get; set; }

        public string? GstRegistrationType
        {
            get;
            set;
        }

        public string? StartingFrom { get; set; }

        public string? BooksFrom { get; set; }
    }
}