using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/db/outstanding")]
    //[AllowAnonymous]
    [Authorize]
    public class OutstandingsDbController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public OutstandingsDbController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetOutstanding(
            [FromQuery] int? companyId = null,
            [FromQuery] string? ledgerName = null,
            [FromQuery] bool includeSettled = false)
        {
            // =====================================================
            // OUTSTANDING ROWS
            //
            // companyId is optional.
            //
            // No companyId:
            //      Return all companies.
            //
            // companyId supplied:
            //      Return selected company only.
            // =====================================================

            var query = _dbContext.Outstandings
                .AsNoTracking()
                .AsQueryable();

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(x =>
                    x.CompanyId == companyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(ledgerName))
            {
                query = query.Where(x =>
                    x.LedgerName == ledgerName);
            }

            var rows = await query
                .OrderBy(x => x.CompanyId)
                .ThenBy(x => x.VoucherDate)
                .ThenBy(x => x.Id)
                .ToListAsync();


            // =====================================================
            // COMPANY LOOKUP
            // =====================================================

            var companyMap = await _dbContext.Companies
                .AsNoTracking()
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.Name
                );


            // =====================================================
            // LEDGER PARENT LOOKUP
            //
            // Important:
            // Same ledger name can exist in different companies.
            //
            // Therefore lookup key:
            //
            //      CompanyId + LedgerName
            //
            // =====================================================

            var ledgerQuery = _dbContext.Ledgers
                .AsNoTracking()
                .AsQueryable();

            if (companyId.HasValue && companyId.Value > 0)
            {
                ledgerQuery = ledgerQuery.Where(x =>
                    x.CompanyId == companyId.Value);
            }

            var ledgers = await ledgerQuery
                .Select(x => new
                {
                    x.CompanyId,
                    x.Name,
                    x.Parent
                })
                .ToListAsync();

            var ledgerParentMap = ledgers
                .GroupBy(x => new
                {
                    x.CompanyId,
                    Name = (x.Name ?? "").ToUpper()
                })
                .ToDictionary(
                    x => (
                        x.Key.CompanyId,
                        x.Key.Name
                    ),
                    x => x.First().Parent ?? ""
                );


            // =====================================================
            // GROUP BY COMPANY + PARTY + BILL REFERENCE
            // =====================================================

            var grouped = rows
                .GroupBy(x => new
                {
                    x.CompanyId,
                    x.LedgerName,
                    x.BillReference
                })
                .Select(group =>
                {
                    var orderedRows = group
                        .OrderBy(x => x.Id)
                        .ToList();


                    // =================================================
                    // COMPANY NAME
                    // =================================================

                    companyMap.TryGetValue(
                        group.Key.CompanyId,
                        out var companyName
                    );

                    companyName ??= "";


                    // =================================================
                    // ON ACCOUNT?
                    // =================================================

                    var isOnAccount = orderedRows.Any(x =>
                        string.Equals(
                            x.BillType,
                            "On Account",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );


                    // =================================================
                    // ORIGINAL BILL
                    //
                    // New Ref OR Opening Balance
                    // =================================================

                    var originalRow = orderedRows
                        .FirstOrDefault(x =>
                            string.Equals(
                                x.BillType,
                                "New Ref",
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                x.BillType,
                                "Opening Balance",
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                    var firstRow =
                        originalRow ??
                        orderedRows.First();


                    // =================================================
                    // SIGNED AMOUNTS
                    // =================================================

                    var signedPendingAmount =
                        group.Sum(x => x.Amount);

                    var signedOriginalAmount =
                        originalRow?.Amount ?? 0;


                    // =================================================
                    // LEDGER PARENT / BALANCE TYPE
                    // =================================================

                    var ledgerKey = (
                        group.Key.CompanyId,
                        (group.Key.LedgerName ?? "").ToUpper()
                    );

                    ledgerParentMap.TryGetValue(
                        ledgerKey,
                        out var ledgerParent
                    );

                    ledgerParent ??= "";

                    string balanceType;

                    if (ledgerParent.Equals(
                        "Sundry Debtors",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        balanceType = "Receivable";
                    }
                    else if (ledgerParent.Equals(
                        "Sundry Creditors",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        balanceType = "Payable";
                    }
                    else
                    {
                        balanceType = "";
                    }


                    // =================================================
                    // BILL / VOUCHER DATE
                    // =================================================

                    var dateText =
                        !string.IsNullOrWhiteSpace(firstRow.BillDate)
                            ? firstRow.BillDate
                            : firstRow.VoucherDate;

                    DateTime? parsedBillDate = null;

                    if (!string.IsNullOrWhiteSpace(dateText) &&
                        DateTime.TryParseExact(
                            dateText,
                            "yyyyMMdd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var parsedDate))
                    {
                        parsedBillDate = parsedDate;
                    }


                    // =================================================
                    // PENDING DAYS
                    //
                    // On Account is an unallocated adjustment,
                    // not a pending bill.
                    // =================================================

                    var pendingDays =
                        !isOnAccount &&
                        parsedBillDate.HasValue &&
                        signedPendingAmount != 0
                            ? Math.Max(
                                0,
                                (DateTime.Today -
                                 parsedBillDate.Value.Date).Days
                            )
                            : 0;


                    // =================================================
                    // DISPLAY BILL REFERENCE
                    //
                    // Internal synthetic key remains unchanged.
                    // UI receives friendly "On Account".
                    // =================================================

                    var displayBillReference =
                        isOnAccount
                            ? "On Account"
                            : group.Key.BillReference;


                    // =================================================
                    // ROW TYPE
                    // =================================================

                    var rowType =
                        isOnAccount
                            ? "OnAccount"
                            : "Bill";


                    // =================================================
                    // STATUS
                    // =================================================

                    var status =
                        isOnAccount
                            ? "On Account"
                            : signedPendingAmount == 0
                                ? "Settled"
                                : "Pending";


                    // =================================================
                    // FINAL REPORT ROW
                    // =================================================

                    return new
                    {
                        companyId =
                            group.Key.CompanyId,

                        companyName,

                        ledgerName =
                            group.Key.LedgerName,

                        ledgerParent,

                        balanceType,

                        rowType,

                        // Friendly UI reference.
                        billReference =
                            displayBillReference,

                        // Internal stable reference.
                        internalBillReference =
                            group.Key.BillReference,

                        voucherNumber =
                            firstRow.VoucherNumber,

                        voucherType =
                            firstRow.VoucherType,

                        voucherDate =
                            firstRow.VoucherDate,

                        billDate =
                            firstRow.BillDate,

                        creditPeriod =
                            firstRow.CreditPeriod,

                        // On Account is not an original bill.
                        originalAmount =
                            isOnAccount
                                ? 0
                                : Math.Abs(
                                    signedOriginalAmount
                                ),

                        // Positive amount for UI display.
                        pendingAmount =
                            Math.Abs(
                                signedPendingAmount
                            ),

                        // Keep Tally accounting sign.
                        // Used for receivable/payable net calculation.
                        signedPendingAmount,

                        pendingDays,

                        status,

                        allocations = orderedRows
                            .Select(x => new
                            {
                                x.VoucherNumber,
                                x.VoucherType,
                                x.VoucherDate,
                                x.BillType,

                                amount =
                                    Math.Abs(x.Amount),

                                signedAmount =
                                    x.Amount
                            })
                            .ToList()
                    };
                })
                .ToList();


            // =====================================================
            // SETTLED FILTER
            //
            // On Account rows with non-zero value remain because
            // they participate in net outstanding calculation.
            // =====================================================

            var result = includeSettled
                ? grouped
                : grouped
                    .Where(x =>
                        x.signedPendingAmount != 0)
                    .ToList();


            return Ok(result);
        }
    }
}