using TallyWebAPI.Services;
using System.Net;
using System.Text;

if (args.Contains("--live"))
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
    var result = await new LedgerReportService(client).GetLedgerReportAsync(
        "JAYA JOTHI MALIGAI", "Mahatma School", "20240401", "20241231");
    File.WriteAllText("tests/LedgerReport.Checks/live-report.json", System.Text.Json.JsonSerializer.Serialize(result));
    if (result.OpeningBalance != -1103283m || result.TotalDebit != 5960256m || result.TotalCredit != 5000m || result.ClosingBalance != -7058539m || result.Transactions.Count != 84)
        throw new Exception("Live reference totals/count do not reconcile.");
    var raw = File.ReadAllText("tests/LedgerReport.Checks/probe-response.xml");
    raw = System.Text.RegularExpressions.Regex.Replace(raw, @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);", "");
    var reference = System.Xml.Linq.XDocument.Parse(raw).Descendants("VOUCHER").Select(v => {
        var entries = v.Elements("ALLLEDGERENTRIES.LIST").ToList();
        if (entries.Count == 0) entries = v.Elements("LEDGERENTRIES.LIST").ToList();
        var movements = entries.Where(e => (string?)e.Element("LEDGERNAME") == "Mahatma School")
            .Select(e => decimal.Parse(e.Element("AMOUNT")!.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return new { Date = v.Element("DATE")!.Value, Number = v.Element("VOUCHERNUMBER")!.Value,
            Type = v.Element("VOUCHERTYPENAME")!.Value, Debit = -movements.Where(x => x < 0).Sum(), Credit = movements.Where(x => x > 0).Sum(),
            Particulars = entries.Select(e => e.Element("LEDGERNAME")!.Value).First(x => x != "Mahatma School") };
    }).OrderBy(x => x.Date).ThenBy(x => x.Number).ToList();
    if (reference.Count != result.Transactions.Count || reference.Where((r, i) => {
        var actual = result.Transactions[i];
        return r.Date != actual.Date || r.Number != actual.VoucherNumber || r.Type != actual.VoucherType ||
            r.Debit != actual.Debit || r.Credit != actual.Credit || r.Particulars != actual.Particulars;
    }).Any()) throw new Exception("Live report differs from independent Tally XML export.");
    Console.WriteLine("LIVE: all 84 rows match independent Tally XML by date, number, type, particulars, debit and credit; all four reference balances match.");
    return 0;
}

const decimal openingDr = -1_103_283m;
const decimal debit = 5_960_256m;
const decimal credit = 5_000m;
const decimal expectedClosingDr = -7_058_539m;

var closing = LedgerBalanceMath.CalculateClosingBalance(openingDr, debit, credit);
if (closing != expectedClosingDr)
{
    Console.Error.WriteLine($"Expected signed closing {expectedClosingDr:N2}; got {closing:N2}.");
    return 1;
}

Console.WriteLine("Signed opening/debit/credit reconciliation passed: 11,03,283 Dr - 59,60,256 Dr + 5,000 Cr = 70,58,539 Dr.");
foreach (var (opening, dr, cr, expected) in new[] {
    (100m, 30m, 10m, 80m), (-100m, 0m, 150m, 50m),
    (0m, 0m, 0m, 0m), (-100m, 0m, 100m, 0m) })
    if (LedgerBalanceMath.CalculateClosingBalance(opening, dr, cr) != expected)
        throw new Exception("Signed balance arithmetic failed.");

using var fixtureClient = FixtureClient(new FixtureHandler());
var report = await new LedgerReportService(fixtureClient).GetLedgerReportAsync(
    "Test Company", "Test Ledger", "20240401", "20241231");
if (report.TotalDebit != 150m || report.TotalCredit != 20m || report.ClosingBalance != -230m)
    throw new Exception($"All selected-ledger postings must count: got debit={report.TotalDebit}, credit={report.TotalCredit}, closing={report.ClosingBalance}");
Console.WriteLine("Repeated selected-ledger postings and signed period totals passed.");
using var extractionClient = FixtureClient(new FixtureHandler());
var vouchers = await new VoucherService(extractionClient).GetVoucherListAsync("20240401", "20240430", "Test Company");
var postingProperty = typeof(TallyWebAPI.DTOs.VoucherDto).GetProperty("Postings");
if (postingProperty == null || ((System.Collections.ICollection)postingProperty.GetValue(vouchers.Single())!).Count != 4)
    throw new Exception("Voucher list extraction must retain all four ledger postings for persistence.");
Console.WriteLine("Voucher posting extraction passed.");
var target = new TallyWebAPI.Models.VoucherEntity { CompanyId = 1 };
var masters = new List<TallyWebAPI.Models.LedgerEntity> {
    new() { Id = 10, CompanyId = 1, Name = "Test Ledger", TallyGuid = "ledger-a" },
    new() { Id = 11, CompanyId = 1, Name = "Other Ledger", TallyGuid = "ledger-b" },
    new() { Id = 99, CompanyId = 2, Name = "Test Ledger", TallyGuid = "other-company" }
};
var planner = typeof(LedgerBalanceMath).Assembly.GetType("TallyWebAPI.Services.VoucherPostingBackfill");
if (planner == null) throw new Exception("Posting backfill must map four entries to company 1 and be idempotent.");
var apply = planner.GetMethod("Apply")!;
apply.Invoke(null, new object[] { target, vouchers.Single().Postings, masters });
apply.Invoke(null, new object[] { target, vouchers.Single().Postings, masters });
if (target.LedgerEntries.Count != 4 || target.LedgerEntries[0].LedgerId != 10 || target.LedgerEntries[3].LedgerId != 11)
    throw new Exception("Backfill duplicated postings or mixed companies.");
var before = target.LedgerEntries[0].Amount;
vouchers.Single().Postings[0].Amount = -999;
try {
    apply.Invoke(null, new object[] { target, vouchers.Single().Postings, masters });
    throw new Exception("Conflicting financial data must be rejected.");
} catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { }
if (target.LedgerEntries[0].Amount != before) throw new Exception("Existing posting was overwritten.");
Console.WriteLine("Company-scoped, idempotent, non-overwriting posting backfill passed.");
using var errorClient = FixtureClient(new FixtureHandler("<ENVELOPE><LINEERROR>Company is not loaded</LINEERROR></ENVELOPE>"));
try {
    await new LedgerReportService(errorClient).GetLedgerReportAsync("Test Company", "Test Ledger", "20240401", "20241231");
    Console.Error.WriteLine("FAIL: Tally export errors must not appear as an empty successful report.");
    return 1;
} catch (InvalidOperationException) { }
return 0;

// Fixture transport is fully intercepted; no live endpoint is contacted.
static HttpClient FixtureClient(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://fixture.invalid/") };

sealed class FixtureHandler(string? overrideResponse = null) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var xml = await request.Content!.ReadAsStringAsync(token);
        if (xml.Contains("<ID>LedgerReportCollection</ID>") && !xml.Contains("<FILTER>HasSelectedLedger</FILTER>"))
            throw new InvalidOperationException("Ledger export must filter vouchers at Tally before transferring all ledger entries.");
        var response = xml.Contains("<ID>LedgerBalanceCollection</ID>")
            ? "<ENVELOPE><LEDGER NAME=\"Test Ledger\"><OPENINGBALANCE>-100</OPENINGBALANCE><CLOSINGBALANCE>-230</CLOSINGBALANCE></LEDGER></ENVELOPE>"
            : """
              <ENVELOPE><VOUCHER><DATE>20240401</DATE><VOUCHERNUMBER>1</VOUCHERNUMBER><VOUCHERTYPENAME>Journal</VOUCHERTYPENAME>
              <ALLLEDGERENTRIES.LIST><LEDGERNAME>Test Ledger</LEDGERNAME><AMOUNT>-100</AMOUNT></ALLLEDGERENTRIES.LIST>
              <ALLLEDGERENTRIES.LIST><LEDGERNAME>Test Ledger</LEDGERNAME><AMOUNT>-50</AMOUNT></ALLLEDGERENTRIES.LIST>
              <ALLLEDGERENTRIES.LIST><LEDGERNAME>Test Ledger</LEDGERNAME><AMOUNT>20</AMOUNT></ALLLEDGERENTRIES.LIST>
              <ALLLEDGERENTRIES.LIST><LEDGERNAME>Other Ledger</LEDGERNAME><AMOUNT>130</AMOUNT></ALLLEDGERENTRIES.LIST>
              </VOUCHER></ENVELOPE>
              """;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(overrideResponse ?? response, Encoding.UTF8, "text/xml") };
    }
}
