using System.Text;

namespace TallyWebSyncAgent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public Worker(
            ILogger<Worker> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Tally Web Sync Agent started."
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckTallyAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unable to connect to local Tally."
                    );
                }

                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    stoppingToken
                );
            }
        }

        private async Task CheckTallyAsync(
            CancellationToken stoppingToken)
        {
            var client =
                _httpClientFactory.CreateClient("Tally");

            const string xml = """
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
                                    </COLLECTION>
                                </TDLMESSAGE>
                            </TDL>
                        </DESC>
                    </BODY>
                </ENVELOPE>
                """;

            using var content = new StringContent(
                xml,
                Encoding.UTF8,
                "application/xml"
            );

            using var response =
                await client.PostAsync(
                    "",
                    content,
                    stoppingToken
                );

            var result =
                await response.Content
                    .ReadAsStringAsync(stoppingToken);

            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Tally connected successfully. Response length: {Length}",
                result.Length
            );
        }
    }
}