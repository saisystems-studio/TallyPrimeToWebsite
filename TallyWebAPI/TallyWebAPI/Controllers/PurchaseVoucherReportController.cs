using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/reports/purchase-register")]
[Authorize]
public sealed class PurchaseVoucherReportController(AppDbContext db) : ControllerBase
{
    [HttpGet("periods")]
    public async Task<IActionResult> GetAvailablePeriods(
        [FromQuery] int companyId,
        CancellationToken cancellationToken)
    {
        if (companyId <= 0)
            return BadRequest(new { message = "A valid CompanyId is required." });

        // VoucherDate is persisted as yyyyMMdd, so lexical ordering is chronological.
        var dates = db.Vouchers
            .AsNoTracking()
            .Where(v => v.CompanyId == companyId
                && v.VoucherType == "Purchase"
                && v.VoucherDate != null
                && v.VoucherDate.Length == 8)
            .Select(v => (string?)v.VoucherDate);

        var earliest = await dates.MinAsync(cancellationToken);
        var latest = await dates.MaxAsync(cancellationToken);

        return Ok(new { companyId, fromDate = earliest, toDate = latest, hasVouchers = earliest != null });
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int companyId,
        [FromQuery] string fromDate,
        [FromQuery] string toDate,
        CancellationToken cancellationToken)
    {
        if (companyId <= 0)
            return BadRequest(new { message = "A valid CompanyId is required." });

        if (!DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var from)
            || !DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var to)
            || from > to)
        {
            return BadRequest(new { message = "Supply an ordered yyyyMMdd date range." });
        }

        // Vouchers_tbl stores dates as sortable yyyyMMdd strings; its Amount
        // column is the persisted voucher total used by the existing voucher API.
        var vouchers = await db.Vouchers
            .AsNoTracking()
            .Where(v => v.CompanyId == companyId
                && v.VoucherType == "Purchase"
                && v.VoucherDate != null
                && v.VoucherDate.CompareTo(fromDate) >= 0
                && v.VoucherDate.CompareTo(toDate) <= 0)
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .Select(v => new
            {
                v.Id,
                v.CompanyId,
                tallyGuid = v.TallyGuid,
                date = v.VoucherDate,
                voucherNumber = v.VoucherNumber,
                voucherType = v.VoucherType,
                partyName = v.PartyName,
                amount = v.Amount,
                narration = v.Narration
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            companyId,
            fromDate,
            toDate,
            totalRecords = vouchers.Count,
            totalPurchaseAmount = vouchers.Where(v => v.amount.HasValue).Sum(v => v.amount!.Value),
            vouchers
        });
    }
}
