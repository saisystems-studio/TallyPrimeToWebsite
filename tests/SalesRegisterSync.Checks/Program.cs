using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Data;
using TallyWebAPI.Controllers;
using TallyWebAPI.Services;

const string fullAndLegacyXml = """
<ENVELOPE><BODY><DATA><COLLECTION>
<VOUCHER><DATE>20240401</DATE><GUID>fixture-guid-1</GUID><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><VOUCHERNUMBER>1</VOUCHERNUMBER><PARTYLEDGERNAME>Fixture Party</PARTYLEDGERNAME>
  <LEDGERENTRIES.LIST><LEDGERNAME>Fixture Party</LEDGERNAME><AMOUNT>-10.00</AMOUNT></LEDGERENTRIES.LIST>
  <ALLLEDGERENTRIES.LIST><LEDGERNAME>Fixture Party</LEDGERNAME><AMOUNT>-10.00</AMOUNT></ALLLEDGERENTRIES.LIST>
  <ALLLEDGERENTRIES.LIST><LEDGERNAME>Fixture Sales</LEDGERNAME><AMOUNT>10.00</AMOUNT></ALLLEDGERENTRIES.LIST>
</VOUCHER>
<VOUCHER><DATE>20240401</DATE><GUID>fixture-guid-1</GUID><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><VOUCHERNUMBER>1</VOUCHERNUMBER><PARTYLEDGERNAME>Fixture Party</PARTYLEDGERNAME>
  <LEDGERENTRIES.LIST><LEDGERNAME>Fixture Party</LEDGERNAME><AMOUNT>-10.00</AMOUNT></LEDGERENTRIES.LIST>
  <ALLLEDGERENTRIES.LIST><LEDGERNAME>Fixture Party</LEDGERNAME><AMOUNT>-10.00</AMOUNT></ALLLEDGERENTRIES.LIST>
  <ALLLEDGERENTRIES.LIST><LEDGERNAME>Fixture Sales</LEDGERNAME><AMOUNT>10.00</AMOUNT></ALLLEDGERENTRIES.LIST>
</VOUCHER>
</COLLECTION></DATA></BODY></ENVELOPE>
""";

var rawService = new VoucherService(new HttpClient(new FixedXmlHandler(fullAndLegacyXml))
{
    BaseAddress = new Uri("http://tally.test/")
});
var raw = await rawService.GetVoucherListAsync("20240401", "20240430", "Fixture Company",
    preserveDuplicateGuids: true);
Assert(raw.Count == 2, "Raw preview fetch must retain duplicate GUID rows for reporting.");
Assert(raw.All(x => x.Postings.Count == 2), "Full postings must be used once; party-only rows must not be appended.");
Assert(raw.All(x => x.Postings.Sum(p => p.Amount) == 0), "Full signed postings must balance.");
Assert(raw.All(x => x.Amount == "10.00"), "Voucher header amount must remain the absolute party amount.");

var legacyService = new VoucherService(new HttpClient(new FixedXmlHandler("""
<ENVELOPE><BODY><DATA><COLLECTION><VOUCHER><DATE>20240401</DATE><GUID>legacy-guid</GUID><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><VOUCHERNUMBER>2</VOUCHERNUMBER><PARTYLEDGERNAME>Fixture Party</PARTYLEDGERNAME>
<LEDGERENTRIES.LIST><LEDGERNAME>Fixture Party</LEDGERNAME><AMOUNT>-3.50</AMOUNT></LEDGERENTRIES.LIST>
</VOUCHER></COLLECTION></DATA></BODY></ENVELOPE>
""")) { BaseAddress = new Uri("http://tally.test/") });
var legacy = await legacyService.GetVoucherListAsync("20240401", "20240430", "Fixture Company");
Assert(legacy.Single().Postings.Single().Amount == -3.50m,
    "Legacy LEDGERENTRIES-only responses must remain supported.");

