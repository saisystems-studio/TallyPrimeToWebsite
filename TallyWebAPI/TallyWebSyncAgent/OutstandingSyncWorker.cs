using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TallyWebSyncAgent
{
    public class OutstandingSyncWorker
    {
        private readonly ILogger<OutstandingSyncWorker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public OutstandingSyncWorker(
            ILogger<OutstandingSyncWorker> logger,
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
                "Outstanding sync started.");

            var companies =
                await GetCompaniesAsync(
                    cancellationToken);

            var allOutstandings =
                new List<AgentOutstandingDto>();

            foreach (var company in companies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(
                        company.TallyGuid) ||
                    string.IsNullOrWhiteSpace(
                        company.Name))
                {
                    continue;
                }

                if (!DateTime.TryParseExact(
                        company.StartingFrom,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var fromDate))
                {
                    _logger.LogWarning(
                        "Skipping Outstanding sync for {Company}. " +
                        "Invalid StartingFrom: {StartingFrom}",
                        company.Name,
                        company.StartingFrom);

                    continue;
                }

                var toDate = DateTime.Today;

                var companyRows =
                    await GetOutstandingAsync(
                        company,
                        fromDate,
                        toDate,
                        cancellationToken);

                allOutstandings.AddRange(
                    companyRows);

                _logger.LogInformation(
                    "Fetched {Count} outstanding allocation(s) " +
                    "from Tally company {Company}.",
                    companyRows.Count,
                    company.Name);
            }

            if (allOutstandings.Count == 0)
            {
                _logger.LogInformation(
                    "No outstanding allocations found.");

                return;
            }

            // Prevent accidental duplicate logical rows
            // inside one Agent cycle.
            var distinctRows =
                allOutstandings
                    .GroupBy(
                        x => BuildOutstandingKey(
                            x.CompanyTallyGuid,
                            x.TallyGuid,
                            x.LedgerName,
                            x.BillReference,
                            x.BillType),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();

            await PushToServerAsync(
                distinctRows,
                cancellationToken);

            _logger.LogInformation(
                "Outstanding sync completed. " +
                "{Count} allocation(s) sent successfully.",
                distinctRows.Count);
        }

        // =========================================================
        // COMPANIES
        // =========================================================
        private async Task<List<TallyCompanyDto>>
            GetCompaniesAsync(
                CancellationToken cancellationToken)
        {
            var xmlRequest = """
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>AgentOutstandingCompanyCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="AgentOutstandingCompanyCollection">
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
                return new();
            }

            xml = CleanXml(xml);

            var document =
                XDocument.Parse(xml);

            var result =
                new List<TallyCompanyDto>();

            foreach (var company in
                document
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "COMPANY",
                            StringComparison.OrdinalIgnoreCase)))
            {
                var name =
                    company.Attribute("NAME")
                        ?.Value
                        ?.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    name =
                        GetDirectValue(
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
                    new TallyCompanyDto
                    {
                        Name = name,
                        TallyGuid = guid,
                        StartingFrom = startingFrom
                    });
            }

            return result;
        }

        // =========================================================
        // FINAL OUTSTANDING
        // =========================================================
        private async Task<List<AgentOutstandingDto>>
            GetOutstandingAsync(
                TallyCompanyDto company,
                DateTime fromDate,
                DateTime toDate,
                CancellationToken cancellationToken)
        {
            var result =
                new List<AgentOutstandingDto>();

            // -----------------------------------------------------
            // A. OPENING BILL ALLOCATIONS
            // -----------------------------------------------------
            var ledgerXml =
                await GetOutstandingLedgerRawAsync(
                    company.Name,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(
                    ledgerXml))
            {
                ledgerXml =
                    CleanXml(ledgerXml);

                var ledgerDocument =
                    XDocument.Parse(ledgerXml);

                var ledgers =
                    ledgerDocument
                        .Descendants()
                        .Where(x =>
                            x.Name.LocalName.Equals(
                                "LEDGER",
                                StringComparison.OrdinalIgnoreCase));

                foreach (var ledger in ledgers)
                {
                    var ledgerName =
                        GetLedgerName(ledger);

                    if (string.IsNullOrWhiteSpace(
                            ledgerName))
                    {
                        continue;
                    }

                    var isBillWise =
                        GetDirectValue(
                            ledger,
                            "ISBILLWISEON");

                    if (!isBillWise.Equals(
                            "Yes",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var openingBills =
                        ledger
                            .Elements()
                            .Where(x =>
                                x.Name.LocalName.Equals(
                                    "BILLALLOCATIONS.LIST",
                                    StringComparison.OrdinalIgnoreCase));

                    foreach (var bill in openingBills)
                    {
                        var billReference =
                            GetDirectValue(
                                bill,
                                "NAME");

                        if (string.IsNullOrWhiteSpace(
                                billReference))
                        {
                            continue;
                        }

                        var amountText =
                            GetDirectValue(
                                bill,
                                "OPENINGBALANCE")
                                .Replace(",", "")
                                .Trim();

                        if (!decimal.TryParse(
                                amountText,
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var amount))
                        {
                            continue;
                        }

                        if (amount == 0)
                        {
                            continue;
                        }

                        var billDate =
                            GetDirectValue(
                                bill,
                                "BILLDATE");

                        var creditPeriod =
                            GetDirectValue(
                                bill,
                                "BILLCREDITPERIOD");

                        // Same synthetic identity used by
                        // the existing backend service.
                        var openingKey =
                            $"OPENING|{ledgerName}|{billReference}";

                        result.Add(
                            new AgentOutstandingDto
                            {
                                CompanyTallyGuid =
                                    company.TallyGuid,

                                LedgerName =
                                    ledgerName,

                                TallyGuid =
                                    openingKey,

                                VoucherNumber =
                                    null,

                                VoucherType =
                                    "Opening Balance",

                                VoucherDate =
                                    billDate,

                                BillReference =
                                    billReference,

                                BillType =
                                    "Opening Balance",

                                BillDate =
                                    billDate,

                                CreditPeriod =
                                    creditPeriod,

                                Amount =
                                    amount
                            });
                    }
                }
            }

            // -----------------------------------------------------
            // B. VOUCHER BILL ALLOCATIONS
            // Fetch month-by-month so large companies do not
            // create one massive Tally XML response.
            // -----------------------------------------------------
            var currentMonth =
                new DateTime(
                    fromDate.Year,
                    fromDate.Month,
                    1);

            while (currentMonth <= toDate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var monthStart =
                    currentMonth < fromDate
                        ? fromDate
                        : currentMonth;

                var monthEnd =
                    currentMonth
                        .AddMonths(1)
                        .AddDays(-1);

                if (monthEnd > toDate)
                {
                    monthEnd = toDate;
                }

                var voucherRows =
                    await GetVoucherOutstandingAsync(
                        company,
                        monthStart,
                        monthEnd,
                        cancellationToken);

                result.AddRange(
                    voucherRows);

                _logger.LogInformation(
                    "Outstanding voucher batch {Company}: " +
                    "{FromDate} - {ToDate}, {Count} allocation(s).",
                    company.Name,
                    monthStart.ToString("yyyyMMdd"),
                    monthEnd.ToString("yyyyMMdd"),
                    voucherRows.Count);

                currentMonth =
                    currentMonth.AddMonths(1);
            }

            return result;
        }

        // =========================================================
        // LEDGER / OPENING BILL XML
        // =========================================================
        private async Task<string>
            GetOutstandingLedgerRawAsync(
                string companyName,
                CancellationToken cancellationToken)
        {
            var safeCompanyName =
                SecurityElement.Escape(
                    companyName) ?? "";

            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>AgentOutstandingLedgerCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="AgentOutstandingLedgerCollection">
                                    <TYPE>Ledger</TYPE>

                                    <FETCH>Name</FETCH>
                                    <FETCH>Parent</FETCH>
                                    <FETCH>OpeningBalance</FETCH>
                                    <FETCH>ClosingBalance</FETCH>
                                    <FETCH>IsBillWiseOn</FETCH>
                                    <FETCH>BillAllocations.*</FETCH>
                                </COLLECTION>
                            </TDLMESSAGE>
                        </TDL>
                    </DESC>
                </BODY>
            </ENVELOPE>
            """;

            return await PostToTallyAsync(
                xmlRequest,
                cancellationToken);
        }

        // =========================================================
        // MONTHLY VOUCHER OUTSTANDING
        // =========================================================
        private async Task<List<AgentOutstandingDto>>
            GetVoucherOutstandingAsync(
                TallyCompanyDto company,
                DateTime fromDate,
                DateTime toDate,
                CancellationToken cancellationToken)
        {
            var safeCompanyName =
                SecurityElement.Escape(
                    company.Name) ?? "";

            var from =
                fromDate.ToString(
                    "yyyyMMdd");

            var to =
                toDate.ToString(
                    "yyyyMMdd");

            var xmlRequest = $"""
            <ENVELOPE>
                <HEADER>
                    <VERSION>1</VERSION>
                    <TALLYREQUEST>Export</TALLYREQUEST>
                    <TYPE>Collection</TYPE>
                    <ID>AgentOutstandingVoucherCollection</ID>
                </HEADER>

                <BODY>
                    <DESC>
                        <STATICVARIABLES>
                            <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
                            <SVCURRENTCOMPANY>{safeCompanyName}</SVCURRENTCOMPANY>
                            <SVFROMDATE TYPE="Date">{from}</SVFROMDATE>
                            <SVTODATE TYPE="Date">{to}</SVTODATE>
                        </STATICVARIABLES>

                        <TDL>
                            <TDLMESSAGE>
                                <COLLECTION NAME="AgentOutstandingVoucherCollection">
                                    <TYPE>Voucher</TYPE>

                                    <FETCH>Date</FETCH>
                                    <FETCH>VoucherNumber</FETCH>
                                    <FETCH>VoucherTypeName</FETCH>
                                    <FETCH>PartyLedgerName</FETCH>
                                    <FETCH>GUID</FETCH>

                                    <FETCH>AllLedgerEntries.*</FETCH>
                                    <FETCH>AllLedgerEntries.BillAllocations.*</FETCH>
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

            var result =
                new List<AgentOutstandingDto>();

            if (string.IsNullOrWhiteSpace(xml))
            {
                return result;
            }

            xml = CleanXml(xml);

            var document =
                XDocument.Parse(xml);

            var vouchers =
                document
                    .Descendants()
                    .Where(x =>
                        x.Name.LocalName.Equals(
                            "VOUCHER",
                            StringComparison.OrdinalIgnoreCase));

            foreach (var voucher in vouchers)
            {
                var tallyGuid =
                    GetValue(
                        voucher,
                        "GUID");

                if (string.IsNullOrWhiteSpace(
                        tallyGuid))
                {
                    continue;
                }

                var voucherNumber =
                    GetValue(
                        voucher,
                        "VOUCHERNUMBER");

                var voucherType =
                    GetValue(
                        voucher,
                        "VOUCHERTYPENAME");

                var voucherDate =
                    GetValue(
                        voucher,
                        "DATE");

                var partyName =
                    GetValue(
                        voucher,
                        "PARTYLEDGERNAME");

                if (string.IsNullOrWhiteSpace(
                        partyName))
                {
                    continue;
                }

                var partyEntries =
                    voucher
                        .Elements()
                        .Where(x =>
                            x.Name.LocalName.Equals(
                                "ALLLEDGERENTRIES.LIST",
                                StringComparison.OrdinalIgnoreCase))
                        .Where(x =>
                            GetDirectValue(
                                    x,
                                    "LEDGERNAME")
                                .Equals(
                                    partyName,
                                    StringComparison.OrdinalIgnoreCase));

                foreach (var partyEntry in
                    partyEntries)
                {
                    var billAllocations =
                        partyEntry
                            .Elements()
                            .Where(x =>
                                x.Name.LocalName.Equals(
                                    "BILLALLOCATIONS.LIST",
                                    StringComparison.OrdinalIgnoreCase));

                    foreach (var bill in
                        billAllocations)
                    {
                        var billType =
                            GetDirectValue(
                                bill,
                                "BILLTYPE");

                        var billReference =
                            GetDirectValue(
                                bill,
                                "NAME");

                        if (string.IsNullOrWhiteSpace(
                                billReference))
                        {
                            if (billType.Equals(
                                    "On Account",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                billReference =
                                    $"ONACCOUNT|{tallyGuid}";
                            }
                            else
                            {
                                continue;
                            }
                        }

                        var billDate =
                            GetDirectValue(
                                bill,
                                "BILLDATE");

                        if (string.IsNullOrWhiteSpace(
                                billDate))
                        {
                            billDate =
                                voucherDate;
                        }

                        var creditPeriod =
                            GetDirectValue(
                                bill,
                                "BILLCREDITPERIOD");

                        var amountText =
                            GetDirectValue(
                                bill,
                                "AMOUNT")
                                .Replace(",", "")
                                .Trim();

                        if (!decimal.TryParse(
                                amountText,
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var amount))
                        {
                            continue;
                        }

                        if (amount == 0)
                        {
                            continue;
                        }

                        result.Add(
                            new AgentOutstandingDto
                            {
                                CompanyTallyGuid =
                                    company.TallyGuid,

                                LedgerName =
                                    partyName,

                                TallyGuid =
                                    tallyGuid,

                                VoucherNumber =
                                    voucherNumber,

                                VoucherType =
                                    voucherType,

                                VoucherDate =
                                    voucherDate,

                                BillReference =
                                    billReference,

                                BillType =
                                    billType,

                                BillDate =
                                    billDate,

                                CreditPeriod =
                                    creditPeriod,

                                // IMPORTANT:
                                // Do not Math.Abs().
                                // Outstanding calculation needs
                                // Tally debit/credit sign.
                                Amount =
                                    amount
                            });
                    }
                }
            }

            return result;
        }

        // =========================================================
        // TALLY POST - UTF-16 FOR TAMIL
        // =========================================================
        private async Task<string>
            PostToTallyAsync(
                string xmlRequest,
                CancellationToken cancellationToken)
        {
            var client =
                _httpClientFactory.CreateClient(
                    "Tally");

            var requestBytes =
                Encoding.Unicode.GetBytes(
                    xmlRequest);

            using var content =
                new ByteArrayContent(
                    requestBytes);

            content.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "text/xml");

            content.Headers.ContentType.CharSet =
                "utf-16";

            using var response =
                await client.PostAsync(
                    "",
                    content,
                    cancellationToken);

            var responseText =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Tally Outstanding request failed. " +
                    $"HTTP {(int)response.StatusCode}: " +
                    responseText);
            }

            return responseText;
        }

        // =========================================================
        // SERVER PUSH - BATCHED
        // =========================================================
        private async Task PushToServerAsync(
            List<AgentOutstandingDto> outstandings,
            CancellationToken cancellationToken)
        {
            var baseUrl =
                _configuration[
                    "ServerApi:BaseUrl"];

            var agentKey =
                _configuration[
                    "ServerApi:AgentKey"];

            if (string.IsNullOrWhiteSpace(
                    baseUrl))
            {
                throw new InvalidOperationException(
                    "ServerApi:BaseUrl is missing.");
            }

            if (string.IsNullOrWhiteSpace(
                    agentKey))
            {
                throw new InvalidOperationException(
                    "ServerApi:AgentKey is missing.");
            }

            var endpoint =
                $"{baseUrl.TrimEnd('/')}/agent-sync/outstandings";

            // Same safe batching approach used
            // for Voucher sync.
            const int batchSize = 250;

            var totalBatches =
                (int)Math.Ceiling(
                    outstandings.Count /
                    (double)batchSize);

            _logger.LogInformation(
                "Sending {Count} outstanding allocation(s) " +
                "to server in {BatchCount} batch(es).",
                outstandings.Count,
                totalBatches);

            for (var i = 0;
                 i < outstandings.Count;
                 i += batchSize)
            {
                var batch =
                    outstandings
                        .Skip(i)
                        .Take(batchSize)
                        .ToList();

                var batchNumber =
                    (i / batchSize) + 1;

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
                        new AgentOutstandingSyncRequest
                        {
                            Outstandings =
                                batch
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
                        $"Outstanding server sync batch " +
                        $"{batchNumber}/{totalBatches} failed. " +
                        $"HTTP {(int)response.StatusCode}: " +
                        responseText);
                }

                _logger.LogInformation(
                    "Outstanding batch {BatchNumber}/{TotalBatches} " +
                    "synced. {Count} allocation(s). " +
                    "Server response: {Response}",
                    batchNumber,
                    totalBatches,
                    batch.Count,
                    responseText);
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================
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
                    "$1",
                    RegexOptions.IgnoreCase);

            return xml;
        }

        private static string GetLedgerName(
            XElement ledger)
        {
            var name =
                ledger.Attribute("NAME")
                    ?.Value
                    ?.Trim();

            if (!string.IsNullOrWhiteSpace(
                    name))
            {
                return name;
            }

            return GetDirectValue(
                ledger,
                "NAME");
        }

        private static string GetValue(
            XElement parent,
            string name)
        {
            return parent
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim() ?? "";
        }

        private static string GetDirectValue(
            XElement parent,
            string name)
        {
            return parent
                .Elements()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim() ?? "";
        }

        private static string BuildOutstandingKey(
            string companyGuid,
            string tallyGuid,
            string ledgerName,
            string billReference,
            string? billType)
        {
            return string.Join(
                "|",
                companyGuid?.Trim() ?? "",
                tallyGuid?.Trim() ?? "",
                ledgerName?.Trim() ?? "",
                billReference?.Trim() ?? "",
                billType?.Trim() ?? "");
        }

        // =========================================================
        // INTERNAL DTOs
        // =========================================================
        private sealed class TallyCompanyDto
        {
            public string Name { get; set; } = "";
            public string TallyGuid { get; set; } = "";
            public string StartingFrom { get; set; } = "";
        }

        private sealed class AgentOutstandingDto
        {
            public string CompanyTallyGuid { get; set; } = "";
            public string LedgerName { get; set; } = "";
            public string TallyGuid { get; set; } = "";
            public string? VoucherNumber { get; set; }
            public string? VoucherType { get; set; }
            public string? VoucherDate { get; set; }
            public string BillReference { get; set; } = "";
            public string? BillType { get; set; }
            public string? BillDate { get; set; }
            public string? CreditPeriod { get; set; }
            public decimal Amount { get; set; }
        }

        private sealed class AgentOutstandingSyncRequest
        {
            public List<AgentOutstandingDto> Outstandings { get; set; }
                = new();
        }
    }
}