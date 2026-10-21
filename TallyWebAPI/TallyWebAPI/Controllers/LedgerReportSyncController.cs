using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/sync/ledger-report")]
[Authorize(Roles = "Admin")]
public sealed class LedgerReportSyncController(LedgerReportSnapshotService snapshots) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Sync([FromBody] LedgerReportSyncRequest? request, CancellationToken token)
    {
        if (request is null || request.CompanyId <= 0 || request.LedgerId <= 0)
            return BadRequest(new { message = "Valid CompanyId and LedgerId are required." });
        try { return Ok(await snapshots.SyncAsync(request.CompanyId, request.LedgerId, request.FromDate, request.ToDate, request.ReplaceExisting, token)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidDataException ex) { return UnprocessableEntity(new { message = ex.Message }); }
        catch (SnapshotAlreadyExistsException ex) { return Conflict(new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Cannot reach local TallyPrime. Open the selected company and enable its XML server, then retry." }); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return StatusCode(504, new { message = "TallyPrime took too long to generate the Ledger Report. Retry later." }); }
    }
}

public sealed record LedgerReportSyncRequest(int CompanyId, int LedgerId, string? FromDate = null, string? ToDate = null, bool ReplaceExisting = false);