var compatibility = await rawService.GetVoucherListAsync("20240401", "20240430", "Fixture Company");
Assert(compatibility.Count == 1, "Existing callers must keep the legacy de-duplicated result by default.");
Console.WriteLine("PASS: complete signed postings, legacy fallback, duplicate reporting, and default de-duplication.");


// Regression: cancelled rows may have empty postings and share active identities.
var cancelledXml = fullAndLegacyXml.Replace("<VOUCHER><DATE>", "<VOUCHER><ISCANCELLED>No</ISCANCELLED><DATE>")
    .Replace("</COLLECTION>", "<VOUCHER><DATE>20240401</DATE><GUID>fixture-guid-1</GUID><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><VOUCHERNUMBER>1</VOUCHERNUMBER><ISCANCELLED TYPE=\"Logical\"> yEs </ISCANCELLED><ALLLEDGERENTRIES.LIST /></VOUCHER></COLLECTION>");
var cancellationHandler = new FixedXmlHandler(cancelledXml);
var cancellationService = new VoucherService(new HttpClient(cancellationHandler) { BaseAddress = new Uri("http://tally.test/") });
var cancelledRows = await cancellationService.GetSalesRegisterVouchersAsync("20240401", "20240430", "Fixture Company");
Assert(cancellationHandler.LastRequest.Contains("<FETCH>IsCancelled</FETCH>"), "Independent export must explicitly fetch Tally cancellation status.");
var cancellationJson = JsonSerializer.SerializeToElement(cancelledRows);
Assert(cancelledRows.Count == 3, "Cancelled identities and duplicate active GUIDs must be retained for diagnostics.");
Assert(cancellationJson.EnumerateArray().Count(v => v.GetProperty("IsCancelled").GetBoolean()) == 1, "Explicit mixed-case Yes cancellation flag must be detected.");
Assert(cancelledRows.Sum(v => v.Postings.Count) == 4, "Cancelled empty postings must not be parsed or counted.");
Console.WriteLine("PASS: cancellation-aware parsing retains identities and preserves active signed postings.");


var malformedActiveXml = "<ENVELOPE><VOUCHER><DATE>20240401</DATE><GUID>active-bad</GUID><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><VOUCHERNUMBER>2</VOUCHERNUMBER><ISCANCELLED>No</ISCANCELLED><ALLLEDGERENTRIES.LIST><LEDGERNAME>Party</LEDGERNAME><AMOUNT>bad</AMOUNT></ALLLEDGERENTRIES.LIST></VOUCHER><VOUCHER><DATE>20240401</DATE><GUID>unknown-status</GUID><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><VOUCHERNUMBER>3</VOUCHERNUMBER><ISCANCELLED>unexpected</ISCANCELLED></VOUCHER></ENVELOPE>";
var malformedService = new VoucherService(new HttpClient(new FixedXmlHandler(malformedActiveXml)) { BaseAddress = new Uri("http://tally.test/") });
var malformedRows = await malformedService.GetSalesRegisterVouchersAsync("20240401", "20240430", "Fixture Company");
Assert(malformedRows.Count == 2 && malformedRows.All(v => !v.IsCancelled && v.SourceValidationError is not null), "Malformed active postings and unknown statuses must remain invalid, never silently excluded as cancelled.");
Assert(cancelledRows.Where(v => !v.IsCancelled).GroupBy(v => v.Guid).Single().Count() == 2, "Active duplicate GUIDs remain visible to independent validation.");
Assert(cancelledRows.Single(v => v.IsCancelled).Amount == "" && cancelledRows.Single(v => v.IsCancelled).Postings.Count == 0, "Cancelled vouchers have identity diagnostics, not invented financial values.");
var legacyRejected = false;
try { await malformedService.GetVoucherListAsync("20240401", "20240430", "Fixture Company"); }
catch (InvalidOperationException) { legacyRejected = true; }
Assert(legacyRejected, "Unrelated legacy callers keep their existing malformed-posting rejection.");
Console.WriteLine("PASS: invalid active postings, unknown status, active duplicate retention and legacy behavior.");

