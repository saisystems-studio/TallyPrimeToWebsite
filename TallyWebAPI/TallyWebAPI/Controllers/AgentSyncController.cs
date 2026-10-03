using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Controllers
{
    [ApiController]
    [Route("api/agent-sync")]
    public class AgentSyncController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AgentSyncController(
            AppDbContext dbContext,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [HttpPost("companies")]
        public async Task<IActionResult> SyncCompanies(
            [FromBody] AgentCompanySyncRequest request)
        {
            // ---------------------------------------------
            // AGENT AUTHENTICATION
            // ---------------------------------------------

            var expectedKey =
                _configuration["AgentSync:ApiKey"];

            var receivedKey =
                Request.Headers["X-Agent-Key"]
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(expectedKey) ||
                string.IsNullOrWhiteSpace(receivedKey) ||
                !string.Equals(
                    expectedKey,
                    receivedKey,
                    StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    message = "Invalid sync agent key."
                });
            }

            if (request.Companies == null ||
                request.Companies.Count == 0)
            {
                return Ok(new
                {
                    fetched = 0,
                    inserted = 0,
                    updated = 0,
                    skipped = 0
                });
            }

            var validCompanies = request.Companies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.TallyGuid) &&
                    !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            var existingCompanies =
                await _dbContext.Companies.ToListAsync();

            var existingByGuid = existingCompanies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.TallyGuid))
                .ToDictionary(
                    x => x.TallyGuid,
                    StringComparer.OrdinalIgnoreCase);

            var inserted = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var incoming in validCompanies)
            {
                var guid = incoming.TallyGuid.Trim();
                var name = incoming.Name.Trim();

                if (!existingByGuid.TryGetValue(
                    guid,
                    out var company))
                {
                    company = new CompanyEntity
                    {
                        TallyGuid = guid,
                        Name = name,
                        FormalName = incoming.FormalName,
                        State = incoming.State,
                        Country = incoming.Country,
                        Pincode = incoming.Pincode,
                        Email = incoming.Email,
                        Phone = incoming.Phone,
                        Gstin = incoming.Gstin,
                        GstRegistrationType =
                            incoming.GstRegistrationType,
                        StartingFrom = incoming.StartingFrom,
                        BooksFrom = incoming.BooksFrom,
                        LastSyncedAt = DateTime.UtcNow
                    };

                    _dbContext.Companies.Add(company);

                    existingByGuid[guid] = company;

                    inserted++;
                    continue;
                }

                var changed =
                    company.Name != name ||
                    company.FormalName != incoming.FormalName ||
                    company.State != incoming.State ||
                    company.Country != incoming.Country ||
                    company.Pincode != incoming.Pincode ||
                    company.Email != incoming.Email ||
                    company.Phone != incoming.Phone ||
                    company.Gstin != incoming.Gstin ||
                    company.GstRegistrationType !=
                        incoming.GstRegistrationType ||
                    company.StartingFrom != incoming.StartingFrom ||
                    company.BooksFrom != incoming.BooksFrom;

                if (!changed)
                {
                    skipped++;
                    continue;
                }

                company.Name = name;
                company.FormalName = incoming.FormalName;
                company.State = incoming.State;
                company.Country = incoming.Country;
                company.Pincode = incoming.Pincode;
                company.Email = incoming.Email;
                company.Phone = incoming.Phone;
                company.Gstin = incoming.Gstin;
                company.GstRegistrationType =
                    incoming.GstRegistrationType;
                company.StartingFrom =
                    incoming.StartingFrom;
                company.BooksFrom =
                    incoming.BooksFrom;
                company.LastSyncedAt =
                    DateTime.UtcNow;

                updated++;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                fetched = validCompanies.Count,
                inserted,
                updated,
                skipped
            });
        }
    }
}