using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize]
[AllowAnonymous]
public class StockSummaryController(StockSummaryService service, StockSummarySnapshotService snapshots, AppDbContext db,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStockSummary([FromQuery] int companyId,
        [FromQuery] string? fromDate = null, [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) return BadRequest(new { message = "Please select a company." });
        try
        {
            var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken);
            if (company is null) return NotFound(new { message = "The selected company was not found." });
            var (resolvedFrom, to) = await service.ResolveRequestedPeriodAsync(fromDate, toDate,
                company.BooksFrom, company.Name, cancellationToken);
            var report = await snapshots.GetAsync(company.Id, resolvedFrom, to, cancellationToken);
            if (report is null)
                return NotFound(new { message = $"Stock Summary sync required for {company.Name} for this period ({resolvedFrom} to {to})." });
            return Ok(report);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidDataException ex) { return UnprocessableEntity(new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Cannot resolve the latest accounting date from local TallyPrime. Verify that TallyPrime is open with the selected company, then try again." }); }
    }

    // Financial XML diagnostics are only available in the local Development environment.
    [HttpGet("raw")]
    public async Task<IActionResult> GetRawStockSummary([FromQuery] int companyId,
        [FromQuery] string? fromDate = null, [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (companyId <= 0) return BadRequest(new { message = "Please select a company." });
        try
        {
            var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken);
            if (company is null) return NotFound(new { message = "The selected company was not found." });
            var (resolvedFrom, to) = await service.ResolveRequestedPeriodAsync(fromDate, toDate,
                company.BooksFrom, company.Name, cancellationToken);
            return Content(await service.GetRawStockSummaryAsync(resolvedFrom, to, company.Name, cancellationToken), "application/xml");
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Cannot reach local TallyPrime." }); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return StatusCode(504, new { message = "TallyPrime report request timed out." }); }
    }
}