if (args.Contains("--cancellation-live-preview")) await CancellationPreview.RunAsync();

if (args.Contains("--live-preview", StringComparer.OrdinalIgnoreCase))
    await RunLivePreviewAsync();
if (args.Contains("--tally-only-preview", StringComparer.OrdinalIgnoreCase))
    await RunTallyOnlyPreviewAsync();
if (args.Contains("--live-apply-approved", StringComparer.OrdinalIgnoreCase))
    await RunApprovedLiveApplyAsync();
if (args.Contains("--live-verify", StringComparer.OrdinalIgnoreCase))
    await RunLiveVoucherComparisonAsync();

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task RunLivePreviewAsync()
{
    const int companyId = 1;
    const string fromDate = "20240401";
    const string toDate = "20240430";
    const decimal expectedTotal = 4241929.10m;

    var settingsPath = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "TallyWebAPI", "TallyWebAPI", "appsettings.json"));
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    var root = settings.RootElement;
    var connectionString = root.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()
        ?? throw new InvalidOperationException("Configured SQL connection string is empty.");
    var tallyUrl = root.GetProperty("Tally").GetProperty("BaseUrl").GetString()
        ?? throw new InvalidOperationException("Configured Tally base URL is empty.");

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(connectionString)
        .Options;
    await using var db = new AppDbContext(options);
    using var tallyClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    using var voucherClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    var service = new SalesRegisterSyncService(db, new TallyService(tallyClient), new VoucherService(voucherClient));

    // Explicitly read-only: this harness calls preview only, never apply.
    var result = await service.PreviewOrApplyAsync(companyId, fromDate, toDate, apply: false);
    var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(resultJson);
    using var preview = JsonDocument.Parse(resultJson);
    var data = preview.RootElement;
    Assert(data.GetProperty("preview").GetBoolean(), "Live result must be preview mode.");
    Assert(!data.GetProperty("applied").GetBoolean(), "Live preview must not report an apply.");
    Assert(data.GetProperty("companyId").GetInt32() == companyId, "Preview selected the wrong SQL company.");
    Assert(data.GetProperty("fromDate").GetString() == fromDate && data.GetProperty("toDate").GetString() == toDate,
        "Preview selected the wrong date range.");
    Assert(data.GetProperty("salesVoucherCount").GetInt32() == 103, "Expected exactly 103 Sales vouchers.");
    Assert(data.GetProperty("headersToAdd").GetInt32() == 103, "Expected exactly 103 independent headers to add.");
    Assert(data.GetProperty("signedPostingsToAdd").GetInt32() == 250, "Expected exactly 250 independent signed postings.");
    Assert(data.GetProperty("absoluteSalesTotal").GetDecimal() == expectedTotal,
        "Sales total does not match the verified Tally amount.");
    Assert(data.GetProperty("distinctPostingLedgerNames").GetInt32() == 13 &&
        data.GetProperty("mappedPostingLedgerNames").GetInt32() == 13 &&
        data.GetProperty("distinctMappedPostingLedgerGuids").GetInt32() == 13 &&
        data.GetProperty("unmappedPostingLedgerNames").GetArrayLength() == 0,
        "Expected all 13 posting ledgers to have unique SQL GUID mappings.");
    Assert(data.GetProperty("safeToApply").GetBoolean(), "Preview did not approve applying the exact validated batch.");
    Assert(data.GetProperty("tablesExist").GetBoolean(), "Independent Sales Register tables were not found after approved creation.");
    Assert(data.GetProperty("existingSalesRegisterRowsInPeriod").GetInt32() == 0 &&
        data.GetProperty("unexpectedExistingSalesRegisterRows").GetInt32() == 0,
        "Expected no existing or unexpected independent Sales Register period rows.");
    Assert(data.GetProperty("validationErrors").GetArrayLength() == 0, "Preview returned validation errors.");
    Assert(data.GetProperty("duplicateSourceGuidRows").GetInt32() == 0 &&
        data.GetProperty("crossCompanyGuidMatches").GetInt32() == 0,
        "Preview found duplicate source or cross-company voucher GUIDs.");

    var remainingRows = await db.Vouchers.AsNoTracking().CountAsync(x => x.CompanyId == companyId &&
        x.VoucherDate != null && x.VoucherDate.CompareTo(fromDate) >= 0 && x.VoucherDate.CompareTo(toDate) <= 0);
    Assert(remainingRows == 0, "Existing shared-table SQL target period unexpectedly contains headers.");
    Console.WriteLine($"PASS: independent-table preview validated 103 headers, 250 postings and ₹{expectedTotal:N2}; shared-table target period remains at {remainingRows} rows.");
}

