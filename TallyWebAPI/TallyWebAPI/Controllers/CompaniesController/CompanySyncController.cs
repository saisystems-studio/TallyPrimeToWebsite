using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/sync/companies")]
    //[AllowAnonymous] // Temporary for Swagger testing
    [Authorize]
    public class CompanySyncController : ControllerBase
    {
        private readonly CompanySyncService _companySyncService;

        public CompanySyncController(
            CompanySyncService companySyncService)
        {
            _companySyncService = companySyncService;
        }

        [HttpPost]
        public async Task<IActionResult> SyncCompanies()
        {
            try
            {
                var result = await _companySyncService.SyncAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Company sync failed.",
                    error = ex.Message
                });
            }
        }
    }
}