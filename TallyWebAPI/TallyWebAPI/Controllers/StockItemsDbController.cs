using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/db/stock-items")]
    //[AllowAnonymous]
    [Authorize]
    public class StockItemsDbController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public StockItemsDbController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET: /api/db/stock-items?companyId=1
        [HttpGet]
        public async Task<IActionResult> GetStockItems([FromQuery] int companyId)
        {
            var items = await _dbContext.StockItems
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    id = x.Id,
                    companyId = x.CompanyId,

                    tallyGuid = x.TallyGuid,
                    masterId = x.MasterId,
                    alterId = x.AlterId,

                    name = x.Name,
                    stockGroup = x.StockGroup,
                    unit = x.Unit,

                    openingQuantity = x.OpeningQuantity,
                    closingQuantity = x.ClosingQuantity,

                    lastSyncedAt = x.LastSyncedAt
                })
                .ToListAsync();

            return Ok(new
            {
                companyId,
                totalRecords = items.Count,
                stockItems = items
            });
        }
    }
}