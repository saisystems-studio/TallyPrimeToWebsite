using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/db/vouchers")]
    //[AllowAnonymous]
    [Authorize]
    public class VouchersDbController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public VouchersDbController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetVouchers(
            [FromQuery] int companyId,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? voucherType = null)
        {
            var query = _dbContext.Vouchers
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(fromDate))
                query = query.Where(x => x.VoucherDate!.CompareTo(fromDate) >= 0);

            if (!string.IsNullOrWhiteSpace(toDate))
                query = query.Where(x => x.VoucherDate!.CompareTo(toDate) <= 0);

            if (!string.IsNullOrWhiteSpace(voucherType) &&
                !voucherType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.VoucherType == voucherType);
            }

            var vouchers = await query
                .OrderByDescending(x => x.VoucherDate)
                .ThenByDescending(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    companyId = x.CompanyId,
                    tallyGuid = x.TallyGuid,
                    date = x.VoucherDate,
                    voucherNumber = x.VoucherNumber,
                    voucherType = x.VoucherType,
                    partyName = x.PartyName,
                    partyGstin = x.PartyGstin,
                    state = x.State,
                    placeOfSupply = x.PlaceOfSupply,
                    amount = x.Amount,
                    narration = x.Narration,
                    lastSyncedAt = x.LastSyncedAt
                })
                .ToListAsync();

            return Ok(new
            {
                companyId,
                totalRecords = vouchers.Count,
                vouchers
            });
        }
    }
}