using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/outstanding")]
    //[AllowAnonymous]
    [Authorize]
    public class OutstandingController : ControllerBase
    {
        private readonly OutstandingService _outstandingService;

        public OutstandingController(
            OutstandingService outstandingService)
        {
            _outstandingService = outstandingService;
        }


        // =========================================================
        // LEDGER OUTSTANDING RAW
        // =========================================================
        [HttpGet("raw")]
        public async Task<IActionResult> GetRaw(
            [FromQuery] string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName))
            {
                return BadRequest(new
                {
                    message = "Company name is required."
                });
            }

            try
            {
                var xml = await _outstandingService
                    .GetOutstandingRawAsync(companyName);

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch outstanding from Tally.",
                    error = ex.Message
                });
            }
        }


        // =========================================================
        // OPENING BALANCE / OPENING BILL RAW TEST
        // =========================================================
        [HttpGet("opening-bills-raw")]
        public async Task<IActionResult> GetOpeningBillsRaw(
            [FromQuery] string companyName,
            [FromQuery] string ledgerName)
        {
            if (string.IsNullOrWhiteSpace(companyName) ||
                string.IsNullOrWhiteSpace(ledgerName))
            {
                return BadRequest(new
                {
                    message =
                        "Company name and ledger name are required."
                });
            }

            try
            {
                var xml = await _outstandingService
                    .GetOpeningBillsRawAsync(
                        companyName,
                        ledgerName
                    );

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message =
                        "Unable to fetch opening bill details from Tally.",
                    error = ex.Message
                });
            }
        }


        // =========================================================
        // VOUCHER + BILL ALLOCATION RAW
        // =========================================================
        [HttpGet("voucher-bills-raw")]
        public async Task<IActionResult> GetVoucherBillsRaw(
            [FromQuery] string companyName,
            [FromQuery] string fromDate,
            [FromQuery] string toDate)
        {
            if (string.IsNullOrWhiteSpace(companyName) ||
                string.IsNullOrWhiteSpace(fromDate) ||
                string.IsNullOrWhiteSpace(toDate))
            {
                return BadRequest(new
                {
                    message =
                        "Company name, from date and to date are required."
                });
            }

            try
            {
                var xml = await _outstandingService
                    .GetVoucherBillsRawAsync(
                        companyName,
                        fromDate,
                        toDate
                    );

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message =
                        "Unable to fetch voucher bill allocations.",
                    error = ex.Message
                });
            }
        }


        // =========================================================
        // PARSED OUTSTANDING LIST
        // =========================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetOutstandingList(
            [FromQuery] string companyName,
            [FromQuery] string fromDate,
            [FromQuery] string toDate)
        {
            if (string.IsNullOrWhiteSpace(companyName) ||
                string.IsNullOrWhiteSpace(fromDate) ||
                string.IsNullOrWhiteSpace(toDate))
            {
                return BadRequest(new
                {
                    message =
                        "Company name, from date and to date are required."
                });
            }

            try
            {
                var data = await _outstandingService
                    .GetOutstandingAsync(
                        companyName,
                        fromDate,
                        toDate
                    );

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message =
                        "Unable to fetch outstanding data.",
                    error = ex.Message
                });
            }
        }
    }
}