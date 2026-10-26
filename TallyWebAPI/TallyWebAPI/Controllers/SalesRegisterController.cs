using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Services;
using TallyWebAPI.Services.SalesRegister;
using System.Security.Claims;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/sales-register")]
[Authorize]
public sealed class SalesRegisterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SalesSyncCoordinator? _coordinator;
    private readonly BoundedSalesImportService? _boundedImport;
    public SalesRegisterController(AppDbContext db, SalesRegisterSyncService sync, SalesSyncCoordinator? coordinator = null, BoundedSalesImportService? boundedImport = null) { _db = db; _coordinator = coordinator; _boundedImport = boundedImport; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int companyId, [FromQuery] string? fromDate = null, [FromQuery] string? toDate = null)
    {
        if (companyId <= 0) return BadRequest(new { message = "A valid CompanyId is required." });
        if ((fromDate is null) != (toDate is null)) return BadRequest(new { message = "Supply both fromDate and toDate, or omit both for All Available Data." });
        DateTime? from = null, to = null;
        if (fromDate is not null && (!DateTime.TryParseExact(fromDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var f) || !DateTime.TryParseExact(toDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var t) || f > t))
            return BadRequest(new { message = "Dates must be an ordered yyyyMMdd range." });
        if (fromDate is not null) { from = DateTime.ParseExact(fromDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture); to = DateTime.ParseExact(toDate!, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture); }
        var q = _db.SalesRegister.AsNoTracking().Where(x => x.CompanyId == companyId);
        if (from.HasValue) q = q.Where(x => x.VoucherDate >= from.Value && x.VoucherDate <= to!.Value);
        var headers = await q.Include(x => x.Entries).OrderBy(x => x.VoucherDate).ThenBy(x => x.Id).ToListAsync();
        var vouchers = headers.Select(x => new
        {
            id = x.Id, companyId = x.CompanyId, guid = x.TallyGuid, tallyGuid = x.TallyGuid,
            date = x.VoucherDate.ToString("yyyyMMdd"), voucherNumber = x.VoucherNumber, voucherType = x.VoucherType,
            partyName = x.PartyLedgerName, particulars = x.PartyLedgerName, partyLedgerGuid = x.PartyLedgerGuid,
            amount = x.Amount, partyAmount = x.Entries.Where(e => e.LedgerGuid == x.PartyLedgerGuid).Sum(e => e.SignedAmount),
            postings = x.Entries.OrderBy(e => e.EntrySequence).Select(e => new { ledgerGuid = e.LedgerGuid, ledgerName = e.LedgerName, amount = e.SignedAmount, entryIndex = e.EntrySequence })
        }).ToList();
        return Ok(new { companyId, totalRecords = vouchers.Count,
            availableFrom = vouchers.Count == 0 ? null : vouchers.Min(x => x.date),
            availableTo = vouchers.Count == 0 ? null : vouchers.Max(x => x.date), vouchers });
    }

    [HttpGet("vouchers/{id:int}")]
    public async Task<IActionResult> Detail(int id, [FromQuery] int companyId)
    {
        if (id <= 0 || companyId <= 0) return BadRequest(new { message = "A valid voucher and company are required." });
        var row = await _db.SalesRegister.AsNoTracking().Include(v => v.Entries).SingleOrDefaultAsync(v => v.Id == id && v.CompanyId == companyId);
        if (row is null) return NotFound(new { message = "No stored Sales Register voucher matches this company and ID." });
        return Ok(new { source = "SalesRegisterSQL", voucher = new {
            id = row.Id, companyId = row.CompanyId, guid = row.TallyGuid, voucherNumber = row.VoucherNumber,
            date = row.VoucherDate.ToString("yyyyMMdd"), voucherType = row.VoucherType, partyName = row.PartyLedgerName,
            partyLedgerGuid = row.PartyLedgerGuid, amount = row.Amount,
            partyAmount = row.Entries.Where(p => p.LedgerGuid == row.PartyLedgerGuid).Sum(p => p.SignedAmount),
            postings = row.Entries.OrderBy(p => p.EntrySequence).Select(p => new { ledgerGuid = p.LedgerGuid, ledgerName = p.LedgerName, amount = p.SignedAmount, entryIndex = p.EntrySequence })
        }});
    }

    [HttpGet("periods")]
    public async Task<IActionResult> Periods([FromQuery] int companyId)
    {
        if (companyId <= 0) return BadRequest(new { message = "A valid company is required." });
        var company = await _db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId);
        if (company is null) return NotFound(new { message = "Company not found." });
        var dates = await _db.SalesRegister.AsNoTracking().Where(v => v.CompanyId == companyId).Select(v => v.VoucherDate).ToListAsync();
        return Ok(new { source = "SalesRegisterSQL", companyId, startingFrom = company.StartingFrom,
            availableFrom = dates.Count == 0 ? null : dates.Min().ToString("yyyyMMdd"), availableTo = dates.Count == 0 ? null : dates.Max().ToString("yyyyMMdd"), totalRecords = dates.Count });
    }

    [HttpPost("sync/preview")]
    public async Task<IActionResult> Preview([FromQuery] int companyId, CancellationToken token) => await Controlled(async () => {
        var plan = await Coordinator.PreviewAsync(companyId, Operator, token);
        return PlanResponse(plan);
    });

    public sealed record ApprovalRequest(string PlanId, string Digest, bool ExplicitlyApproved);
    public sealed record ApplyRequest(string PlanId, string Digest);
    public sealed record BoundedImportPreviewRequest(int CompanyId, string FromDate, string ToDate);

    [Authorize(Roles = "Admin")]
    [HttpPost("sync/approve")]
    public IActionResult Approve([FromBody] ApprovalRequest request, CancellationToken token) =>
        StatusCode(StatusCodes.Status410Gone, new { message = "Approval-bound Sales Register synchronization is disabled." });

    [Authorize(Roles = "Admin")]
    [HttpPost("sync/apply")]
    public IActionResult Apply([FromBody] ApplyRequest request, CancellationToken token) =>
        StatusCode(StatusCodes.Status410Gone, new { message = "Approval-bound Sales Register synchronization is disabled." });

    [HttpPost("import/preview")]
    public async Task<IActionResult> BoundedImportPreview([FromBody] BoundedImportPreviewRequest request, CancellationToken token) => await Controlled(async () => {
        if (!DateTime.TryParseExact(request.FromDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(request.ToDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var to) ||
            from != new DateTime(2024, 5, 1) || to != new DateTime(2024, 12, 31))
            throw new ArgumentException("Only the approved 20240501 through 20241231 range is supported.");
        var result = await (_boundedImport ?? throw new InvalidOperationException("Date-bounded import service is unavailable."))
            .PreviewAsync(request.CompanyId, Operator, token);
        return new {
            preview = true, applyEnabled = true, planId = result.Plan.Id, digest = result.Plan.Digest,
            createdAt = result.Plan.CreatedAt, expiresAt = result.Plan.ExpiresAt, companyId = result.Plan.Source.CompanyId,
            companyName = result.Plan.Source.CompanyName, companyGuid = result.Plan.Source.CompanyGuid,
            fromDate = result.Plan.Source.BooksFrom.ToString("yyyyMMdd"), toDate = result.Plan.Source.Through.ToString("yyyyMMdd"),
            salesVoucherTypes = result.Plan.Source.SalesTypes,
            activeVouchers = result.Plan.Source.Active.Length, cancelledExcluded = result.Plan.Source.Cancelled.Length,
            ledgerPostings = result.Plan.Source.Active.Sum(v => v.Postings.Length), existingGuids = result.ExistingGuids,
            missingGuids = result.Reconciliation.MissingGuids, monthly = result.Reconciliation.Monthly,
            validationErrors = result.ValidationErrors,
            aprilPreservation = new { vouchers = result.AprilVouchers, postings = result.AprilPostings, salesCredit = result.AprilSalesCredit },
            safeToApply = result.Plan.SafeToApply
        };
    });

    [Authorize(Roles = "Admin")]
    [HttpPost("import/apply")]
    public async Task<IActionResult> BoundedImportApply([FromBody] ApplyRequest request, CancellationToken token) => await Controlled(async () => {
        var result = await (_boundedImport ?? throw new InvalidOperationException("Date-bounded import service is unavailable."))
            .ApplyAsync(request.PlanId, request.Digest, Operator, token);
        return new { applied = true, result.PlanId, result.InsertedVouchers, result.InsertedPostings,
            reconciliation = result.Reconciliation,
            aprilPreservation = new { vouchers = result.AprilVouchers, postings = result.AprilPostings, salesCredit = result.AprilSalesCredit } };
    });

    private SalesSyncCoordinator Coordinator => _coordinator ?? throw new InvalidOperationException("Approval-bound workflow is unavailable.");
    private string Operator => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated operator identity is required.");
    private object PlanResponse(SalesSyncPlan plan) => new {
        preview = plan.ApprovedAt is null, applied = false, planId = plan.Id, plan.Digest, plan.CreatedAt, plan.ExpiresAt,
        companyId = plan.Source.CompanyId, companyName = plan.Source.CompanyName, booksFrom = plan.Source.BooksFrom, through = plan.Source.Through,
        validationReady = plan.SafeToApply, approvalGranted = plan.ApprovedAt.HasValue, applyEnabled = Coordinator.ApplyEnabled,
        safeToApply = plan.SafeToApply && plan.ApprovedAt.HasValue && Coordinator.ApplyEnabled && DateTimeOffset.UtcNow < plan.ExpiresAt,
        plan.Blockers, plan.Actions, reconciliation = SalesReconciliation.Compare(plan.Source, plan.Baseline)
    };
    private async Task<IActionResult> Controlled(Func<Task<object>> operation)
    {
        try { return Ok(await operation()); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Tally is unavailable. No synchronization was started." }); }
    }
}
