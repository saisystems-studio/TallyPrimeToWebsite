using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/sync/vouchers")]
    //[AllowAnonymous]
    [Authorize]
    public class VoucherSyncController : ControllerBase
    {
        private readonly VoucherSyncService _voucherSyncService;

        public VoucherSyncController(
            VoucherSyncService voucherSyncService)
        {
            _voucherSyncService = voucherSyncService;
        }

        [HttpPost]
        public async Task<IActionResult> SyncVouchers()
        {
            try
            {
                var result = await _voucherSyncService.SyncAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Voucher sync failed.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("postings/backfill")]
        public async Task<IActionResult> BackfillPostings([FromQuery] int companyId,
            [FromQuery] string fromDate, [FromQuery] string toDate,
            [FromQuery] bool apply = false, [FromQuery] bool salesOnly = false)
        {
            if (companyId <= 0) return BadRequest(new { message = "A single CompanyId is required." });
            try
            {
                return Ok(await _voucherSyncService.BackfillPostingsAsync(
                    companyId, fromDate, toDate, apply, salesOnly));
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }
    }
}
