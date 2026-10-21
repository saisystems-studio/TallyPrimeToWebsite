using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/sync/stock-summary")]
//[Authorize(Roles = "Admin")]
[AllowAnonymous]
public class StockSummarySyncController(StockSummarySnapshotService snapshots) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Sync([FromBody] StockSummarySyncRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.CompanyId <= 0)
            return BadRequest(new { message = "A valid CompanyId is required." });
        try
        {
            return Ok(await snapshots.SyncAsync(request.CompanyId, request.FromDate, request.ToDate, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidDataException ex) { return UnprocessableEntity(new { message = ex.Message }); }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { message = "Cannot reach local TallyPrime. Open the selected company and enable its XML server, then retry." });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "TallyPrime took too long to generate Stock Summary. Please retry." });
        }
    }
}

public sealed record StockSummarySyncRequest(int CompanyId, string? FromDate = null, string? ToDate = null);
