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
    }
}