using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;
using Microsoft.AspNetCore.Authorization;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TallyController : ControllerBase
    {
        private readonly TallyService _tallyService;

        public TallyController(TallyService tallyService)
        {
            _tallyService = tallyService;
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            try
            {
                var xml = await _tallyService.GetCompaniesAsync();

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Tally communication failed.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("current-company")]
        public async Task<IActionResult> GetCurrentCompany()
        {
            try
            {
                var company = await _tallyService.GetCurrentCompanyAsync();

                if (company == null)
                {
                    return NotFound(new
                    {
                        connected = true,
                        message = "No company is currently available in Tally."
                    });
                }

                return Ok(new
                {
                    connected = true,
                    company
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to read current Tally company.",
                    error = ex.Message
                });
            }
        }


        [HttpGet("ledgers-raw")]
        public async Task<IActionResult> GetLedgersRaw()
        {
            try
            {
                var xml = await _tallyService.GetLedgersAsync();

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch ledgers from Tally.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("company-raw")]
        public async Task<IActionResult> GetCompanyRaw()
        {
            try
            {
                var xml = await _tallyService.GetCompaniesAsync();

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch company data from Tally.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("gst-registrations-raw")]
        public async Task<IActionResult> GetGstRegistrationsRaw()
        {
            try
            {
                var xml = await _tallyService.GetGstRegistrationsAsync();

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch GST registration details from Tally.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("ledgers")]
        public async Task<IActionResult> GetLedgers()
        {
            try
            {
                var ledgers = await _tallyService.GetLedgerListAsync();

                return Ok(new
                {
                    connected = true,
                    count = ledgers.Count,
                    ledgers
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("===== LEDGER ERROR =====");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("========================");

                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch ledger details from Tally.",
                    error = ex.Message,
                    details = ex.ToString()
                });
            }
        }


        // =========================================================
        // STOCK ITEMS RAW DATA
        // =========================================================
        [HttpGet("stock-items-raw")]
        public async Task<IActionResult> GetStockItemsRaw()
        {
            try
            {
                var result = await _tallyService.GetStockItemsAsync();

                return Content(result, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch stock items from Tally.",
                    error = ex.Message
                });
            }
        }

        // =========================================================
        // STOCK GROUPS RAW DATA
        // =========================================================
        [HttpGet("stock-groups-raw")]
        public async Task<IActionResult> GetStockGroupsRaw()
        {
            try
            {
                var result = await _tallyService.GetStockGroupsAsync();

                return Content(result, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch stock groups from Tally.",
                    error = ex.Message
                });
            }
        }


        // =========================================================
        // STOCK ITEMS LIST
        // =========================================================
        [HttpGet("stock-items")]
        public async Task<IActionResult> GetStockItems()
        {
            try
            {
                var stockItems = await _tallyService.GetStockItemListAsync();

                return Ok(new
                {
                    connected = true,
                    count = stockItems.Count,
                    stockItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch stock item details from Tally.",
                    error = ex.Message
                });
            }
        }
    }
}