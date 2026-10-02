using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/sync/ledgers")]
    //[AllowAnonymous] // Temporary for Swagger testing
    [Authorize]
    public class LedgerSyncController : ControllerBase
    {
        private readonly LedgerSyncService _ledgerSyncService;

        public LedgerSyncController(
            LedgerSyncService ledgerSyncService)
        {
            _ledgerSyncService = ledgerSyncService;
        }

        [HttpPost]
        public async Task<IActionResult> SyncLedgers()
        {
            try
            {
                var result =
                    await _ledgerSyncService.SyncAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Ledger sync failed.",
                    error = ex.Message
                });
            }
        }
    }
}