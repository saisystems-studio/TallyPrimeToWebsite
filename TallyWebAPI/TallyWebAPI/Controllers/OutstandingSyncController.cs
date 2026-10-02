using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/sync/outstanding")]
    //[AllowAnonymous]
    [Authorize]
    public class OutstandingSyncController : ControllerBase
    {
        private readonly OutstandingSyncService _syncService;

        public OutstandingSyncController(
            OutstandingSyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpPost]
        public async Task<IActionResult> Sync()
        {
            try
            {
                var result = await _syncService.SyncAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to sync outstanding data.",
                    error = ex.Message
                });
            }
        }
    }
}