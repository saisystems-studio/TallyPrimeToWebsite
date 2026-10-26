using Microsoft.AspNetCore.DataProtection;
using TallyWebAPI.Services.SalesRegister;

internal static class PlanStoreChecks
{
    public static async Task RunAsync()
    {
        var tempRoot=Path.GetFullPath("tmp");var directory=Path.Combine(tempRoot,"sales-plan-fixture-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var keys=new DirectoryInfo(Path.Combine(directory,"keys"));
            var store=new SalesSyncPlanStore(directory,DataProtectionProvider.Create(keys));
            var plan=SalesSyncPlanner.Create(Fixtures.Source(),[],"operator",DateTimeOffset.UtcNow,TimeSpan.FromMinutes(1));
            await store.SaveAsync(plan);
            if(File.ReadAllText(Path.Combine(directory,plan.Id+".plan")).Contains("Fixture company"))throw new Exception("Plan was stored as plaintext.");
            var restarted=new SalesSyncPlanStore(directory,DataProtectionProvider.Create(keys));
            var restored=await restarted.LoadAsync(plan.Id);
            if(restored.Digest!=plan.Digest || restored.ApprovedAt!=null)throw new Exception("Restart changed plan identity or granted approval.");
            await store.SaveAsync(restored with {ApprovedAt=DateTimeOffset.UtcNow});
            if((await restarted.LoadAsync(plan.Id)).ApprovedAt is null)throw new Exception("Explicit approval was not durable.");
            await using(var lease=await store.LockAsync(plan.Id))
            {
                try{await restarted.LockAsync(plan.Id);throw new Exception("Concurrent plan lease was granted.");}catch(InvalidOperationException){}
            }
            await File.WriteAllTextAsync(Path.Combine(directory,plan.Id+".plan"),"tampered");
            try{await restarted.LoadAsync(plan.Id);throw new Exception("Tampered plan loaded.");}catch(InvalidOperationException){}
            try{await restarted.LoadAsync("../outside");throw new Exception("Plan traversal accepted.");}catch(InvalidOperationException){}
            Console.WriteLine("PASS: protected plan/approval persistence across store restart, exclusive lease, tamper and path rejection.");
        }
        finally {if(!Path.GetFullPath(directory).StartsWith(tempRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture cleanup path");Directory.Delete(directory,true);}
    }
}
