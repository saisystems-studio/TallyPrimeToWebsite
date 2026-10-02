using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/db/day-book")]
    [AllowAnonymous]
    //[Authorize]
    public class DayBookController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public DayBookController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetDayBook(
            [FromQuery] int? companyId = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? voucherType = null)
        {
            var query = _dbContext.Vouchers
                .AsNoTracking()
                .AsQueryable();

            // Company filter
            // companyId empty = All Companies
            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(x =>
                    x.CompanyId == companyId.Value);
            }

            // From Date
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                query = query.Where(x =>
                    x.VoucherDate != null &&
                    x.VoucherDate.CompareTo(fromDate) >= 0);
            }

            // To Date
            if (!string.IsNullOrWhiteSpace(toDate))
            {
                query = query.Where(x =>
                    x.VoucherDate != null &&
                    x.VoucherDate.CompareTo(toDate) <= 0);
            }

            // Voucher Type
            if (!string.IsNullOrWhiteSpace(voucherType) &&
                !voucherType.Equals(
                    "All",
                    StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.VoucherType == voucherType);
            }

            var vouchers = await query
                .OrderByDescending(x => x.VoucherDate)
                .ThenByDescending(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,

                    companyId = x.CompanyId,

                    companyName = x.Company != null
                        ? x.Company.Name
                        : "",

                    tallyGuid = x.TallyGuid,

                    date = x.VoucherDate,

                    particulars = x.PartyName,

                    voucherType = x.VoucherType,

                    voucherNumber = x.VoucherNumber,

                    amount = x.Amount,

                    narration = x.Narration
                })
                .ToListAsync();

            var voucherTypes = vouchers
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.voucherType))
                .Select(x => x.voucherType!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return Ok(new
            {
                companyId,
                fromDate,
                toDate,
                totalRecords = vouchers.Count,
                voucherTypes,
                vouchers
            });
        }
    }
}