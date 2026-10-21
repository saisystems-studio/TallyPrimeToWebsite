using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TallyWebAPI.Services;

// Synthetic protocol fixtures only: never used as application/report data.
const string masters = """
<ENVELOPE><BODY><DATA><COLLECTION>
<STOCKGROUP NAME="Goods"><PARENT>Primary</PARENT><GUID>group-goods</GUID></STOCKGROUP>
<STOCKGROUP NAME="Parts"><PARENT>Goods</PARENT><GUID>group-parts</GUID></STOCKGROUP>
<STOCKITEM NAME="Widget"><PARENT>Parts</PARENT><BASEUNITS>nos</BASEUNITS><GUID>item-widget</GUID></STOCKITEM>
<STOCKITEM NAME="Bulk"><PARENT>Goods</PARENT><BASEUNITS>kg</BASEUNITS><GUID>item-bulk</GUID></STOCKITEM>
</COLLECTION></DATA></BODY></ENVELOPE>
""";
const string native = """
<ENVELOPE>
<DSPACCNAME><DSPDISPNAME>Widget</DSPDISPNAME></DSPACCNAME>
<DSPSTKINFO>
<DSPSTKOP><DSPOPQTY>10 nos</DSPOPQTY><DSPOPRATE>10.00/nos</DSPOPRATE><DSPOPAMTA>100.00</DSPOPAMTA></DSPSTKOP>
<DSPSTKIN><DSPINQTY>5 nos</DSPINQTY><DSPINRATE>12.00/nos</DSPINRATE><DSPDRAMTA>-60.00</DSPDRAMTA></DSPSTKIN>
<DSPSTKOUT><DSPOUTQTY>18 nos</DSPOUTQTY><DSPOUTRATE>20.00/nos</DSPOUTRATE><DSPNETTCRAMTA>360.00</DSPNETTCRAMTA></DSPSTKOUT>
<DSPSTKCL><DSPCLQTY>(-)3 nos</DSPCLQTY><DSPCLRATE>12.00/nos</DSPCLRATE><DSPCLAMTA>-36.00</DSPCLAMTA></DSPSTKCL>
</DSPSTKINFO>
<DSPACCNAME><DSPDISPNAME>Bulk</DSPDISPNAME></DSPACCNAME>
<DSPSTKINFO>
<DSPSTKOP><DSPOPQTY>2.500 kg</DSPOPQTY><DSPOPRATE>20.00/kg</DSPOPRATE><DSPOPAMTA>50.00</DSPOPAMTA></DSPSTKOP>
<DSPSTKIN><DSPINQTY>1.500 kg</DSPINQTY><DSPINRATE>20.00/kg</DSPINRATE><DSPDRAMTA>-30.00</DSPDRAMTA></DSPSTKIN>
<DSPSTKOUT><DSPOUTQTY>1 kg</DSPOUTQTY><DSPOUTRATE>30.00/kg</DSPOUTRATE><DSPNETTCRAMTA>30.00</DSPNETTCRAMTA></DSPSTKOUT>
<DSPSTKCL><DSPCLQTY>3.000 kg</DSPCLQTY><DSPCLRATE>20.00/kg</DSPCLRATE><DSPCLAMTA>-60.00</DSPCLAMTA></DSPSTKCL>
</DSPSTKINFO>
</ENVELOPE>
""";

