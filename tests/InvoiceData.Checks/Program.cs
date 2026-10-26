using System.Net;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;
using TallyWebAPI.Services;

const string fixture = """
<ENVELOPE><VOUCHER><DATE>20241101</DATE><VOUCHERNUMBER>661</VOUCHERNUMBER><VOUCHERTYPENAME>Purchase</VOUCHERTYPENAME><GUID>purchase-fixture</GUID><PARTYLEDGERNAME>Supplier Ledger</PARTYLEDGERNAME><PARTYNAME>Actual Supplier</PARTYNAME><PARTYGSTIN>33AGIPS9108F1ZB</PARTYGSTIN><STATENAME>Tamil Nadu</STATENAME><ADDRESS.LIST><ADDRESS>Supplier Street</ADDRESS><ADDRESS>Supplier City</ADDRESS></ADDRESS.LIST><BASICBUYERNAME>Separate Receiver</BASICBUYERNAME><CONSIGNEEMAILINGNAME>Separate Receiver</CONSIGNEEMAILINGNAME><BASICBUYERADDRESS.LIST><BASICBUYERADDRESS>Ship Road</BASICBUYERADDRESS><BASICBUYERADDRESS>Ship City</BASICBUYERADDRESS></BASICBUYERADDRESS.LIST><CONSIGNEEGSTIN>33AFHPB5979B1ZO</CONSIGNEEGSTIN><CONSIGNEESTATENAME>Tamil Nadu</CONSIGNEESTATENAME><REFERENCE>SUP/9</REFERENCE><REFERENCEDATE>20241023</REFERENCEDATE><CMPGSTIN>33AFHPB5979B1ZO</CMPGSTIN><INVENTORYENTRIES.LIST><STOCKITEMNAME>தமிழ்</STOCKITEMNAME><ACTUALQTY>2 kg</ACTUALQTY><RATE>114.02/kg</RATE><DISCOUNT>5</DISCOUNT><AMOUNT>-228.04</AMOUNT></INVENTORYENTRIES.LIST><LEDGERENTRIES.LIST><LEDGERNAME>Supplier Ledger</LEDGERNAME><AMOUNT>228.04</AMOUNT></LEDGERENTRIES.LIST></VOUCHER></ENVELOPE>
""";
using var client = FixtureClient(new FixtureHandler(fixture));
var service = new VoucherService(client);
var detail = await service.GetVoucherDetailAsync("purchase-fixture", "20240401", "20241231", "Fixture Company");
void Equal(object? actual, object? expected, string field) { if (!Equals(actual,expected)) throw new Exception($"{field}: expected '{expected}', got '{actual}'."); }
Equal(detail!.PartyName,"Actual Supplier","Voucher party display name");
// New DTO fields are accessed reflectively so this test initially fails against the missing mapping.
object? Property(object value,string name) => value.GetType().GetProperty(name)?.GetValue(value);
Equal(Property(detail,"PartyAddress"),"Supplier Street\nSupplier City","Party address");
Equal(Property(detail,"PartyLedgerName"),"Supplier Ledger","Party ledger identity");
Equal(Property(detail,"ReferenceDate"),"20241023","Supplier reference date");
var consignee=Property(detail,"Consignee") ?? throw new Exception("Consignee mapping missing");
Equal(Property(consignee,"Name"),"Separate Receiver","Consignee name");
Equal(Property(consignee,"Address"),"Ship Road\nShip City","Consignee address");
Equal(Property(consignee,"StateCode"),"33","Consignee state code");
Equal(detail.Items.Count,1,"Alternate inventory list");
Equal(Property(detail.Items[0],"Discount"),"5","Discount");
Equal(detail.TotalAmount,"228.04","Original voucher total");
Equal(detail.Items[0].Amount,"228.04","Original item amount");
Console.WriteLine("Fixture: party/consignee separation, reference date, address lines, state code, discount and original amounts verified.");
using var companyHandler = new FixtureHandler("<ENVELOPE><COMPANY NAME=\"Fixture Company\"><GUID>company-guid</GUID><ADDRESS.LIST><ADDRESS>Actual Company Street</ADDRESS><ADDRESS>Actual Company City</ADDRESS></ADDRESS.LIST><STATENAME>Tamil Nadu</STATENAME><EMAIL>contact@example.test</EMAIL><MOBILENUMBERS.LIST><MOBILENUMBERS>123456</MOBILENUMBERS></MOBILENUMBERS.LIST></COMPANY></ENVELOPE>");
using var companyClient = FixtureClient(companyHandler);
var fixtureCompany = await new VoucherService(companyClient).GetInvoiceCompanyAsync("Fixture Company");
Equal(fixtureCompany!.Address,"Actual Company Street\nActual Company City","Company object postal address");
Equal(fixtureCompany.Email,"contact@example.test","Company email");
Equal(fixtureCompany.Mobile,"123456","Company mobile list");
var companyRequest = XDocument.Parse(companyHandler.LastRequest);
Equal(companyRequest.Root!.Element("HEADER")!.Element("TYPE")!.Value,"Object","Company must use Object, not collection");
if (!companyRequest.Descendants("FETCH").Any(x=>x.Value=="StateName") || !companyRequest.Descendants("FETCH").Any(x=>x.Value=="Email")) throw new Exception("Object FETCHLIST needs individual fetch tags.");
Console.WriteLine("Fixture: company object request, individual fetch tags and full company postal/contact mapping verified.");

