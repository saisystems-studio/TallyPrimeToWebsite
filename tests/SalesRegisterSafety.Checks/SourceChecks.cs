using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Models;
using TallyWebAPI.Services.SalesRegister;

internal static class SourceChecks
{
    public static async Task RunAsync()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Companies.Add(new CompanyEntity{Id=7,Name="Fixture company",TallyGuid="company-guid"});
        db.Ledgers.AddRange(new LedgerEntity{CompanyId=7,Name="Party",TallyGuid="party-guid"},new LedgerEntity{CompanyId=7,Name="Sales",TallyGuid="sales-guid"});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var handler=new XmlHandler();
        var adapter=new SalesSyncSource(db,new HttpClient(handler){BaseAddress=new Uri("http://fixture.test/")});
        var snapshot=await adapter.ReadAsync(7);
        if(snapshot.Active.Length!=1 || snapshot.Cancelled.Length!=1 || snapshot.Active[0].Postings[0].SignedAmount!=-10 || snapshot.Errors.Length!=0 || !snapshot.SalesTypes.Contains("Nested Sales"))throw new Exception("Source extraction lost classification, cancellation, signs or sequence.");
        handler.BadStatus=true;
        try{await adapter.ReadAsync(7);throw new Exception("Failed export was accepted.");}catch(InvalidOperationException ex)when(ex.Message.Contains("export")){}
        handler.BadStatus=false;handler.BadAmount=true;
        if((await adapter.ReadAsync(7)).Errors.Length==0)throw new Exception("Invalid active posting was accepted.");
        handler.BadAmount=false;handler.Drift=true;handler.HistoryReads=0;
        if((await adapter.ReadAsync(7)).Errors.Length==0)throw new Exception("Metadata drift was accepted.");
        Console.WriteLine("PASS: real XML adapter resolves Sales ancestry, cancellations and ordered signed postings; rejects malformed export, postings and metadata drift.");
    }
    sealed class XmlHandler:HttpMessageHandler
    {
        public bool BadStatus,BadAmount,Drift;public int HistoryReads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var xml=XDocument.Parse(await request.Content!.ReadAsStringAsync(token));var id=xml.Descendants("ID").First().Value;
            var data=id switch {
                "SafeSalesCompanies"=>"<COMPANY NAME=\"Fixture company\"><GUID>company-guid</GUID><NAME>Fixture company</NAME><BOOKSFROM>20200401</BOOKSFROM><ENDINGAT>20200430</ENDINGAT></COMPANY>",
                "SafeSalesTypes"=>"<VOUCHERTYPE NAME=\"Sales\"><PARENT>Sales</PARENT></VOUCHERTYPE><VOUCHERTYPE NAME=\"Custom\"><PARENT>Sales</PARENT></VOUCHERTYPE><VOUCHERTYPE NAME=\"Nested Sales\"><PARENT>Custom</PARENT></VOUCHERTYPE>",
                _=>Vouchers(id=="SafeSalesPostings",BadAmount,Drift && id=="SafeSalesHistory" && ++HistoryReads>1)
            };
            var body=BadStatus?"<ENVELOPE><HEADER><STATUS>0</STATUS></HEADER></ENVELOPE>":"<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION>"+data+"</COLLECTION></DATA></BODY></ENVELOPE>";
            return new(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"text/xml")};
        }
        static string Vouchers(bool postings,bool bad,bool drift)=>$"""
        <VOUCHER><GUID>source-1</GUID><DATE>20200402</DATE><VOUCHERNUMBER>{(drift?"changed":"1")}</VOUCHERNUMBER><VOUCHERTYPENAME>Nested Sales</VOUCHERTYPENAME><ISCANCELLED>No</ISCANCELLED><PARTYLEDGERNAME>Party</PARTYLEDGERNAME>{(postings?$"<ALLLEDGERENTRIES.LIST><LEDGERNAME>Party</LEDGERNAME><AMOUNT>{(bad?"bad":"-10")}</AMOUNT></ALLLEDGERENTRIES.LIST><ALLLEDGERENTRIES.LIST><LEDGERNAME>Sales</LEDGERNAME><AMOUNT>10</AMOUNT></ALLLEDGERENTRIES.LIST>":"")}</VOUCHER>
        <VOUCHER><GUID>cancelled-1</GUID><DATE>20200403</DATE><VOUCHERNUMBER>2</VOUCHERNUMBER><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><ISCANCELLED>Yes</ISCANCELLED></VOUCHER>
        """;
    }
}
