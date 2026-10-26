using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TallyWebAPI.Data;
using TallyWebAPI.Services.SalesRegister;

internal static class ReadOnlyDiscovery
{
    public static async Task RunAsync(int companyId)
    {
        var directory="diagnostics/sales-register-safe-sync";Directory.CreateDirectory(directory);
        using var settings=JsonDocument.Parse(await File.ReadAllTextAsync("TallyWebAPI/TallyWebAPI/appsettings.json"));
        var connectionString=settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
        var sql=new SqlConnectionStringBuilder(connectionString);var host=sql.DataSource.Split('\\')[0].Split(',')[0];
        if(!new[]{".","localhost","127.0.0.1","(local)","(localdb)",Environment.MachineName}.Contains(host,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Local SQL only.");
        var uri=new Uri(settings.RootElement.GetProperty("Tally").GetProperty("BaseUrl").GetString()!);
        if(!uri.IsLoopback)throw new InvalidOperationException("Local Tally only.");
        var guard=new SelectOnly();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).AddInterceptors(guard).Options);
        async Task<string> Snapshot()
        {
            var connection=db.Database.GetDbConnection();if(connection.State!=System.Data.ConnectionState.Open)await connection.OpenAsync();
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT (SELECT * FROM dbo.SalesRegister_tbl ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) + (SELECT * FROM dbo.SalesRegisterEntries_tbl ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((string)(await command.ExecuteScalarAsync())!)));
        }
        var before=await Snapshot();
        using var http=new HttpClient(new ProgressHandler()){BaseAddress=uri,Timeout=TimeSpan.FromSeconds(90)};
        var source=await new SalesSyncSource(db,http).ReadAsync(companyId);
        var repository=new SalesSyncRepository(db,TimeProvider.System);
        var stored=await repository.ReadAsync(companyId);
        var plan=SalesSyncPlanner.Create(source,stored,"read-only-diagnostic-not-approved",DateTimeOffset.UtcNow,TimeSpan.FromMinutes(30));
        var reconciliation=SalesReconciliation.Compare(source,stored);
        // Repeated planning over the same captured source plus freshly read SQL must yield the same exact actions.
        var repeated=SalesSyncPlanner.Create(source,await repository.ReadAsync(companyId),"read-only-diagnostic-not-approved",DateTimeOffset.UtcNow,TimeSpan.FromMinutes(30));
        var repeatUnchanged=SalesSyncPlanner.Hash(plan.Actions.Select(a=>new{a.Kind,a.Guid,Content=SalesSyncPlanner.VoucherHash(a.After)}))==SalesSyncPlanner.Hash(repeated.Actions.Select(a=>new{a.Kind,a.Guid,Content=SalesSyncPlanner.VoucherHash(a.After)}));
        var after=await Snapshot();
        var years=reconciliation.Monthly.GroupBy(m=>int.Parse(m.Month[..4])-(int.Parse(m.Month.Substring(4,2))<4?1:0)).Select(g=>new {
            financialYear=$"{g.Key}-{g.Key+1}",active=g.Sum(m=>m.SourceVouchers),stored=g.Sum(m=>m.SqlVouchers),postings=g.Sum(m=>m.SourcePostings),
            cancelled=g.Sum(m=>m.CancelledExcluded),debit=g.Sum(m=>m.SourceDebit),credit=g.Sum(m=>m.SourceCredit),closing=g.Sum(m=>m.SourceCredit-m.SourceDebit)
        }).ToArray();
        var result=new {generatedAt=DateTimeOffset.UtcNow,preview=true,applied=false,source.CompanyId,source.CompanyName,source.CompanyGuid,source.BooksFrom,source.Through,
            earliestSales=source.Active.Length==0?(DateTime?)null:source.Active.Min(v=>v.Date),latestSales=source.Active.Length==0?(DateTime?)null:source.Active.Max(v=>v.Date),
            validationReady=plan.SafeToApply,approvalGranted=false,applyEnabled=false,safeToApply=false,plan.Blockers,source.Errors,
            inserts=plan.Actions.Count(a=>a.Kind=="Insert"),updates=plan.Actions.Count(a=>a.Kind=="Update"),unchanged=stored.Length-plan.Actions.Count(a=>a.Kind=="Update"),
            insertPostings=plan.Actions.Where(a=>a.Kind=="Insert").Sum(a=>a.After.Postings.Length),updatePostings=plan.Actions.Where(a=>a.Kind=="Update").Sum(a=>a.After.Postings.Length),
            financialYears=years,reconciliation,cancelled=source.Cancelled,sourceFingerprint=plan.SourceFingerprint,planDigest=plan.Digest,expiresAt=plan.ExpiresAt,
            beforeSha256=before,afterSha256=after,sqlUnchanged=before==after,repeatActionsUnchanged=repeatUnchanged,selectCommands=guard.Count,
            planId=plan.Id,approvalPlanPersisted=false,note="Diagnostic manifest only; not registered or approved in the application plan store. Fresh application preview/approval required before future apply."};
        var json=new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
        await File.WriteAllTextAsync($"{directory}/reconciliation.json",JsonSerializer.Serialize(result,json));
        await File.WriteAllTextAsync($"{directory}/exact-actions.json",JsonSerializer.Serialize(plan.Actions,json));
        await File.WriteAllTextAsync($"{directory}/validated-source.json",JsonSerializer.Serialize(source,json));
        Console.WriteLine(JsonSerializer.Serialize(new{result.validationReady,result.safeToApply,result.inserts,result.updates,result.insertPostings,result.unchanged,result.sqlUnchanged,result.repeatActionsUnchanged,financialYears=years,plan.Blockers},json));
        if(before!=after || !repeatUnchanged || !plan.SafeToApply)throw new InvalidOperationException("Read-only final verification failed; inspect reconciliation.json. No apply is permitted.");
    }
    sealed class SelectOnly:DbCommandInterceptor
    {
        public int Count;
        void Check(DbCommand command){if(command.CommandType!=System.Data.CommandType.Text || !command.CommandText.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Live diagnostics permit SELECT only.");Count++;}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> r,CancellationToken t=default){Check(c);return ValueTask.FromResult(r);}
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<object> r,CancellationToken t=default){Check(c);return ValueTask.FromResult(r);}
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<int> r,CancellationToken t=default)=>throw new InvalidOperationException("Live SQL writes are prohibited.");
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> r){Check(c);return r;}
        public override InterceptionResult<object> ScalarExecuting(DbCommand c,CommandEventData d,InterceptionResult<object> r){Check(c);return r;}
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c,CommandEventData d,InterceptionResult<int> r)=>throw new InvalidOperationException("Live SQL writes are prohibited.");
    }
    sealed class ProgressHandler:DelegatingHandler
    {
        int count; public ProgressHandler():base(new HttpClientHandler()){}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        { Console.WriteLine($"Read-only Tally export {++count}");return await base.SendAsync(request,token); }
    }
}