if (args.Contains("--live"))
{
    var audit = Path.GetFullPath("diagnostics/invoice-data");
    using var liveClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    var live = new VoucherService(liveClient);
    XDocument Read(string file)
    {
        var text = File.ReadAllText(Path.Combine(audit,file));
        text = System.Text.RegularExpressions.Regex.Replace(text,@"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);", "");
        text = System.Text.RegularExpressions.Regex.Replace(text,@"(<\/?)UDF:", "$1");
        return XDocument.Parse(text);
    }
    string Value(XElement node,string tag) => node.Element(tag)?.Value.Trim() ?? "";
    string Address(XElement node,string list,string tag) => string.Join("\n",node.Elements(list).Elements(tag).Select(x=>x.Value.Trim()).Where(x=>x.Length>0));
    var company = await live.GetInvoiceCompanyAsync("JAYA JOTHI MALIGAI") ?? throw new Exception("Live company missing");
    var master = Read("company-object.xml").Descendants("COMPANY").Single(x=>(string?)x.Attribute("NAME")=="JAYA JOTHI MALIGAI");
    Equal(company.Name,"JAYA JOTHI MALIGAI","Live company name");
    Equal(company.Address,Address(master,"ADDRESS.LIST","ADDRESS"),"Live company postal address");
    Equal(company.State,Value(master,"STATENAME"),"Live company state");
    Equal(company.Email,Value(master,"EMAIL"),"Live company email");
    Equal(company.Mobile,Address(master,"MOBILENUMBERS.LIST","MOBILENUMBERS"),"Live company mobile");
    File.WriteAllText(Path.Combine(audit,"company-api.json"),JsonSerializer.Serialize(company,new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true }));
    foreach (var kind in new[]{"purchase","sales"})
    {
        var raw = Read(kind+".xml").Descendants("VOUCHER").Single(x=>x.Element("GUID")!=null);
        var actual = await live.GetVoucherDetailAsync(Value(raw,"GUID"),"20240401","20261231","JAYA JOTHI MALIGAI") ?? throw new Exception("Live voucher missing");
        Equal(actual.Guid,Value(raw,"GUID"),kind+" GUID");
        Equal(actual.VoucherType,Value(raw,"VOUCHERTYPENAME"),kind+" type");
        Equal(actual.VoucherNumber,Value(raw,"VOUCHERNUMBER"),kind+" number");
        Equal(actual.Date,Value(raw,"DATE"),kind+" date");
        Equal(actual.PartyName,Value(raw,"PARTYNAME"),kind+" actual party");
        Equal(actual.PartyLedgerName,Value(raw,"PARTYLEDGERNAME"),kind+" ledger identity");
        Equal(actual.PartyAddress,Address(raw,"ADDRESS.LIST","ADDRESS"),kind+" party address");
        Equal(actual.PartyGstin,Value(raw,"PARTYGSTIN"),kind+" party GSTIN");
        Equal(actual.State,Value(raw,"STATENAME"),kind+" party state");
        Equal(actual.PartyPincode,Value(raw,"PARTYPINCODE"),kind+" party pincode");
        Equal(actual.Reference,Value(raw,"REFERENCE"),kind+" supplier invoice/reference number");
        Equal(actual.ReferenceDate,Value(raw,"REFERENCEDATE"),kind+" supplier invoice/reference date");
        Equal(actual.CompanyGstin,Value(raw,"CMPGSTIN"),kind+" company GSTIN");
        Equal(actual.Consignee.Name,Value(raw,"CONSIGNEEMAILINGNAME"),kind+" consignee name");
        Equal(actual.Consignee.Address,Address(raw,"BASICBUYERADDRESS.LIST","BASICBUYERADDRESS"),kind+" consignee address");
        Equal(actual.Consignee.Gstin,Value(raw,"CONSIGNEEGSTIN"),kind+" consignee GSTIN");
        Equal(actual.Consignee.State,Value(raw,"CONSIGNEESTATENAME"),kind+" consignee state");
        Equal(actual.Consignee.Pincode,Value(raw,"CONSIGNEEPINCODE"),kind+" consignee pincode");
        var inventory=raw.Elements("ALLINVENTORYENTRIES.LIST").ToList();
        if (inventory.Count==0) inventory=raw.Elements("INVENTORYENTRIES.LIST").ToList();
        Equal(actual.Items.Count,inventory.Count,kind+" item count");
        for(var i=0;i<inventory.Count;i++)
        {
            Equal(actual.Items[i].ItemName,Value(inventory[i],"STOCKITEMNAME"),kind+" item name "+i);
            Equal(actual.Items[i].Quantity,Value(inventory[i],"ACTUALQTY"),kind+" quantity "+i);
            Equal(actual.Items[i].Rate,Value(inventory[i],"RATE"),kind+" rate "+i);
            Equal(actual.Items[i].Discount,Value(inventory[i],"DISCOUNT"),kind+" discount "+i);
            Equal(decimal.Parse(actual.Items[i].Amount),Math.Abs(decimal.Parse(Value(inventory[i],"AMOUNT"))),kind+" item amount "+i);
        }
        var partyLedger=raw.Descendants().First(x=>(x.Name.LocalName=="ALLLEDGERENTRIES.LIST"||x.Name.LocalName=="LEDGERENTRIES.LIST")&&Value(x,"LEDGERNAME")==actual.PartyLedgerName);
        Equal(decimal.Parse(actual.TotalAmount),Math.Abs(decimal.Parse(Value(partyLedger,"AMOUNT"))),kind+" original grand total");
        File.WriteAllText(Path.Combine(audit,kind+"-api.json"),JsonSerializer.Serialize(actual,new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true }));
        Console.WriteLine($"LIVE {kind} {actual.VoucherNumber}: identity, dates, party and consignee fields, reference/date, every item and original total {actual.TotalAmount} match independent Tally XML.");
    }
    Console.WriteLine("LIVE company: every postal line, state, email and mobile match the selected Company object export.");
}

// Fixture transport is fully intercepted; no live endpoint is contacted.
static HttpClient FixtureClient(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://fixture.invalid/") };

sealed class FixtureHandler(string xml) : HttpMessageHandler
{
    public string LastRequest { get; private set; } = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        LastRequest = body;
        if (!body.Contains("<TALLYREQUEST>Export</TALLYREQUEST>")) throw new Exception("Only read-only Tally Export is allowed.");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml,Encoding.Unicode,"text/xml") };
    }
}
