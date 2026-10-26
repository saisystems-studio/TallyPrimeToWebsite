using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TallyWebAPI.Data;
using TallyWebAPI.Models;
using TallyWebAPI.Services.SalesRegister;
using TallyWebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

internal static class RelationalChecks
{
    public static async Task RunAsync()
    {
        // Hardcoded in-memory SQLite only. No appsettings, environment connection string or live SQL.
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var fault = new Failure();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(fault).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Companies.AddRange(new CompanyEntity {Id=7,Name="Fixture",TallyGuid="c7"},new CompanyEntity {Id=8,Name="Other",TallyGuid="c8"});
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new SalesSyncRepository(db, TimeProvider.System);
        var plan=SalesSyncPlanner.Create(Fixtures.Source(),[],"operator",DateTimeOffset.UtcNow,TimeSpan.FromMinutes(30));
        try{await repository.CommitAsync(plan,plan.Actions);throw new Exception("Unapproved repository batch accepted.");}catch(InvalidOperationException){}
        plan=plan with {ApprovedAt=DateTimeOffset.UtcNow};
        fault.Fail=true;
        try {await repository.CommitAsync(plan,plan.Actions);throw new Exception("Injected save failure was ignored.");} catch(InvalidOperationException ex) when(ex.Message=="Injected save failure") {}
        fault.Fail=false;
        if(await db.SalesRegister.CountAsync()!=0 || await db.SalesRegisterEntries.CountAsync()!=0)throw new Exception("Failed batch was not rolled back.");
        await repository.CommitAsync(plan,plan.Actions);
        var before=await db.SalesRegister.AsNoTracking().OrderBy(v=>v.Id).Select(v=>new {v.Id,v.CreatedAt,v.UpdatedAt}).ToArrayAsync();
        await repository.CommitAsync(plan,plan.Actions); // already committed, safe retry
        if(await db.SalesRegister.CountAsync()!=2 || await db.SalesRegisterEntries.CountAsync()!=4)throw new Exception("Retry duplicated rows.");
        var after=await db.SalesRegister.AsNoTracking().OrderBy(v=>v.Id).Select(v=>new {v.Id,v.CreatedAt,v.UpdatedAt}).ToArrayAsync();
        if(!before.SequenceEqual(after))throw new Exception("Unchanged voucher IDs/timestamps were modified.");
        var otherSource=Fixtures.Source() with {CompanyId=8,Active=Fixtures.Source().Active.Select(v=>v with{CompanyId=8}).ToArray()};
        var otherPlan=SalesSyncPlanner.Create(otherSource,[],"operator",DateTimeOffset.UtcNow,TimeSpan.FromMinutes(30));
        otherPlan=otherPlan with {ApprovedAt=DateTimeOffset.UtcNow};
        await repository.CommitAsync(otherPlan,otherPlan.Actions);
        if((await repository.ReadAsync(7)).Length!=2 || (await repository.ReadAsync(8)).Length!=2)throw new Exception("Company-scoped GUID isolation failed.");
        var changed=Fixtures.Source() with {Active=Fixtures.Source().Active.Select(v=>v with {VoucherNumber="updated-"+v.VoucherNumber}).ToArray()};
        var update=SalesSyncPlanner.Create(changed,await repository.ReadAsync(7),"operator",DateTimeOffset.UtcNow,TimeSpan.FromMinutes(30));
        update=update with {ApprovedAt=DateTimeOffset.UtcNow};
        await repository.CommitAsync(update,update.Actions);
        if((await repository.ReadAsync(7)).Any(v=>!v.VoucherNumber.StartsWith("updated-")) || (await repository.ReadAsync(8)).Any(v=>v.VoucherNumber.StartsWith("updated-")))throw new Exception("Modified update crossed companies.");
        if(await db.SalesRegisterEntries.CountAsync()!=8)throw new Exception("Posting replacement left duplicates or removed other-company postings.");
        var controller=new SalesRegisterController(db,null!);
        var id=await db.SalesRegister.Where(v=>v.CompanyId==7).Select(v=>v.Id).FirstAsync();
        var detail=await controller.Detail(id,7);
        if(detail is not OkObjectResult ok || JsonSerializer.SerializeToElement(ok.Value).GetProperty("voucher").GetProperty("postings").GetArrayLength()!=2)throw new Exception("Stored voucher detail did not return independent postings.");
        if(await controller.Detail(id,8) is not NotFoundObjectResult)throw new Exception("Voucher detail crossed companies.");
        var period=await controller.Get(7,"20200403","20200430");
        if(period is not OkObjectResult filtered || JsonSerializer.SerializeToElement(filtered.Value).GetProperty("totalRecords").GetInt32()!=0)throw new Exception("Date range did not filter SQL vouchers.");
        if(await controller.Get(7,"20200430","20200401") is not BadRequestObjectResult)throw new Exception("Reversed date range accepted.");
        if(await controller.Periods(7) is not OkObjectResult)throw new Exception("Stored period metadata unavailable.");
        Console.WriteLine("PASS: real disposable SQLite transactions roll back failed batches, prevent duplicates, preserve unchanged IDs/timestamps and isolate company updates.");
    }
    sealed class Failure:SaveChangesInterceptor
    {
        public bool Fail;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken token=default)
        {if(Fail)throw new InvalidOperationException("Injected save failure");return ValueTask.FromResult(result);}
    }
}
