using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/sync/stock-items")]
    //[AllowAnonymous]
    [Authorize]
    public class StockItemSyncController : ControllerBase
    {
        private readonly StockItemSyncService _syncService;

        public StockItemSyncController(
            StockItemSyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpPost]
        public async Task<IActionResult> Sync()
        {
            var result = await _syncService.SyncAsync();

            return Ok(result);
        }
    }
}