using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/gst-registration")]
    //[AllowAnonymous] // Temporary for testing
    [Authorize]
    public class GstRegistrationController : ControllerBase
    {
        private readonly TallyService _tallyService;

        public GstRegistrationController(TallyService tallyService)
        {
            _tallyService = tallyService;
        }

        [HttpGet("raw")]
        public async Task<IActionResult> GetRaw([FromQuery] string companyName)
        {
            try
            {
                var xml = await _tallyService
                    .GetGstRegistrationsAsync(companyName);

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unable to fetch GST registration from Tally.",
                    error = ex.Message
                });
            }
        }
    }
}