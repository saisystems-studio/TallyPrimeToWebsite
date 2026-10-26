
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers;

[ApiController]
[Route("api/reports/inactive-customers")]
[Authorize]
public sealed class InactiveCustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int companyId,
        CancellationToken cancellationToken)
    {
        if (companyId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid CompanyId is required."
            });
        }

        // Read Sales vouchers only for the selected company.
        var vouchers = await db.Vouchers
            .AsNoTracking()
            .Where(v =>
                v.CompanyId == companyId &&
                v.VoucherType == "Sales" &&
                v.PartyName != null &&
                v.PartyName != "" &&
                v.VoucherDate != null &&
                v.VoucherDate.Length == 8)
            .Select(v => new
            {
                v.PartyName,
                v.VoucherDate
            })
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;

        var customers = vouchers
            .Select(v => new
            {
                CustomerName = v.PartyName!.Trim(),
                SalesDate = DateTime.TryParseExact(
                    v.VoucherDate,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate)
                        ? (DateTime?)parsedDate
                        : null
            })
            .Where(v =>
                v.CustomerName.Length > 0 &&
                v.SalesDate.HasValue &&
                v.SalesDate.Value.Date <= today)
            .GroupBy(
                v => v.CustomerName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var lastSalesDate = group.Max(v => v.SalesDate!.Value);

                return new
                {
                    customerName = group.Key,
                    lastSalesDate = lastSalesDate.ToString("yyyy-MM-dd"),
                    inactiveDays = (today - lastSalesDate.Date).Days
                };
            })
            .OrderByDescending(c => c.inactiveDays)
            .ThenBy(c => c.customerName)
            .ToList();

        return Ok(new
        {
            companyId,
            asOfDate = today.ToString("yyyy-MM-dd"),
            totalCustomers = customers.Count,
            customers
        });
    }
}
