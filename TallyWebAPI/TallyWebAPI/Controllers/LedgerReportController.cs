using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[AllowAnonymous]
    [Authorize]
    public class LedgerReportController : ControllerBase
    {
        private readonly LedgerReportService _ledgerReportService;

        public LedgerReportController(
            LedgerReportService ledgerReportService)
        {
            _ledgerReportService = ledgerReportService;
        }

        [HttpGet]
        public async Task<IActionResult> GetLedgerReport(
            [FromQuery] string ledgerName,
            [FromQuery] string fromDate,
            [FromQuery] string toDate)
        {
            if (string.IsNullOrWhiteSpace(ledgerName))
            {
                return BadRequest(new
                {
                    message = "Ledger Name is required."
                });
            }

            if (string.IsNullOrWhiteSpace(fromDate) ||
                string.IsNullOrWhiteSpace(toDate))
            {
                return BadRequest(new
                {
                    message = "From Date and To Date are required."
                });
            }

            try
            {
                var report =
                    await _ledgerReportService.GetLedgerReportAsync(
                        ledgerName,
                        fromDate,
                        toDate
                    );

                return Ok(report);
            }
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