static async Task RunApprovedLiveApplyAsync()
{
    const int companyId = 1;
    const string fromDate = "20240401", toDate = "20240430";
    const string expectedCompanyGuid = "5bd36bbb-820e-4784-b692-fe9891019a11";
    const decimal expectedTotal = 4241929.10m;
    var settingsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "TallyWebAPI", "TallyWebAPI", "appsettings.json"));
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    var root = settings.RootElement;
    var connectionString = root.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
    var tallyUrl = root.GetProperty("Tally").GetProperty("BaseUrl").GetString()!;
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
    await using var db = new AppDbContext(options);
    using var tallyClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    using var voucherClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    var sync = new SalesRegisterSyncService(db, new TallyService(tallyClient), new VoucherService(voucherClient));

    // Mandatory fresh preview. No apply call is reachable unless every guard below passes.
    var previewJson = JsonSerializer.Serialize(await sync.PreviewOrApplyAsync(companyId, fromDate, toDate, apply: false));
    using var preview = JsonDocument.Parse(previewJson);
    var p = preview.RootElement;
    Assert(p.GetProperty("safeToApply").GetBoolean() && p.GetProperty("validationErrors").GetArrayLength() == 0, "Fresh preview is not safe; apply stopped.");
    Assert(p.GetProperty("companyTallyGuid").GetString() == expectedCompanyGuid, "Company GUID changed; apply stopped.");
    Assert(p.GetProperty("salesVoucherCount").GetInt32() == 103 && p.GetProperty("headersToAdd").GetInt32() == 103, "Expected exactly 103 missing Sales headers; apply stopped.");
    Assert(p.GetProperty("signedPostingsToAdd").GetInt32() == 250 && p.GetProperty("absoluteSalesTotal").GetDecimal() == expectedTotal, "Expected exactly 250 postings and verified total; apply stopped.");
    Assert(p.GetProperty("duplicateSourceGuidRows").GetInt32() == 0 && p.GetProperty("crossCompanyGuidMatches").GetInt32() == 0 && p.GetProperty("unexpectedExistingSalesRegisterRows").GetInt32() == 0, "Duplicate, cross-company or unexpected rows found; apply stopped.");
    Assert(p.GetProperty("mappedPostingLedgerNames").GetInt32() == 13 && p.GetProperty("distinctMappedPostingLedgerGuids").GetInt32() == 13 && p.GetProperty("unmappedPostingLedgerNames").GetArrayLength() == 0, "Ledger mapping validation failed; apply stopped.");

    var applyJson = JsonSerializer.Serialize(await sync.PreviewOrApplyAsync(companyId, fromDate, toDate, apply: true));
    using var applied = JsonDocument.Parse(applyJson);
    var a = applied.RootElement;
    Assert(a.GetProperty("applied").GetBoolean() && a.GetProperty("headersToAdd").GetInt32() == 103 && a.GetProperty("signedPostingsToAdd").GetInt32() == 250, "Apply did not report the exact inserted batch.");

    var from = new DateTime(2024, 4, 1); var to = new DateTime(2024, 4, 30);
    var headers = await db.SalesRegister.AsNoTracking().Include(x => x.Entries)
        .Where(x => x.CompanyId == companyId && x.VoucherDate >= from && x.VoucherDate <= to)
        .OrderBy(x => x.VoucherDate).ThenBy(x => x.VoucherNumber).ToListAsync();
    var postingCount = headers.Sum(x => x.Entries.Count);
    var amountTotal = headers.Sum(x => x.Amount);
    Assert(headers.Count == 103 && postingCount == 250 && amountTotal == expectedTotal, "Post-apply SQL reconciliation failed.");
    Assert(headers.Select(x => x.TallyGuid).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 103, "Post-apply voucher GUID uniqueness check failed.");
    Assert(headers.All(x => x.CompanyId == companyId && x.Entries.Sum(e => e.SignedAmount) == 0m), "Post-apply company scope or balance validation failed.");
    Assert(headers.All(x => x.VoucherType.Equals("Sales", StringComparison.OrdinalIgnoreCase) || x.VoucherType.Equals("GST Sales", StringComparison.OrdinalIgnoreCase)), "Unexpected voucher type present in inserted period.");

    var reportController = new SalesRegisterController(db, sync);
    var drillAction = await reportController.Get(companyId, fromDate, toDate);
    var drillValue = (drillAction as OkObjectResult)?.Value ?? throw new InvalidOperationException("Sales Register April drill-down API did not return success.");
    using var drill = JsonDocument.Parse(JsonSerializer.Serialize(drillValue));
    Assert(drill.RootElement.GetProperty("totalRecords").GetInt32() == 103 && drill.RootElement.GetProperty("vouchers").GetArrayLength() == 103, "Sales Register API drill-down is incomplete.");
    var drillTotal = drill.RootElement.GetProperty("vouchers").EnumerateArray().Sum(x => x.GetProperty("amount").GetDecimal());
    Assert(drillTotal == expectedTotal, "Sales Register API drill-down total does not reconcile.");

    var otherCompanyHeaders = await db.SalesRegister.AsNoTracking().CountAsync(x => x.CompanyId != companyId);
    var otherVoucherTypes = await db.SalesRegister.AsNoTracking().CountAsync(x => x.CompanyId == companyId && x.VoucherDate >= from && x.VoucherDate <= to && x.VoucherType != "Sales" && x.VoucherType != "GST Sales");
    var sharedRows = await db.Vouchers.AsNoTracking().CountAsync(x => x.CompanyId == companyId && x.VoucherDate != null && x.VoucherDate.CompareTo(fromDate) >= 0 && x.VoucherDate.CompareTo(toDate) <= 0);
    Assert(otherCompanyHeaders == 0 && otherVoucherTypes == 0 && sharedRows == 0, "Isolation check failed: unrelated company/type or shared voucher tables have rows in target scope.");
    Console.WriteLine(JsonSerializer.Serialize(new { preview = p, apply = a, verifiedHeaders = headers.Count, verifiedPostings = postingCount, verifiedTotal = amountTotal, monthDrillDownVouchers = drill.RootElement.GetProperty("totalRecords").GetInt32(), monthDrillDownTotal = drillTotal, otherCompanyHeaders, otherVoucherTypes, sharedVoucherTableRows = sharedRows }, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task RunLiveVoucherComparisonAsync()
{
    const int companyId = 1;
    const string fromDate = "20240401", toDate = "20240430";
    const string expectedCompanyGuid = "5bd36bbb-820e-4784-b692-fe9891019a11";
    const decimal expectedTotal = 4241929.10m;
    var settingsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "TallyWebAPI", "TallyWebAPI", "appsettings.json"));
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    var root = settings.RootElement;
    var cs = root.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
    var tallyUrl = root.GetProperty("Tally").GetProperty("BaseUrl").GetString()!;
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    using var tallyClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    using var voucherClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    var sqlCompany = await db.Companies.AsNoTracking().SingleAsync(x => x.Id == companyId);
    Assert(string.Equals(sqlCompany.TallyGuid, expectedCompanyGuid, StringComparison.OrdinalIgnoreCase), "SQL company GUID mismatch.");
    var tally = new TallyService(tallyClient);
    var types = await tally.GetVoucherTypesAsync(sqlCompany.Name);
    var salesTypes = types.Where(x => string.Equals(x.Parent, "Sales", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var source = (await new VoucherService(voucherClient).GetVoucherListAsync(fromDate, toDate, sqlCompany.Name, preserveDuplicateGuids: true))
        .Where(x => salesTypes.Contains(x.VoucherType)).ToList();
    var stored = await db.SalesRegister.AsNoTracking().Include(x => x.Entries)
        .Where(x => x.CompanyId == companyId && x.VoucherDate >= new DateTime(2024, 4, 1) && x.VoucherDate <= new DateTime(2024, 4, 30)).ToListAsync();
    var ledgers = await db.Ledgers.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync();
    var ledgerMap = ledgers.Where(x => !string.IsNullOrWhiteSpace(x.TallyGuid)).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
    var storedByGuid = stored.ToDictionary(x => x.TallyGuid, StringComparer.OrdinalIgnoreCase);
    var problems = new List<string>();
    foreach (var t in source)
    {
        if (!storedByGuid.TryGetValue(t.Guid, out var s)) { problems.Add($"Missing SQL voucher GUID {t.Guid}"); continue; }
        if (s.VoucherNumber != t.VoucherNumber || s.VoucherDate.ToString("yyyyMMdd") != t.Date || s.VoucherType != t.VoucherType || s.PartyLedgerName != t.PartyName || s.Amount != decimal.Parse(t.Amount, System.Globalization.CultureInfo.InvariantCulture))
            problems.Add($"Header mismatch for voucher {t.VoucherNumber} ({t.Guid})");
        if (!ledgerMap.TryGetValue(t.PartyName, out var partyLedger) || s.PartyLedgerGuid != partyLedger.TallyGuid)
            problems.Add($"Party GUID mismatch for voucher {t.VoucherNumber} ({t.Guid})");
        var ordered = t.Postings.OrderBy(x => x.EntryIndex).ToList();
        var sqlEntries = s.Entries.OrderBy(x => x.EntrySequence).ToList();
        if (ordered.Count != sqlEntries.Count) { problems.Add($"Posting count mismatch for voucher {t.VoucherNumber} ({t.Guid})"); continue; }
        for (var i = 0; i < ordered.Count; i++)
        {
            var posting = ordered[i]; var entry = sqlEntries[i];
            if (!ledgerMap.TryGetValue(posting.LedgerName, out var ledger) || entry.EntrySequence != posting.EntryIndex || entry.LedgerName != posting.LedgerName || entry.LedgerGuid != ledger.TallyGuid || entry.SignedAmount != posting.Amount)
                problems.Add($"Posting mismatch for voucher {t.VoucherNumber}, sequence {posting.EntryIndex}");
        }
    }
    if (stored.Count != source.Count) problems.Add($"Tally/SQL voucher count differs: {source.Count}/{stored.Count}");
    var sqlTotal = stored.Sum(x => x.Amount);
    if (sqlTotal != expectedTotal) problems.Add($"SQL total mismatch: {sqlTotal}");
    Console.WriteLine(JsonSerializer.Serialize(new { tallyVoucherCount = source.Count, sqlVoucherCount = stored.Count,
        sqlPostingCount = stored.Sum(x => x.Entries.Count), sqlAmountTotal = sqlTotal,
        matchedVoucherGuids = source.Count - problems.Count(x => x.StartsWith("Missing SQL voucher GUID", StringComparison.Ordinal)),
        fieldOrPostingMismatches = problems, passed = problems.Count == 0 }, new JsonSerializerOptions { WriteIndented = true }));
    Assert(problems.Count == 0, "Per-voucher Tally-to-SQL comparison failed.");
}

static async Task RunTallyOnlyPreviewAsync()
{
    const string companyName = "JAYA JOTHI MALIGAI";
    const string companyGuid = "5bd36bbb-820e-4784-b692-fe9891019a11";
    const string fromDate = "20240401", toDate = "20240430";
    const decimal expectedTotal = 4241929.10m;
    var settingsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "TallyWebAPI", "TallyWebAPI", "appsettings.json"));
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    var tallyUrl = settings.RootElement.GetProperty("Tally").GetProperty("BaseUrl").GetString()!;
    using var tallyClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    using var voucherClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
    var tally = new TallyService(tallyClient);
    var companies = XDocument.Parse(await tally.GetCompaniesAsync());
    var tallyCompanies = companies.Descendants().Where(x => x.Name.LocalName.Equals("COMPANY", StringComparison.OrdinalIgnoreCase))
        .Where(x => string.Equals((string?)x.Attribute("NAME") ?? x.Elements().FirstOrDefault(y => y.Name.LocalName == "NAME")?.Value, companyName, StringComparison.OrdinalIgnoreCase)).ToList();
    Assert(tallyCompanies.Count == 1, "Tally did not return exactly one selected company.");
    var returnedGuid = tallyCompanies[0].Elements().FirstOrDefault(x => x.Name.LocalName == "GUID")?.Value.Trim() ?? "";
    Assert(string.Equals(returnedGuid, companyGuid, StringComparison.OrdinalIgnoreCase), "Tally company GUID differs from expected company.");
    var types = await tally.GetVoucherTypesAsync(companyName);
    var salesTypes = types.Where(x => string.Equals(x.Parent, "Sales", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var vouchers = await new VoucherService(voucherClient).GetVoucherListAsync(fromDate, toDate, companyName, preserveDuplicateGuids: true);
    var sales = vouchers.Where(x => salesTypes.Contains(x.VoucherType)).ToList();
    var total = sales.Sum(x => decimal.Parse(x.Amount, System.Globalization.CultureInfo.InvariantCulture));
    var postings = sales.Sum(x => x.Postings.Count);
    var invalid = sales.Count(x => string.IsNullOrWhiteSpace(x.Guid) || !x.Guid.StartsWith(companyGuid + "-", StringComparison.OrdinalIgnoreCase) || x.Postings.Count == 0 || x.Postings.Sum(p => p.Amount) != 0m);
    var duplicateGuidRows = sales.Count - sales.Select(x => x.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    var orderedNumbers = sales.Select(x => int.TryParse(x.VoucherNumber, out var number) ? number : -1).Order().ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new { tallyCompany = companyName, tallyCompanyGuid = returnedGuid, fromDate, toDate,
        salesTypes = salesTypes.OrderBy(x => x), sourceVouchers = vouchers.Count, salesVouchers = sales.Count,
        signedPostings = postings, absoluteSalesTotal = total, distinctLedgerNames = sales.SelectMany(x => x.Postings).Select(x => x.LedgerName).Distinct(StringComparer.OrdinalIgnoreCase).Count(), invalidVoucherCount = invalid, duplicateGuidRows, voucherNumberRange = orderedNumbers.Length == 0 ? "none" : $"{orderedNumbers[0]}-{orderedNumbers[^1]}" }, new JsonSerializerOptions { WriteIndented = true }));
    Assert(sales.Count == 103 && postings == 250 && total == expectedTotal && invalid == 0 && duplicateGuidRows == 0 && orderedNumbers.SequenceEqual(Enumerable.Range(1, 103)), "Live Tally-only figures did not match the expected preview data.");
    Console.WriteLine("PASS: Tally read-only source data reconciles. SQL ledger mapping and independent-table readiness remain unverified.");
}

sealed class FixedXmlHandler(string responseXml) : HttpMessageHandler
{
    public string LastRequest { get; private set; } = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseXml, Encoding.UTF8, "application/xml")
        };
        return response;
    }
}
