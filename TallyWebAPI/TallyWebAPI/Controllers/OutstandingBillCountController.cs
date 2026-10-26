
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/outstanding-bill-count")]
    [Authorize]
    public class OutstandingBillCountController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<OutstandingBillCountController> _logger;

        public OutstandingBillCountController(
            AppDbContext db,
            ILogger<OutstandingBillCountController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetBillCount(
            [FromQuery] int companyId)
        {
            if (companyId <= 0)
            {
                return BadRequest(new
                {
                    message = "Valid company ID is required."
                });
            }

            const string sql = """
                WITH BillBalances AS
                (
                    SELECT
                        CompanyId,
                        LedgerName,
                        BillReference,
                        SUM(Amount) AS PendingAmount
                    FROM dbo.Outstanding_tbl
                    WHERE CompanyId = @companyId
                      AND BillType IN ('New Ref', 'Agst Ref')
                      AND NULLIF(
                          LTRIM(RTRIM(BillReference)), ''
                      ) IS NOT NULL
                    GROUP BY
                        CompanyId,
                        LedgerName,
                        BillReference
                )
                SELECT
                    LedgerName,
                    COUNT(*) AS OutstandingBillCount,
                    SUM(ABS(PendingAmount)) AS OutstandingAmount
                FROM BillBalances
                WHERE ABS(PendingAmount) > 0.01
                GROUP BY LedgerName
                ORDER BY LedgerName;
                """;

            try
            {
                var parties = new List<object>();
                int totalBills = 0;
                decimal totalOutstanding = 0;

                var connection = _db.Database.GetDbConnection();

                await _db.Database.OpenConnectionAsync();

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.CommandType = CommandType.Text;

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@companyId";
                    parameter.DbType = DbType.Int32;
                    parameter.Value = companyId;
                    command.Parameters.Add(parameter);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string ledgerName =
                            reader.GetString(0);

                        int billCount =
                            Convert.ToInt32(reader.GetValue(1));

                        decimal amount =
                            Convert.ToDecimal(reader.GetValue(2));

                        totalBills += billCount;
                        totalOutstanding += amount;

                        parties.Add(new
                        {
                            ledgerName,
                            outstandingBillCount = billCount,
                            outstandingAmount = amount
                        });
                    }
                }
                finally
                {
                    await _db.Database.CloseConnectionAsync();
                }

                return Ok(new
                {
                    companyId,
                    totalBills,
                    totalOutstanding,
                    parties
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Outstanding bill count failed for company {CompanyId}",
                    companyId
                );

                return StatusCode(500, new
                {
                    message =
                        "Unable to fetch outstanding bill count."
                });
            }
        }


        [HttpGet("details")]
        public async Task<IActionResult> GetBillDetails(
            [FromQuery] int companyId,
            [FromQuery] string ledgerName)
        {
            if (companyId <= 0 ||
                string.IsNullOrWhiteSpace(ledgerName))
            {
                return BadRequest(new
                {
                    message = "Company and party are required."
                });
            }

            const string sql = """
        WITH BillBalances AS
        (
            SELECT
                BillReference,

                MAX(CASE WHEN BillType = 'New Ref'
                    THEN VoucherNumber END) AS VoucherNumber,

                SUM(CASE WHEN BillType = 'New Ref'
                    THEN ABS(Amount) ELSE 0 END) AS OpeningAmount,

                SUM(Amount) AS SignedPending,

                TRY_CONVERT(date,
                    MIN(CASE WHEN BillType = 'New Ref'
                        THEN BillDate END), 112) AS BillDate,

                TRY_CONVERT(int,
                    MAX(CASE WHEN BillType = 'New Ref'
                        THEN CreditPeriod END)) AS CreditDays,

                SUM(CASE WHEN BillType = 'New Ref'
                    THEN 1 ELSE 0 END) AS NewRefCount

            FROM dbo.Outstanding_tbl
            WHERE CompanyId = @companyId
              AND LedgerName = @ledgerName
              AND BillType IN ('New Ref', 'Agst Ref')
              AND NULLIF(LTRIM(RTRIM(BillReference)), '') IS NOT NULL

            GROUP BY BillReference
        )
        SELECT
            BillReference,
            VoucherNumber,
            OpeningAmount,
            ABS(SignedPending) AS PendingAmount,
            BillDate,
            CreditDays
        FROM BillBalances
        WHERE ABS(SignedPending) > 0.01
          AND NewRefCount > 0
        ORDER BY BillDate, BillReference;
        """;

            try
            {
                var bills = new List<object>();
                decimal totalPending = 0;

                await _db.Database.OpenConnectionAsync();

                try
                {
                    using var command =
                        _db.Database.GetDbConnection().CreateCommand();

                    command.CommandText = sql;

                    var companyParam = command.CreateParameter();
                    companyParam.ParameterName = "@companyId";
                    companyParam.Value = companyId;
                    command.Parameters.Add(companyParam);

                    var ledgerParam = command.CreateParameter();
                    ledgerParam.ParameterName = "@ledgerName";
                    ledgerParam.Value = ledgerName;
                    command.Parameters.Add(ledgerParam);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var reference = reader.GetString(0);

                        var voucherNumber = reader.IsDBNull(1)
                            ? ""
                            : reader.GetString(1);

                        var openingAmount =
                            Convert.ToDecimal(reader.GetValue(2));

                        var pendingAmount =
                            Convert.ToDecimal(reader.GetValue(3));

                        DateTime? billDate = reader.IsDBNull(4)
                            ? null
                            : reader.GetDateTime(4);

                        int? creditDays = reader.IsDBNull(5)
                            ? null
                            : reader.GetInt32(5);

                        int? overdueDays = null;

                        if (billDate.HasValue &&
                            creditDays.HasValue &&
                            creditDays.Value >= 0)
                        {
                            var dueDate =
                                billDate.Value.Date.AddDays(creditDays.Value);

                            overdueDays = Math.Max(
                                0,
                                (DateTime.Today - dueDate).Days
                            );
                        }

                        totalPending += pendingAmount;

                        bills.Add(new
                        {
                            ledgerName,
                            billReference = reference,
                            voucherNumber,
                            openingAmount,
                            pendingAmount,
                            billDate = billDate?.ToString("yyyy-MM-dd"),
                            overdueDays
                        });
                    }
                }
                finally
                {
                    await _db.Database.CloseConnectionAsync();
                }

                return Ok(new
                {
                    companyId,
                    ledgerName,
                    totalBills = bills.Count,
                    totalPending,
                    bills
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to load bill details for company {CompanyId}",
                    companyId
                );

                return StatusCode(500, new
                {
                    message = "Unable to fetch bill details."
                });
            }
        }


        [HttpGet("groups")]
        public async Task<IActionResult> GetGroupWiseBillCount(
            [FromQuery] int companyId)
        {
            if (companyId <= 0)
            {
                return BadRequest(new
                {
                    message = "Valid company ID is required."
                });
            }

            const string sql = """
        WITH BillBalances AS
        (
            SELECT
                CompanyId,
                LedgerName,
                BillReference,
                SUM(Amount) AS PendingAmount
            FROM dbo.Outstanding_tbl
            WHERE CompanyId = @companyId
              AND BillType IN ('New Ref', 'Agst Ref')
              AND NULLIF(
                  LTRIM(RTRIM(BillReference)), ''
              ) IS NOT NULL
            GROUP BY
                CompanyId,
                LedgerName,
                BillReference
        ),
        PendingBills AS
        (
            SELECT *
            FROM BillBalances
            WHERE ABS(PendingAmount) > 0.01
        ),
        PartyGroups AS
        (
            SELECT
                CompanyId,
                Name AS LedgerName,
                MAX(
                    NULLIF(LTRIM(RTRIM(Parent)), '')
                ) AS GroupName
            FROM dbo.Ledgers_tbl
            GROUP BY CompanyId, Name
        )
        SELECT
            COALESCE(g.GroupName, 'Unassigned') AS GroupName,
            p.LedgerName,
            COUNT(*) AS BillCount,
            SUM(ABS(p.PendingAmount)) AS PendingAmount
        FROM PendingBills p
        LEFT JOIN PartyGroups g
          ON g.CompanyId = p.CompanyId
         AND g.LedgerName = p.LedgerName
        GROUP BY
            COALESCE(g.GroupName, 'Unassigned'),
            p.LedgerName
        ORDER BY
            GroupName,
            p.LedgerName;
        """;

            try
            {
                var groups = new Dictionary<string, GroupBillResult>(
                    StringComparer.OrdinalIgnoreCase
                );

                await _db.Database.OpenConnectionAsync();

                try
                {
                    using var command =
                        _db.Database.GetDbConnection().CreateCommand();

                    command.CommandText = sql;

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@companyId";
                    parameter.Value = companyId;
                    command.Parameters.Add(parameter);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string groupName = reader.GetString(0);
                        string ledgerName = reader.GetString(1);
                        int billCount =
                            Convert.ToInt32(reader.GetValue(2));
                        decimal amount =
                            Convert.ToDecimal(reader.GetValue(3));

                        if (!groups.TryGetValue(groupName, out var group))
                        {
                            group = new GroupBillResult
                            {
                                GroupName = groupName
                            };

                            groups.Add(groupName, group);
                        }

                        group.Parties.Add(new PartyBillResult
                        {
                            LedgerName = ledgerName,
                            OutstandingBillCount = billCount,
                            OutstandingAmount = amount
                        });

                        group.TotalParties++;
                        group.OutstandingBillCount += billCount;
                        group.OutstandingAmount += amount;
                    }
                }
                finally
                {
                    await _db.Database.CloseConnectionAsync();
                }

                return Ok(new
                {
                    companyId,
                    totalGroups = groups.Count,
                    totalBills = groups.Values.Sum(
                        x => x.OutstandingBillCount),
                    totalOutstanding = groups.Values.Sum(
                        x => x.OutstandingAmount),
                    groups = groups.Values.ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Group-wise outstanding failed for company {CompanyId}",
                    companyId
                );

                return StatusCode(500, new
                {
                    message = "Unable to fetch group-wise outstanding."
                });
            }
        }

        public class GroupBillResult
        {
            public string GroupName { get; set; } = "";
            public int TotalParties { get; set; }
            public int OutstandingBillCount { get; set; }
            public decimal OutstandingAmount { get; set; }
            public List<PartyBillResult> Parties { get; set; } = new();
        }

        public class PartyBillResult
        {
            public string LedgerName { get; set; } = "";
            public int OutstandingBillCount { get; set; }
            public decimal OutstandingAmount { get; set; }
        }


    }
}