var failures = 0;
await Check("verified company history resolves both blank dates", () =>
{
    var range = StockSummaryService.ResolveFilters(null, null, "19980401", "20250331", "A & B");
    Assert(range == ("19980401", "20250331"), "Blank dates must use Books From and the verified latest date.");
    return Task.CompletedTask;
});
await Check("explicit dates are retained, missing start uses Books From, missing end needs verified latest date", () =>
{
    Assert(StockSummaryService.ResolveFilters("20240501", "20240630", "19980401", "20250331", "A & B") ==
        ("20240501", "20240630"), "Explicit date range changed.");
    Assert(StockSummaryService.ResolveFilters("20240501", null, "19980401", "20250331", "A & B") ==
        ("20240501", "20250331"), "Missing To Date did not use the verified latest accounting date.");
    Assert(StockSummaryService.ResolveFilters(null, "20240630", "19980401", null, "A & B") ==
        ("19980401", "20240630"), "Missing From Date did not use Books From.");
    return Task.CompletedTask;
});
await Check("invalid ranges and absent reporting boundaries are rejected", () =>
{
    AssertReject(() => StockSummaryService.ResolveFilters("20240501", "20240430", "19980401", "20250331", "A & B"));
    AssertReject(() => StockSummaryService.ResolveFilters("20240230", null, "19980401", "20250331", "A & B"));
    AssertReject(() => StockSummaryService.ResolveFilters(null, null, "19980401", null, "A & B"));
    return Task.CompletedTask;
});
await Check("latest Tally company entry date comes from EndingAt", async () =>
{
    const string latestXml = "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"JAYA JOTHI MALIGAI\"><NAME>JAYA JOTHI MALIGAI</NAME><STARTINGFROM>20210401</STARTINGFROM><ENDINGAT TYPE=\"Date\">20241231</ENDINGAT><STOCKSUMMARYFROM>20240401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20241231</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
    var handler = new TallyHandler(masters, masters, responseFactory: _ => latestXml);
    var latest = await new StockSummaryService(new HttpClient(handler))
        .GetLatestVoucherDateAsync("JAYA JOTHI MALIGAI", "20210401");
    Assert(latest == "20241231", "Tally's aggregate date was not returned.");
    var request = handler.Requests.Single();
    Assert(request.Descendants("SVCURRENTCOMPANY").Single().Value == "JAYA JOTHI MALIGAI", "Latest date query must be company scoped.");
    Assert(!request.Descendants("SVFROMDATE").Any() && !request.Descendants("SVTODATE").Any(),
        "Latest date query must not inherit a period boundary from Tally's current financial year.");
    Assert(request.Descendants("TYPE").Any(x => x.Value == "Company") &&
        request.Descendants("FETCH").Single().Value.Contains("EndingAt"), "Latest entry date must come from the selected Tally company's EndingAt method.");
});
await Check("Tally company StartingFrom and EndingAt resolve the actual report range", async () =>
{
    var handler = new TallyHandler(masters, native, responseFactory: request =>
    {
        if (request.Descendants("ID").Single().Value == "WebCompanyAccountingDates")
            return "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"PMP Rice Mill\"><NAME>PMP Rice Mill</NAME><STARTINGFROM>20260401</STARTINGFROM><ENDINGAT>20261002</ENDINGAT><STOCKSUMMARYFROM>20260401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20261002</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
        return "<ENVELOPE><BODY><DATA><COLLECTION /></DATA></BODY></ENVELOPE>";
    });
    var range = await new StockSummaryService(new HttpClient(handler))
        .ResolveRequestedPeriodAsync(null, null, "20210401", "PMP Rice Mill");
    Assert(range == ("20260401", "20261002"), "The current Tally period start and actual latest voucher date must be used.");
});
await Check("SQL company GUID must match one unambiguous Tally company before sync", async () =>
{
    const string identityXml = "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"Alpha Co\"><GUID>guid-alpha</GUID></COMPANY><COMPANY NAME=\"Beta Co\"><GUID>guid-beta</GUID></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
    var handler = new TallyHandler(masters, native, identityXml);
    var service = new StockSummaryService(new HttpClient(handler));
    await service.VerifyCompanyIdentityAsync("Alpha Co", "guid-alpha");
    await MustReject(() => service.VerifyCompanyIdentityAsync("Alpha Co", "guid-beta"));
    await MustReject(() => service.VerifyCompanyIdentityAsync("Missing Co", "guid-missing"));
    await MustReject(() => service.VerifyCompanyIdentityAsync("Alpha Co", ""));
    Assert(handler.Requests.All(x => x.Descendants("ID").Any(id => id.Value == "WebStockSummaryCompanyIdentity")),
        "Company identity validation must use Tally's company collection response.");
});
await Check("duplicate Tally names are rejected as ambiguous company identities", async () =>
{
    const string duplicateXml = "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"Same Co\"><GUID>guid-one</GUID></COMPANY><COMPANY NAME=\"Same Co\"><GUID>guid-two</GUID></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
    var service = new StockSummaryService(new HttpClient(new TallyHandler(masters, native, duplicateXml)));
    await MustReject(() => service.VerifyCompanyIdentityAsync("Same Co", "guid-one"));
});
await Check("company switching resolves independent Tally periods", async () =>
{
    var handler = new TallyHandler(masters, native, responseFactory: request =>
    {
        var company = request.Descendants("SVCURRENTCOMPANY").Single().Value;
        if (request.Descendants("ID").Single().Value == "WebCompanyAccountingDates")
            return company == "Alpha Co"
                ? "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"Alpha Co\"><NAME>Alpha Co</NAME><STARTINGFROM>20210401</STARTINGFROM><ENDINGAT>20241231</ENDINGAT><STOCKSUMMARYFROM>20240401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20241231</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>"
                : "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"Beta Co\"><NAME>Beta Co</NAME><STARTINGFROM>20230701</STARTINGFROM><ENDINGAT>20231215</ENDINGAT><STOCKSUMMARYFROM>20230401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20231215</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
        return "<ENVELOPE><BODY><DATA><COLLECTION /></DATA></BODY></ENVELOPE>";
    });
    var service = new StockSummaryService(new HttpClient(handler));
    var alpha = await service.ResolveRequestedPeriodAsync(null, null, "20200101", "Alpha Co");
    var beta = await service.ResolveRequestedPeriodAsync(null, null, "20200101", "Beta Co");
    Assert(alpha == ("20240401", "20241231") && beta == ("20230401", "20231215"),
        "Switching companies reused another company's reporting period.");
    Assert(handler.Requests.Where(x => x.Descendants("ID").Any(id => id.Value == "WebCompanyAccountingDates"))
        .All(x => x.Descendants("SVCURRENTCOMPANY").Any()), "Company-specific period requests must carry the selected Tally company.");
});
await Check("company without vouchers accepts Tally's exact company dates", async () =>
{
    var handler = new TallyHandler(masters, native, responseFactory: request =>
    {
        if (request.Descendants("ID").Single().Value == "WebCompanyAccountingDates")
            return "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"No Voucher Co\"><NAME>No Voucher Co</NAME><STARTINGFROM>20240401</STARTINGFROM><ENDINGAT>20240401</ENDINGAT><STOCKSUMMARYFROM>20240401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20240401</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
        return "<ENVELOPE><BODY><DATA><COLLECTION /></DATA></BODY></ENVELOPE>";
    });
    var service = new StockSummaryService(new HttpClient(handler));
    var range = await service.ResolveRequestedPeriodAsync(null, null, "20200101", "No Voucher Co");
    Assert(range == ("20240401", "20240401"), "A company without voucher activity should use the exact Tally company dates returned.");
    var emptyHandler = new TallyHandler(
        "<ENVELOPE><BODY><DATA><COLLECTION /></DATA></BODY></ENVELOPE>",
        "<ENVELOPE><BODY><DATA><COLLECTION /></DATA></BODY></ENVELOPE>");
    var emptyReport = await new StockSummaryService(new HttpClient(emptyHandler))
        .GetStockSummaryAsync("20240401", "20250331", "No Voucher Co");
    Assert(emptyReport.TotalItems == 0 && emptyReport.Groups.Count == 0,
        "A legitimately empty Stock Summary must be represented as zero items without invented groups.");
});
await Check("empty dates use the selected company's Tally period", async () =>
{
    const string periodXml = "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"JAYA JOTHI MALIGAI\"><NAME>JAYA JOTHI MALIGAI</NAME><STARTINGFROM>20210401</STARTINGFROM><ENDINGAT>20241231</ENDINGAT><STOCKSUMMARYFROM>20240401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20241231</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>";
    var handler = new TallyHandler(masters, native, responseFactory: request =>
        request.Descendants("ID").Single().Value == "WebCompanyAccountingDates" ? periodXml : masters);
    var service = new StockSummaryService(new HttpClient(handler));
    var range = await service.ResolveRequestedPeriodAsync(null, null, "20210401", "JAYA JOTHI MALIGAI");
    Assert(range == ("20240401", "20241231"), "Blank dates must use the selected company's Tally StartingFrom and EndingAt dates.");
    Assert(handler.Requests.Count == 1, "Default resolution must use one Tally company dates query.");
    var periodRequest = handler.Requests.Single(x => x.Descendants("ID").Any(id => id.Value == "WebCompanyAccountingDates"));
    Assert(periodRequest.Descendants("SVCURRENTCOMPANY").Single().Value == "JAYA JOTHI MALIGAI", "Active period query must select the requested company.");
    Assert(periodRequest.Descendants("FETCH").Single().Value.Contains("StartingFrom") &&
        periodRequest.Descendants("FETCH").Single().Value.Contains("EndingAt") &&
        periodRequest.Descendants("COMPUTE").Any(x => x.Value == "StockSummaryFrom: $$FinYearBeg:$EndingAt:$StartingFrom") &&
        periodRequest.Descendants("COMPUTE").Any(x => x.Value == "StockSummaryTo: $EndingAt"),
        "Default period must use Tally's financial-year-begin function and actual last-entry date.");
});
await Check("explicit and one-date requests retain their existing resolution", async () =>
{
    var explicitHandler = new TallyHandler(masters, native);
    var explicitRange = await new StockSummaryService(new HttpClient(explicitHandler))
        .ResolveRequestedPeriodAsync("20240501", "20240630", "20210401", "JAYA JOTHI MALIGAI");
    Assert(explicitRange == ("20240501", "20240630") && explicitHandler.Requests.Count == 0,
        "Explicit dates must be used exactly without a Tally period lookup.");

    var fromHandler = new TallyHandler(masters, native);
    var fromRange = await new StockSummaryService(new HttpClient(fromHandler))
        .ResolveRequestedPeriodAsync("20240501", null, "20210401", "JAYA JOTHI MALIGAI");
    Assert(fromRange == ("20240501", "20241231"), "From-only request must retain Tally EndingAt To behavior.");

    var toHandler = new TallyHandler(masters, native);
    var toRange = await new StockSummaryService(new HttpClient(toHandler))
        .ResolveRequestedPeriodAsync(null, "20240630", "20210401", "JAYA JOTHI MALIGAI");
    Assert(toRange == ("20210401", "20240630") && toHandler.Requests.Count == 0,
        "To-only request must retain Books From behavior.");
});
await Check("native valuation and company/date scoped requests", async () =>
{
    var handler = new TallyHandler(masters, native);
    var report = await new StockSummaryService(new HttpClient(handler))
        .GetStockSummaryAsync("20240401", "20240430", "A & B");
    Assert(handler.Requests.Any(x => x.Descendants("TYPE").Any(e => e.Value == "Data")), "Must request Tally's native report, not infer valuation from masters.");
    foreach (var request in handler.Requests)
    {
        Assert(request.Descendants("SVCURRENTCOMPANY").Single().Value == "A & B", "Every request must be company scoped and escaped.");
        Assert(request.Descendants("SVFROMDATE").Single().Value == "20240401", "Start date missing.");
        Assert(request.Descendants("SVTODATE").Single().Value == "20240430", "End date missing.");
    }
    var itemRequest = handler.Requests.First(x => x.Descendants("TYPE").Any(e => e.Value == "StockItem"));
    Assert(itemRequest.Descendants("FETCH").Single().Value.Contains("GUID") && itemRequest.Descendants("FETCH").Single().Value.Contains("BaseUnits"), "Stock Item master must fetch GUID and BaseUnits.");
    var groupRequest = handler.Requests.First(x => x.Descendants("TYPE").Any(e => e.Value == "StockGroup"));
    Assert(groupRequest.Descendants("FETCH").Single().Value.Contains("GUID") && groupRequest.Descendants("FETCH").Single().Value.Contains("Parent"), "Stock Group master must fetch GUID and Parent.");
    var json = JsonSerializer.SerializeToElement(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    var total = json.GetProperty("grandTotal");
    Assert(total.GetProperty("inwardValue").GetDecimal() == 90m, "Inward total must count each leaf once.");
    Assert(total.GetProperty("outwardValue").GetDecimal() == 390m, "Outward total must use Tally transaction values.");
    Assert(total.GetProperty("closingValue").GetDecimal() == 96m, "Closing values must use the native amount field and Tally sign convention.");
    Assert(total.GetProperty("closingQuantity").ValueKind == JsonValueKind.Null, "Mixed units must not be added.");
    var group = json.GetProperty("groups")[0];
    Assert(group.GetProperty("closingValue").GetDecimal() == 96m, "Nested group total is wrong.");
    Assert(group.GetProperty("closingRate").ValueKind == JsonValueKind.Null, "Group rates must not be derived from item rates.");
    Assert(group.GetProperty("tallyGuid").GetString() == "group-goods", "Stock Group GUID was not retained.");
    var item = group.GetProperty("children")[0].GetProperty("items")[0];
    Assert(item.GetProperty("closingQuantity").GetDecimal() == -3m, "Negative stock sign lost.");
    Assert(item.GetProperty("closingQuantityText").GetString() == "(-)3 nos", "Native quantity formatting lost.");
    Assert(item.GetProperty("outwardRate").GetDecimal() == 20m, "Transaction rate changed.");
    Assert(item.GetProperty("tallyGuid").GetString() == "item-widget", "Stock Item GUID was not retained.");
    Assert(item.GetProperty("closingValue").GetDecimal() == 36m, "Closing sign/tag mapping is wrong.");
    Assert(item.GetProperty("inwardValue").GetDecimal() == 60m, "Inward sign/tag mapping is wrong.");
});
await Check("blank Tally amounts remain blank even when quantities are non-zero", async () =>
{
    var withBlank = native.Replace("<DSPDRAMTA>-30.00</DSPDRAMTA>", "<DSPDRAMTA></DSPDRAMTA>");
    var service = new StockSummaryService(new HttpClient(new TallyHandler(masters, withBlank)));
    var blankReport = await service.GetStockSummaryAsync("20240401", "20240430", "A & B");
    var blankJson = JsonSerializer.SerializeToElement(blankReport, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    var bulkItem = blankJson.GetProperty("groups")[0].GetProperty("items")[0];
    Assert(bulkItem.GetProperty("inwardValue").ValueKind == JsonValueKind.Null,
        "A blank native amount must stay blank; non-zero quantity does not imply a value.");

    var noMovement = native
        .Replace("<DSPINQTY>1.500 kg</DSPINQTY>", "<DSPINQTY>0 kg</DSPINQTY>")
        .Replace("<DSPCLQTY>3.000 kg</DSPCLQTY>", "<DSPCLQTY>1.500 kg</DSPCLQTY>")
        .Replace("<DSPDRAMTA>-30.00</DSPDRAMTA>", "<DSPDRAMTA></DSPDRAMTA>");
    var report = await new StockSummaryService(new HttpClient(new TallyHandler(masters, noMovement)))
        .GetStockSummaryAsync("20240401", "20240430", "A & B");
    var json = JsonSerializer.SerializeToElement(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    var bulk = json.GetProperty("groups")[0].GetProperty("items")[0];
    Assert(bulk.GetProperty("inwardValue").ValueKind == JsonValueKind.Null, "A blank native amount must remain blank for zero movement too.");
    Assert(json.GetProperty("grandTotal").GetProperty("inwardValue").GetDecimal() == 60m, "Zero-activity amount handling changed other item values.");
});
await Check("Tally error is not an empty successful report", async () =>
{
    var service = new StockSummaryService(new HttpClient(new TallyHandler(masters, "<ENVELOPE><LINEERROR>Company not found</LINEERROR></ENVELOPE>")));
    await MustReject(() => service.GetStockSummaryAsync("20240401", "20240430", "Other"));
});
await Check("missing movement columns cannot silently become zero", async () =>
{
    var xml = XDocument.Parse(native);
    xml.Descendants("DSPSTKIN").Remove();
    var service = new StockSummaryService(new HttpClient(new TallyHandler(masters, xml.ToString())));
    await MustReject(() => service.GetStockSummaryAsync("20240401", "20240430", "A & B"));
});
await Check("unknown report items cannot be silently dropped", async () =>
{
    var service = new StockSummaryService(new HttpClient(new TallyHandler(masters, native.Replace("Widget", "Unknown"))));
    await MustReject(() => service.GetStockSummaryAsync("20240401", "20240430", "A & B"));
});
await Check("Stock Item master rows absent from the native report are not added", async () =>
{
    var mastersWithInactive = masters.Replace(
        "</COLLECTION>",
        "<STOCKITEM NAME=\"Jam Bottel\"><PARENT>Goods</PARENT><BASEUNITS>nos</BASEUNITS><GUID>item-jam</GUID></STOCKITEM></COLLECTION>");
    var report = await new StockSummaryService(new HttpClient(new TallyHandler(mastersWithInactive, native)))
        .GetStockSummaryAsync("20240401", "20240430", "A & B");
    Assert(report.TotalItems == 2, "Stock Items Master rows must not be treated as Stock Summary report rows.");
    Assert(report.Groups.SelectMany(x => x.Items).All(x => x.StockItemName != "Jam Bottel"),
        "An item absent from the native report was added to the result.");
});
await Check("missing parent groups cannot be invented", async () =>
{
    var incompleteMasters = masters.Replace("<STOCKGROUP NAME=\"Parts\"><PARENT>Goods</PARENT><GUID>group-parts</GUID></STOCKGROUP>", "");
    var service = new StockSummaryService(new HttpClient(new TallyHandler(incompleteMasters, native)));
    await MustReject(() => service.GetStockSummaryAsync("20240401", "20240430", "A & B"));
});
await Check("invalid date or missing company causes no Tally request", async () =>
{
    var handler = new TallyHandler(masters, native);
    var service = new StockSummaryService(new HttpClient(handler));
    await MustReject(() => service.GetStockSummaryAsync("20240431", "20240501", "A & B"));
    await MustReject(() => service.GetStockSummaryAsync("20240501", "20240401", "A & B"));
    await MustReject(() => service.GetStockSummaryAsync("20240401", "20240430", ""));
    Assert(handler.Requests.Count == 0, "Invalid filters reached Tally.");
});
Environment.ExitCode = failures == 0 ? 0 : 1;
Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}: {failures} failed checks.");

async Task Check(string name, Func<Task> test)
{
    try { await test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
static void AssertReject(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Expected invalid date input to be rejected.");
}
static async Task MustReject(Func<Task> action)
{
    try { await action(); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException) { return; }
    throw new Exception("Expected a validation or incomplete-data error.");
}

sealed class TallyHandler(string masters, string report, string? companyIdentity = null,
    Func<XDocument, string>? responseFactory = null) : HttpMessageHandler
{
    public List<XDocument> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var xml = XDocument.Parse(Encoding.Unicode.GetString(await request.Content!.ReadAsByteArrayAsync(cancellationToken)));
        Requests.Add(xml);
        var isReport = xml.Descendants("TYPE").Any(x => x.Value == "Data");
        var isCompanyDates = xml.Descendants("ID").Any(x => x.Value == "WebCompanyAccountingDates");
        var isCompanyIdentity = xml.Descendants("ID").Any(x => x.Value == "WebStockSummaryCompanyIdentity");
        var response = responseFactory is not null ? responseFactory(xml)
            : isCompanyIdentity
            ? companyIdentity ?? "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"A &amp; B\"><GUID>fixture-guid</GUID></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>"
            : isCompanyDates
            ? "<ENVELOPE><BODY><DATA><COLLECTION><COMPANY NAME=\"JAYA JOTHI MALIGAI\"><NAME>JAYA JOTHI MALIGAI</NAME><STARTINGFROM TYPE=\"Date\">20210401</STARTINGFROM><ENDINGAT TYPE=\"Date\">20241231</ENDINGAT><STOCKSUMMARYFROM>20240401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20241231</STOCKSUMMARYTO></COMPANY><COMPANY NAME=\"Other Company\"><NAME>Other Company</NAME><STARTINGFROM TYPE=\"Date\">20210401</STARTINGFROM><ENDINGAT TYPE=\"Date\">20250331</ENDINGAT><STOCKSUMMARYFROM>20240401</STOCKSUMMARYFROM><STOCKSUMMARYTO>20250331</STOCKSUMMARYTO></COMPANY></COLLECTION></DATA></BODY></ENVELOPE>"
            : isReport ? report : masters;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/xml") };
    }
}
