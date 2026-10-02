using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using TallyWebAPI.Data;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services
{
    public class CompanySyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly TallyService _tallyService;

        public CompanySyncService(
            AppDbContext dbContext,
            TallyService tallyService)
        {
            _dbContext = dbContext;
            _tallyService = tallyService;
        }

        public async Task<CompanySyncResult> SyncAsync()
        {
            var xml = await _tallyService.GetCompaniesAsync();

            var document = XDocument.Parse(xml);

            var tallyCompanies = document
                .Descendants("COMPANY")
                .Where(x => x.Attribute("NAME") != null)
                .Select(x => new
                {
                    TallyGuid = GetValue(x, "GUID"),
                    Name = x.Attribute("NAME")?.Value?.Trim()
                           ?? GetValue(x, "NAME"),

                    FormalName =
                        GetValue(x, "BASICCOMPANYFORMALNAME"),

                    State = GetValue(x, "STATENAME"),
                    Country = GetValue(x, "COUNTRYNAME"),
                    Pincode = GetValue(x, "PINCODE"),
                    Email = GetValue(x, "EMAIL"),
                    Phone = GetValue(x, "PHONENUMBER"),

                    StartingFrom =
                        GetValue(x, "STARTINGFROM"),

                    BooksFrom =
                        GetValue(x, "BOOKSFROM")
                })
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.TallyGuid))
                .ToList();

            var existingCompanies =
                await _dbContext.Companies.ToListAsync();

            var existingByGuid = existingCompanies
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.TallyGuid))
                .ToDictionary(
                    x => x.TallyGuid,
                    StringComparer.OrdinalIgnoreCase
                );

            int inserted = 0;
            int updated = 0;
            int skipped = 0;

            foreach (var tally in tallyCompanies)
            {
                // ---------------------------------------------
                // GST DETAILS - fetch for this company
                // ---------------------------------------------
                string gstin = "";
                string gstRegistrationType = "";

                try
                {
                    var gstXml =
                        await _tallyService
                            .GetGstRegistrationsAsync(tally.Name);

                    if (!string.IsNullOrWhiteSpace(gstXml))
                    {
                        // Remove invalid XML control references
                        gstXml =
                            System.Text.RegularExpressions.Regex.Replace(
                                gstXml,
                                @"&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);",
                                ""
                            );

                        var gstDocument =
                            XDocument.Parse(gstXml);

                        var gstTaxUnit = gstDocument
                            .Descendants()
                            .FirstOrDefault(x =>
                                x.Name.LocalName.Equals(
                                    "TAXUNIT",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                &&
                                (
                                    string.Equals(
                                        x.Attribute("TAXTYPE")?.Value,
                                        "GST",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    ||
                                    !string.IsNullOrWhiteSpace(
                                        x.Attribute(
                                            "TAXREGISTRATION"
                                        )?.Value
                                    )
                                )
                            );

                        if (gstTaxUnit != null)
                        {
                            gstin =
                                gstTaxUnit
                                    .Attribute("TAXREGISTRATION")
                                    ?.Value
                                    ?.Trim()
                                ?? "";

                            if (string.IsNullOrWhiteSpace(gstin))
                            {
                                gstin = GetValue(
                                    gstTaxUnit,
                                    "GSTREGNUMBER"
                                );
                            }

                            var registrationDetails =
                                gstTaxUnit
                                    .Descendants()
                                    .FirstOrDefault(x =>
                                        x.Name.LocalName.Equals(
                                            "GSTREGISTRATIONDETAILS.LIST",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    );

                            if (registrationDetails != null)
                            {
                                gstRegistrationType =
                                    GetValue(
                                        registrationDetails,
                                        "REGISTRATIONTYPE"
                                    );
                            }

                            if (string.IsNullOrWhiteSpace(
                                gstRegistrationType))
                            {
                                gstRegistrationType =
                                    GetValue(
                                        gstTaxUnit,
                                        "GSTREGISTRATIONTYPE"
                                    );
                            }
                        }
                    }
                }
                catch
                {
                    // GST failure must not stop company sync.
                    gstin = "";
                    gstRegistrationType = "";
                }

                // ---------------------------------------------
                // INSERT
                // ---------------------------------------------
                if (!existingByGuid.TryGetValue(
                    tally.TallyGuid,
                    out var company))
                {
                    company = new CompanyEntity
                    {
                        TallyGuid = tally.TallyGuid,
                        Name = tally.Name,
                        FormalName = tally.FormalName,
                        State = tally.State,
                        Country = tally.Country,
                        Pincode = tally.Pincode,
                        Email = tally.Email,
                        Phone = tally.Phone,

                        Gstin = gstin,
                        GstRegistrationType =
                            gstRegistrationType,

                        StartingFrom = tally.StartingFrom,
                        BooksFrom = tally.BooksFrom,
                        LastSyncedAt = DateTime.Now
                    };

                    _dbContext.Companies.Add(company);

                    existingByGuid[tally.TallyGuid] =
                        company;

                    inserted++;

                    continue;
                }

                // ---------------------------------------------
                // UPDATE / SKIP
                // ---------------------------------------------
                bool changed =
                    company.Name != tally.Name ||
                    company.FormalName != tally.FormalName ||
                    company.State != tally.State ||
                    company.Country != tally.Country ||
                    company.Pincode != tally.Pincode ||
                    company.Email != tally.Email ||
                    company.Phone != tally.Phone ||
                    company.Gstin != gstin ||
                    company.GstRegistrationType !=
                        gstRegistrationType ||
                    company.StartingFrom !=
                        tally.StartingFrom ||
                    company.BooksFrom != tally.BooksFrom;

                if (!changed)
                {
                    skipped++;
                    continue;
                }

                company.Name = tally.Name;
                company.FormalName = tally.FormalName;
                company.State = tally.State;
                company.Country = tally.Country;
                company.Pincode = tally.Pincode;
                company.Email = tally.Email;
                company.Phone = tally.Phone;

                company.Gstin = gstin;
                company.GstRegistrationType =
                    gstRegistrationType;

                company.StartingFrom =
                    tally.StartingFrom;

                company.BooksFrom =
                    tally.BooksFrom;

                company.LastSyncedAt = DateTime.Now;

                updated++;
            }

            await _dbContext.SaveChangesAsync();

            return new CompanySyncResult
            {
                Fetched = tallyCompanies.Count,
                Inserted = inserted,
                Updated = updated,
                Skipped = skipped
            };
        }

        private static string GetValue(
            XElement element,
            string name)
        {
            return element
                .Descendants()
                .FirstOrDefault(x =>
                    x.Name.LocalName.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?.Value
                ?.Trim() ?? "";
        }
    }

    public class CompanySyncResult
    {
        public int Fetched { get; set; }

        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }
    }
}