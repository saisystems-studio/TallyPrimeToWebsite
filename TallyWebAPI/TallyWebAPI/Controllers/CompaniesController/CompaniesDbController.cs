using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/db/companies")]
    //[AllowAnonymous] // Temporary for testing
    [Authorize]
    public class CompaniesDbController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public CompaniesDbController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanies()
        {
            var companies = await _dbContext.Companies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            return Ok(new
            {
                totalCompanies = companies.Count,
                companies
            });
        }
    }
}