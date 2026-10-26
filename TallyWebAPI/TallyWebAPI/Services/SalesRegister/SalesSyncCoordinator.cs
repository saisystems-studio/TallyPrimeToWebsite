using Microsoft.Extensions.Options;

namespace TallyWebAPI.Services.SalesRegister;

public sealed record SalesBatchResult(string PlanId, int Committed, int Remaining, SalesReconciliation Reconciliation);

public sealed class SalesSyncCoordinator(ISalesSyncSource source, ISalesSyncRepository repository, ISalesSyncPlanStore plans,
    TimeProvider clock, IOptions<SalesSyncOptions> settings)
{
    public bool ApplyEnabled => false;
    public async Task<SalesSyncPlan> PreviewAsync(int companyId, string owner, CancellationToken token = default)
    {
        var snapshot = await source.ReadAsync(companyId, token);
        var baseline = await repository.ReadAsync(companyId, token);
        var plan = SalesSyncPlanner.Create(snapshot, baseline, owner, clock.GetUtcNow(), TimeSpan.FromMinutes(Math.Clamp(settings.Value.PreviewMinutes, 1, 120)));
        await plans.SaveAsync(plan, token);
        return plan;
    }
    public Task<SalesSyncPlan> ApproveAsync(string id, string digest, string owner, bool explicitlyApproved, CancellationToken token = default) =>
        Task.FromException<SalesSyncPlan>(new InvalidOperationException("Approval-bound Sales Register synchronization is disabled."));

    public Task<SalesBatchResult> ApplyBatchAsync(string id, string digest, string owner, CancellationToken token = default) =>
        Task.FromException<SalesBatchResult>(new InvalidOperationException("Approval-bound Sales Register synchronization is disabled."));
    private void Validate(SalesSyncPlan plan, string digest, string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || plan.Owner != owner || plan.Digest != digest || SalesSyncPlanner.Digest(plan) != plan.Digest)
            throw new InvalidOperationException("Preview identity/digest does not match this operator and immutable plan.");
        if (clock.GetUtcNow() >= plan.ExpiresAt) throw new InvalidOperationException("Preview expired. Create a fresh preview and approval.");
    }
}
