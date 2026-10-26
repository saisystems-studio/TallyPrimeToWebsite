using TallyWebAPI.Services.SalesRegister;

// Fixture mode never loads application settings. The separately invoked diagnostic mode permits SELECT only.
if (args.Length == 2 && args[0] == "--read-only-live") { await ReadOnlyDiscovery.RunAsync(int.Parse(args[1])); return; }
if (args.Length != 0) throw new ArgumentException("Use no arguments for isolated tests, or --read-only-live <companyId> for SELECT-only diagnostics.");
var source = Fixtures.Source();
var plan = SalesSyncPlanner.Create(source, [], "operator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30));
Check(plan.Actions.Length == 2 && plan.Actions.All(a => a.Kind == "Insert"), "Preview must identify exact new vouchers without applying.");
var retained = source.Active[0];
var same = SalesSyncPlanner.Create(source, [retained], "operator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30));
Check(same.Actions.Length == 1, "Unchanged existing vouchers must not be written.");
var changed = retained with { VoucherNumber = "changed" };
var modified = SalesSyncPlanner.Create(source, [changed], "operator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30));
Check(modified.Actions.Count(a => a.Kind == "Update") == 1, "Modified vouchers require an exact update action.");
var cancelled = source with { Active = [source.Active[1]], Cancelled = [new(retained.TallyGuid, retained.Date, retained.VoucherNumber, retained.VoucherType)] };
Check(!SalesSyncPlanner.Create(cancelled, [retained], "operator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30)).SafeToApply, "Stored cancellation must retain and block.");
Check(!SalesSyncPlanner.Create(source with { Active = [source.Active[1]] }, [retained], "operator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30)).SafeToApply, "Missing source must retain and block.");
Check(!SalesSyncPlanner.Create(source with { Active = [retained, retained] }, [], "operator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30)).SafeToApply, "Duplicate source GUIDs must block.");
Console.WriteLine("PASS: deterministic insert/update/unchanged plans, stored cancellation/absence blocks, duplicates.");
await OrchestrationChecks.RunAsync();
await RelationalChecks.RunAsync();
await SourceChecks.RunAsync();
await PlanStoreChecks.RunAsync();

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static class Fixtures
{
    public static SalesSourceSnapshot Source() => new(7, "Fixture company", "company-guid", new(2020, 4, 1), new(2020, 4, 30), ["Sales"],
        [Voucher("voucher-1", "1", 10), Voucher("voucher-2", "2", 20)], [], [], "ledger-context");
    public static SalesVoucher Voucher(string guid, string number, decimal amount) => new(7, guid, number, new(2020, 4, 2), "Sales", "Party", "party-guid", amount,
        [new(0,"party-guid","Party",-amount), new(1,"sales-guid","Sales",amount)]);
}
