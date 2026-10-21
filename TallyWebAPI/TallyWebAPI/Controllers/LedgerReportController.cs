using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Data;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[AllowAnonymous]
    [Authorize]
    public class LedgerReportController : ControllerBase
    {
        private readonly LedgerReportSnapshotService _snapshots;
        private readonly AppDbContext _db;
        private readonly StockSummaryService _periods;

        public LedgerReportController(
            LedgerReportSnapshotService snapshots, AppDbContext db, StockSummaryService periods)
        {
            _snapshots = snapshots;
            _db = db;
            _periods = periods;
        }

        [HttpGet]
        public async Task<IActionResult> GetLedgerReport(
            [FromQuery] int companyId,
            [FromQuery] int ledgerId,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            if (companyId <= 0 || ledgerId <= 0)
            {
                return BadRequest(new { message = "CompanyId and LedgerId are required." });
            }
            try
            {
                var report = await _snapshots.GetAsync(companyId, ledgerId, fromDate, toDate, HttpContext.RequestAborted);
                if (report is null)
                {
                    var company = await _db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId, HttpContext.RequestAborted);
                    if (company is null) return NotFound(new { message = "The selected company was not found." });
                    var (from, to) = await _periods.ResolveRequestedPeriodAsync(fromDate, toDate, company.BooksFrom, company.Name, HttpContext.RequestAborted);
                    return NotFound(new { message = $"Sync Required: Ledger Report snapshot is missing for this company, ledger, and period ({from} to {to}). Ask an administrator to sync it." });
                }
                return Ok(report);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch Ledger Report.",
                    error = ex.Message
                });
            }
        }
    }
}
