using Microsoft.Extensions.Options;
using TallyWebAPI.Services.SalesRegister;

internal static class OrchestrationChecks
{
    public static async Task RunAsync()
    {
        var clock = new Clock(); var source = new Source(); var repository = new Repository(); var store = new Store();
        SalesSyncCoordinator Coordinator(bool enabled = true, int batch = 1) => new(source, repository, store, clock,
            Options.Create(new SalesSyncOptions { ApplyEnabled = enabled, BatchSize = batch }));
        var sync = Coordinator();
        var preview = await sync.PreviewAsync(7, "operator");
        await Reject(() => sync.ApplyBatchAsync(preview.Id, preview.Digest, "operator"), "Unapproved apply");
        await Reject(() => sync.ApproveAsync(preview.Id, preview.Digest, "other", true), "Foreign operator");
        await Reject(() => sync.ApproveAsync(preview.Id, "changed", "operator", true), "Changed digest");
        await Reject(() => sync.ApproveAsync(preview.Id, preview.Digest, "operator", false), "Missing explicit approval");
        Check(repository.Rows.Length == 0, "Rejected approval paths must not insert.");
        await sync.ApproveAsync(preview.Id, preview.Digest, "operator", true);
        await Reject(() => Coordinator(false).ApplyBatchAsync(preview.Id, preview.Digest, "operator"), "Disabled apply");
        source.Snapshot = source.Snapshot with { ContextFingerprint = "changed mapping" };
        await Reject(() => sync.ApplyBatchAsync(preview.Id, preview.Digest, "operator"), "Source mapping drift");
        source.Snapshot = Fixtures.Source();
        repository.Rows = [Fixtures.Voucher("unapproved", "x", 1)];
        await Reject(() => sync.ApplyBatchAsync(preview.Id, preview.Digest, "operator"), "Unexpected SQL row");
        repository.Rows = [];
        var first = await sync.ApplyBatchAsync(preview.Id, preview.Digest, "operator");
        Check(repository.Rows.Length == 1 && first.Remaining == 1, "One request must commit only one bounded batch.");
        var second = await Coordinator().ApplyBatchAsync(preview.Id, preview.Digest, "operator");
        Check(repository.Rows.Length == 2 && second.Remaining == 0 && second.Reconciliation.Matches, "A restarted coordinator must resume and reconcile.");
        await sync.ApplyBatchAsync(preview.Id, preview.Digest, "operator");
        Check(repository.Rows.Length == 2, "Repeat apply must not duplicate vouchers.");
        repository.Rows = [Fixtures.Voucher("voucher-1", "old", 10), Fixtures.Source().Active[1]];
        var modified = await sync.PreviewAsync(7,"operator");
        await sync.ApproveAsync(modified.Id,modified.Digest,"operator",true);
        await sync.ApplyBatchAsync(modified.Id,modified.Digest,"operator");
        Check(repository.Rows.Single(v => v.TallyGuid == "voucher-1").VoucherNumber == "1", "Verified modifications must update stored content.");
        repository.Rows = []; var crashPlan = await sync.PreviewAsync(7,"operator");
        await sync.ApproveAsync(crashPlan.Id,crashPlan.Digest,"operator",true);
        repository.ThrowAfterCommit = true;
        await Reject(() => sync.ApplyBatchAsync(crashPlan.Id,crashPlan.Digest,"operator"), "Interrupted acknowledgement");
        repository.ThrowAfterCommit = false;
        await sync.ApplyBatchAsync(crashPlan.Id,crashPlan.Digest,"operator");
        Check(repository.Rows.Length == 2, "Retry after commit/acknowledgement interruption must infer progress without duplicates.");
        clock.Now = clock.Now.AddHours(1);
        await Reject(() => sync.ApplyBatchAsync(crashPlan.Id,crashPlan.Digest,"operator"), "Expired preview");
        var foreign = Fixtures.Source() with { CompanyId = 8, Active = Fixtures.Source().Active.Select(v => v with { CompanyId = 8 }).ToArray() };
        Check(SalesSyncPlanner.Create(foreign, [],"operator",clock.Now,TimeSpan.FromMinutes(1)).SafeToApply, "A GUID may exist independently under another company.");
        Console.WriteLine("PASS: approval/digest/owner/expiry/enablement gates, source/SQL drift, bounded insert, update, retry and interrupted acknowledgement.");
    }
    static async Task Reject(Func<Task> operation, string name) { try { await operation(); } catch (InvalidOperationException) { return; } throw new Exception(name+" was not rejected."); }
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    sealed class Source : ISalesSyncSource { public SalesSourceSnapshot Snapshot=Fixtures.Source(); public Task<SalesSourceSnapshot> ReadAsync(int id,CancellationToken token=default) { if(id!=Snapshot.CompanyId)throw new Exception("Wrong company");return Task.FromResult(Snapshot); } }
    sealed class Store : ISalesSyncPlanStore
    {
        readonly Dictionary<string,SalesSyncPlan> plans=[];
        public Task SaveAsync(SalesSyncPlan p,CancellationToken t=default){plans[p.Id]=p;return Task.CompletedTask;}
        public Task<SalesSyncPlan> LoadAsync(string id,CancellationToken t=default)=>Task.FromResult(plans[id]);
        public Task<IAsyncDisposable> LockAsync(string id,CancellationToken t=default)=>Task.FromResult<IAsyncDisposable>(new Lease());
        sealed class Lease:IAsyncDisposable{public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
    sealed class Repository : ISalesSyncRepository
    {
        public SalesVoucher[] Rows=[];public bool ThrowAfterCommit;
        public Task<SalesVoucher[]> ReadAsync(int id,CancellationToken t=default)=>Task.FromResult(Rows.Where(v=>v.CompanyId==id).ToArray());
        public Task CommitAsync(SalesSyncPlan p,SalesSyncAction[] batch,CancellationToken t=default)
        {
            var pending=SalesSyncPlanner.Pending(p,Rows); if(batch.Any(a=>!pending.Contains(a)))throw new InvalidOperationException("Unexpected batch");
            Rows=Rows.Where(v=>!batch.Any(a=>a.Guid==v.TallyGuid)).Concat(batch.Select(a=>a.After)).ToArray();
            if(ThrowAfterCommit)throw new InvalidOperationException("Simulated interruption after atomic commit.");
            return Task.CompletedTask;
        }
    }
}
