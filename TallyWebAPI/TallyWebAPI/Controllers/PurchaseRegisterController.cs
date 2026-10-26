using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Services.PurchaseRegister;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/purchase-register")]
[Authorize]
public sealed class PurchaseRegisterController(AppDbContext db, PurchaseImportCoordinator coordinator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int companyId, [FromQuery] string? fromDate = null, [FromQuery] string? toDate = null, CancellationToken token = default)
    {
        if (companyId <= 0) return BadRequest(new { message = "A valid CompanyId is required." });
        if ((fromDate is null) != (toDate is null)) return BadRequest(new { message = "Supply both fromDate and toDate, or omit both." });
        DateTime? from = null, to = null;
        if (fromDate is not null && (!DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ||
            !DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) || f > t))
            return BadRequest(new { message = "Dates must be an ordered yyyyMMdd range." });
        if (fromDate is not null) { from = DateTime.ParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture); to = DateTime.ParseExact(toDate!, "yyyyMMdd", CultureInfo.InvariantCulture); }
        var query = db.PurchaseRegister.AsNoTracking().Where(v => v.CompanyId == companyId);
        if (from.HasValue) query = query.Where(v => v.VoucherDate >= from.Value && v.VoucherDate <= to!.Value);
        var rows = await query.Include(v => v.Entries).OrderBy(v => v.VoucherDate).ThenBy(v => v.Id).ToListAsync(token);
        var vouchers = rows.Select(v => new
        {
            id = v.Id, companyId = v.CompanyId, guid = v.TallyGuid, tallyGuid = v.TallyGuid,
            date = v.VoucherDate.ToString("yyyyMMdd"), voucherNumber = v.VoucherNumber, voucherType = v.VoucherType,
            partyName = v.PartyLedgerName, partyLedgerGuid = v.PartyLedgerGuid,
            purchaseLedgerNames = v.PurchaseLedgerNames, purchaseLedgerGuids = v.PurchaseLedgerGuids,
            amount = v.Amount, debit = v.Debit, credit = v.Credit, narration = v.Narration,
            postings = v.Entries.OrderBy(p => p.EntrySequence).Select(p => new
            { ledgerGuid = p.LedgerGuid, ledgerName = p.LedgerName, amount = p.SignedAmount, entryIndex = p.EntrySequence, isPurchaseLedger = p.IsPurchaseLedger })
        }).ToArray();
        return Ok(new { companyId, totalRecords = vouchers.Length,
            availableFrom = vouchers.Length == 0 ? null : vouchers.Min(v => v.date),
            availableTo = vouchers.Length == 0 ? null : vouchers.Max(v => v.date), vouchers });
    }

    [HttpGet("periods")]
    public async Task<IActionResult> Periods([FromQuery] int companyId, CancellationToken token)
    {
        if (companyId <= 0) return BadRequest(new { message = "A valid CompanyId is required." });
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId, token);
        if (company is null) return NotFound(new { message = "Company not found." });
        var dates = await db.PurchaseRegister.AsNoTracking().Where(v => v.CompanyId == companyId).Select(v => v.VoucherDate).ToListAsync(token);
        return Ok(new { source = "PurchaseRegisterSQL", companyId, startingFrom = company.StartingFrom,
            availableFrom = dates.Count == 0 ? null : dates.Min().ToString("yyyyMMdd"),
            availableTo = dates.Count == 0 ? null : dates.Max().ToString("yyyyMMdd"), totalRecords = dates.Count });
    }

    [HttpPost("import/preview")]
    public async Task<IActionResult> Preview([FromBody] PurchasePreviewRequest request, CancellationToken token)
    {
        if (!DateTime.TryParseExact(request.FromDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(request.ToDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) || from > to)
            return BadRequest(new { message = "Supply an ordered yyyyMMdd date range." });
        try
        {
            var result = await coordinator.PreviewAsync(request.CompanyId, from, to, OperatorId, token);
            var plan = result.Plan;
            return Ok(new
            {
                preview = true, planId = plan.Id, digest = plan.Digest, createdAt = plan.CreatedAt, expiresAt = plan.ExpiresAt,
                companyId = plan.Source.CompanyId, companyName = plan.Source.CompanyName, companyGuid = plan.Source.CompanyGuid,
                fromDate = plan.Source.From.ToString("yyyyMMdd"), toDate = plan.Source.To.ToString("yyyyMMdd"),
                purchaseVoucherTypes = plan.Source.PurchaseTypes, activeVouchers = plan.Source.Active.Length,
                cancelledExcluded = plan.Source.Cancelled.Length, ledgerPostings = plan.Source.Active.Sum(v => v.Postings.Length),
                existingVouchers = result.Reconciliation.SqlVouchers, missingGuids = result.Reconciliation.MissingGuids,
                reconciliation = result.Reconciliation, validationErrors = plan.Source.Errors, safeToApply = plan.SafeToApply
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Tally is unavailable. Purchase preview could not be completed." }); }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("import/apply")]
    public async Task<IActionResult> Apply([FromBody] PurchaseApplyRequest request, CancellationToken token)
    {
        try
        {
            var result = await coordinator.ApplyAsync(request.PlanId, request.Digest, OperatorId, token);
            return Ok(new { applied = true, result.PlanId, result.InsertedVouchers, result.InsertedPostings, reconciliation = result.Reconciliation });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Tally could not be revalidated; no Purchase apply completed." }); }
    }

    private string OperatorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated operator identity is required.");
}
