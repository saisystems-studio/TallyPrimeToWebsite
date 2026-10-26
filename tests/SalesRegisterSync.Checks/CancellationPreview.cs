using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TallyWebAPI.Data;
using TallyWebAPI.Services;

internal static class CancellationPreview
{
    public static async Task RunAsync()
    {
        var settings = JsonDocument.Parse(await File.ReadAllTextAsync("TallyWebAPI/TallyWebAPI/appsettings.json")).RootElement;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(settings.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString())
            .AddInterceptors(new SelectOnlyInterceptor()).Options;
        await using var db = new AppDbContext(options);
        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Name == "JAYA JOTHI MALIGAI");
        async Task<string> Snapshot()
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT * FROM dbo.SalesRegister_tbl ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) + (SELECT * FROM dbo.SalesRegisterEntries_tbl ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((string)(await command.ExecuteScalarAsync())!)));
        }
        var before = await Snapshot();
        var tallyUrl = settings.GetProperty("Tally").GetProperty("BaseUrl").GetString()!;
        using var tallyClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
        using var voucherClient = new HttpClient { BaseAddress = new Uri(tallyUrl) };
        var service = new SalesRegisterSyncService(db, new TallyService(tallyClient), new VoucherService(voucherClient));
        // Direct service construction: no API startup, hosted sync, migrations or seeding.
        var result = JsonSerializer.SerializeToElement(await service.PreviewOrApplyAsync(company.Id, "20240501", "20241231", apply: false));
        var after = await Snapshot();
        await File.WriteAllTextAsync("diagnostics/sales-register-ui/cancellation-full-preview.json", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        if (before != after) throw new Exception("Independent register snapshot changed during preview.");
        var expected = new Dictionary<string, (int Active, int Cancelled, int Postings, decimal Total)> {
            ["202405"]=(117,0,300,4730804.11m), ["202406"]=(116,0,290,4556603m),
            ["202407"]=(151,0,411,5780057.47m), ["202408"]=(107,0,306,5037213m),
            ["202409"]=(156,23,526,5462724m), ["202410"]=(41,3,261,4897427m),
            ["202411"]=(38,0,277,4569127m), ["202412"]=(23,0,159,1690993.64m)
        };
        foreach (var month in result.GetProperty("monthly").EnumerateArray())
        {
            var e = expected[month.GetProperty("month").GetString()!];
            if (month.GetProperty("activeSalesVouchers").GetInt32()!=e.Active || month.GetProperty("cancelledSalesVouchers").GetInt32()!=e.Cancelled || month.GetProperty("signedPostings").GetInt32()!=e.Postings || month.GetProperty("absoluteSalesTotal").GetDecimal()!=e.Total)
                throw new Exception("Monthly source reference mismatch.");
            Console.WriteLine(month);
        }
        foreach (var (field, value) in new[] { ("salesVoucherCount",749), ("headersToAdd",749), ("signedPostingsToAdd",2530), ("cancelledSalesVoucherCount",26), ("invalidActiveVoucherCount",0), ("duplicateSourceGuidRows",0), ("crossCompanyGuidMatches",0), ("unbalancedActiveVoucherCount",0), ("ledgerMappingErrorCount",0) })
            if (result.GetProperty(field).GetInt32()!=value) throw new Exception($"Unexpected {field}");
        if (!result.GetProperty("preview").GetBoolean() || result.GetProperty("applied").GetBoolean() || !result.GetProperty("safeToApply").GetBoolean() || result.GetProperty("validationErrors").GetArrayLength()!=0 || result.GetProperty("monthly").GetArrayLength()!=8 || result.GetProperty("absoluteSalesTotal").GetDecimal()!=36724949.22m)
            throw new Exception("Full preview did not pass.");
        var headers = await db.SalesRegister.AsNoTracking().Where(v=>v.CompanyId==company.Id).ToListAsync();
        var postings = await db.SalesRegisterEntries.AsNoTracking().CountAsync();
        if(headers.Count!=103 || headers.Sum(v=>v.Amount)!=4241929.10m || postings!=250) throw new Exception("Unexpected existing SQL figures.");
        var verification = new { beforeSha256=before, afterSha256=after, unchanged=before==after, existingHeaders=headers.Count, existingPostings=postings, existingTotal=headers.Sum(v=>v.Amount), companyId=company.Id, companyGuid=company.TallyGuid, safeToApply=result.GetProperty("safeToApply").GetBoolean() };
        await File.WriteAllTextAsync("diagnostics/sales-register-ui/cancellation-sql-verification.json",JsonSerializer.Serialize(verification,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(verification));
        Console.WriteLine("PASS: full live read-only preview reconciles; SELECT-only EF interceptor; identical full register snapshots before/after.");
    }

    private sealed class SelectOnlyInterceptor : DbCommandInterceptor
    {
        private static void Check(DbCommand command)
        {
            if (!command.CommandText.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Diagnostic harness permits SELECT only.");
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken=default) { Check(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken=default) => throw new InvalidOperationException("No SQL writes allowed in diagnostics.");
    }
}
