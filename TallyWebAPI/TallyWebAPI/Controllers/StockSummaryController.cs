using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[AllowAnonymous] // Temporary only for Swagger testing
    [Authorize]
    public class StockSummaryController : ControllerBase
    {
        private readonly StockSummaryService _stockSummaryService;

        public StockSummaryController(
            StockSummaryService stockSummaryService)
        {
            _stockSummaryService = stockSummaryService;
        }

        [HttpGet("raw")]
        public async Task<IActionResult> GetRawStockSummary(string fromDate, string toDate, string companyName)
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
                var xml = await _stockSummaryService.GetRawStockSummaryAsync(fromDate, toDate, companyName);

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch Stock Summary from Tally.",
                    error = ex.Message
                });
            }
        }


        [HttpGet("movements-raw")]
        public async Task<IActionResult> GetRawStockMovements(
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
                var xml =
                    await _stockSummaryService.GetRawStockMovementsAsync(
                        fromDate,
                        toDate
                    );

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch Stock Movements from Tally.",
                    error = ex.Message
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetStockSummary(string fromDate, string toDate, string companyName)
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
                var report = await _stockSummaryService.GetStockSummaryAsync(fromDate,toDate,companyName);

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch Stock Summary.",
                    error = ex.Message
                });
            }
        }
    }
}