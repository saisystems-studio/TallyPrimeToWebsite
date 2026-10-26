using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace TallyWebAPI.Services.SalesRegister;

public sealed class SalesSyncPlanStore(string directory, IDataProtectionProvider protection) : ISalesSyncPlanStore
{
    private readonly IDataProtector protector = protection.CreateProtector("SalesRegister.ApprovalPlans.v1");
    private string PathFor(string id, string suffix)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidOperationException("Invalid plan ID.");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, id + suffix);
    }
    public async Task SaveAsync(SalesSyncPlan plan, CancellationToken token = default)
    {
        if (SalesSyncPlanner.Digest(plan) != plan.Digest) throw new InvalidOperationException("Plan digest is invalid.");
        var path = PathFor(plan.Id, ".plan"); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(protector.Protect(JsonSerializer.Serialize(plan)).AsMemory(), token);
                await writer.FlushAsync(token); stream.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public async Task<SalesSyncPlan> LoadAsync(string id, CancellationToken token = default)
    {
        try
        {
            var plan = JsonSerializer.Deserialize<SalesSyncPlan>(protector.Unprotect(await File.ReadAllTextAsync(PathFor(id, ".plan"), token)))
                ?? throw new InvalidOperationException("Empty plan.");
            if (plan.Id != id || SalesSyncPlanner.Digest(plan) != plan.Digest) throw new InvalidOperationException("Plan content changed.");
            return plan;
        }
        catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException or JsonException)
        { throw new InvalidOperationException("Plan is missing, unreadable or no longer decryptable. Create a fresh preview.", ex); }
    }
    public Task<IAsyncDisposable> LockAsync(string id, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        try { return Task.FromResult<IAsyncDisposable>(new FileStream(PathFor(id, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException ex) { throw new InvalidOperationException("This plan is already being processed. Retry after the active request finishes.", ex); }
    }
}
