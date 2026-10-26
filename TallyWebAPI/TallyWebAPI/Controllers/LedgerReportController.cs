
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Data;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LedgerReportController : ControllerBase
    {
        private readonly LedgerReportSnapshotService _snapshots;

        public LedgerReportController(
            LedgerReportSnapshotService snapshots)
        {
            _snapshots = snapshots;
        }

        // ==============================================
        // GET LEDGER REPORT
        // Dates are optional
        // ==============================================

        [HttpGet]
        public async Task<IActionResult> GetLedgerReport(
            [FromQuery] int companyId,
            [FromQuery] int ledgerId,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            if (companyId <= 0 || ledgerId <= 0)
            {
                return BadRequest(new
                {
                    message =
                        "CompanyId and LedgerId are required."
                });
            }

            try
            {
                var report = await _snapshots.GetAsync(
                    companyId,
                    ledgerId,
                    fromDate,
                    toDate,
                    HttpContext.RequestAborted);

                if (report is null)
                {
                    return NotFound(new
                    {
                        message =
                            "No validated Ledger Report snapshot " +
                            "covers the requested date range. " +
                            "Please sync the required period " +
                            "from TallyPrime."
                    });
                }

                return Ok(report);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    message =
                        "Unable to fetch Ledger Report."
                });
            }
        }
    }
}
