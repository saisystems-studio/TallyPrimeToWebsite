using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[AllowAnonymous
    [Authorize]
    public class SalesReportController : ControllerBase
    {
        private readonly SalesReportService _salesReportService;

        public SalesReportController(
            SalesReportService salesReportService)
        {
            _salesReportService = salesReportService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] string fromDate,
            [FromQuery] string toDate)
        {
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
                    await _salesReportService.GetSalesReportAsync(
                        fromDate,
                        toDate
                    );

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch Sales Report.",
                    error = ex.Message
                });
            }
        }



        [HttpGet("ledger-balance-test")]
        public async Task<IActionResult> LedgerBalanceTest(
        [FromQuery] string ledgerName,
        [FromQuery] string fromDate,
        [FromQuery] string toDate)
        {
            try
            {
                var result =
                    await _salesReportService.GetLedgerBalanceTestAsync(
                        ledgerName,
                        fromDate,
                        toDate
                    );

                return Content(
                    result,
                    "application/xml",
                    System.Text.Encoding.UTF8
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message
                });
            }
        }
    }
}