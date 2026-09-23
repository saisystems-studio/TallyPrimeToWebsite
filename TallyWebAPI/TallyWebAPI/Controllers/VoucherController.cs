using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyWebAPI.Services;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VoucherController : ControllerBase
    {
        private readonly VoucherService _voucherService;

        public VoucherController(VoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        // ==========================================
        // RAW VOUCHERS FROM TALLY
        // ==========================================
        [HttpGet("vouchers-raw")]
        public async Task<IActionResult> GetVouchersRaw()
        {
            try
            {
                var result = await _voucherService.GetVouchersAsync(
                 "20240401",
                 "20240430"
                );

                return Content(result, "application/xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch vouchers from Tally.",
                    error = ex.Message
                });
            }
        }

        // ==========================================
        // VOUCHER LIST
        // ==========================================
        [HttpGet("vouchers")]
        public async Task<IActionResult> GetVouchers(
            [FromQuery] string fromDate,
            [FromQuery] string toDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fromDate) ||
                    string.IsNullOrWhiteSpace(toDate))
                {
                    return BadRequest(new
                    {
                        connected = false,
                        message = "From Date and To Date are required."
                    });
                }

                var vouchers = await _voucherService.GetVoucherListAsync(
                    fromDate,
                    toDate
                );

                return Ok(new
                {
                    connected = true,
                    fromDate,
                    toDate,
                    count = vouchers.Count,
                    vouchers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch voucher details from Tally.",
                    error = ex.Message
                });
            }
        }


        [HttpGet("voucher-detail")]
        public async Task<IActionResult> GetVoucherDetail(
    [FromQuery] string guid,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(guid) ||
                    string.IsNullOrWhiteSpace(fromDate) ||
                    string.IsNullOrWhiteSpace(toDate))
                {
                    return BadRequest(new
                    {
                        connected = false,
                        message = "GUID, From Date and To Date are required."
                    });
                }

                var voucher = await _voucherService.GetVoucherDetailAsync(
                    guid,
                    fromDate,
                    toDate
                );

                if (voucher == null)
                {
                    return NotFound(new
                    {
                        connected = true,
                        message = "Voucher not found."
                    });
                }

                return Ok(new
                {
                    connected = true,
                    voucher
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to fetch voucher details from Tally.",
                    error = ex.Message
                });
            }
        }

        [AllowAnonymous]
        [HttpGet("voucher-export-raw")]
        public async Task<IActionResult> GetVoucherExportRaw(
    [FromQuery] string voucherNumber,
    [FromQuery] string voucherType,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
        {
            try
            {
                var result = await _voucherService.GetVoucherExportAsync(
                    voucherNumber,
                    voucherType,
                    fromDate,
                    toDate
                );

                return Content(
                    result,
                    "application/xml",
                    System.Text.Encoding.UTF8
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Unable to export voucher from Tally.",
                    error = ex.Message
                });
            }
        }


        [HttpGet("tamil-voucher-test")]
        [AllowAnonymous]
        public async Task<IActionResult> TamilVoucherTest(
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
        {
            try
            {
                var result = await _voucherService.GetTamilVoucherTestAsync(
                    fromDate,
                    toDate
                );

                return Content(
                    result,
                    "application/xml",
                    System.Text.Encoding.UTF8
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    connected = false,
                    message = "Tamil voucher test failed.",
                    error = ex.Message
                });
            }
        }

    }
}