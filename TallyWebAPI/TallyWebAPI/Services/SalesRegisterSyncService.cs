using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services;

public sealed class SalesRegisterSyncService
{
    private readonly AppDbContext _db;
    private readonly TallyService _tally;
    private readonly VoucherService _vouchers;

    public SalesRegisterSyncService(AppDbContext db, TallyService tally, VoucherService vouchers)
    { _db = db; _tally = tally; _vouchers = vouchers; }

    public async Task<object> PreviewOrApplyAsync(int companyId, string fromDate, string toDate, bool apply)
    {
        if (apply) throw new InvalidOperationException("Legacy unbound apply is disabled. Use the approval-bound Sales Register workflow.");

        if (companyId <= 0 || !DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) || from > to)
            throw new ArgumentException("Supply a valid company and ordered yyyyMMdd date range.");

        var errors = new List<string>();
        var company = await _db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId)
            ?? throw new InvalidOperationException($"CompanyId {companyId} does not exist in SQL.");
        var tallyCompanyDoc = XDocument.Parse(await _tally.GetCompaniesAsync());
        var matchingCompanies = tallyCompanyDoc.Descendants().Where(x => x.Name.LocalName.Equals("COMPANY", StringComparison.OrdinalIgnoreCase))
            .Where(x => string.Equals((string?)x.Attribute("NAME") ?? Child(x, "NAME"), company.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingCompanies.Count != 1 || !string.Equals(Child(matchingCompanies.FirstOrDefault(), "GUID"), company.TallyGuid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected SQL company does not match exactly one Tally company GUID.");

        var typeRows = await _tally.GetVoucherTypesAsync(company.Name);
        var salesTypes = typeRows.Where(x => string.Equals(x.Parent, "Sales", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (salesTypes.Count == 0) throw new InvalidOperationException("Tally returned no voucher types with Sales parent classification.");

        var sourceAll = await _vouchers.GetSalesRegisterVouchersAsync(fromDate, toDate, company.Name);
        var knownTypes = typeRows.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceSales = sourceAll.Where(v => salesTypes.Contains(v.VoucherType.Trim())).ToList();
        var cancelledSales = sourceSales.Where(v => v.IsCancelled).ToList();
        var candidates = sourceSales.Where(v => !v.IsCancelled).ToList();
        var duplicateSources = candidates.Where(x => !string.IsNullOrWhiteSpace(x.Guid)).GroupBy(x => x.Guid.Trim(), StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).ToList();
        var duplicateSourceRows = duplicateSources.Sum(x => x.Count() - 1);
        if (sourceAll.Any(x => !knownTypes.Contains(x.VoucherType.Trim()))) errors.Add("Tally returned voucher type(s) missing from its classification collection.");
        if (duplicateSourceRows > 0) errors.Add($"Tally returned {duplicateSourceRows} duplicate Sales voucher GUID row(s).");

        var ledgers = await _db.Ledgers.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync();
        var ledgerMap = ledgers.Where(x => !string.IsNullOrWhiteSpace(x.TallyGuid))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1 && !string.IsNullOrWhiteSpace(x.Single().TallyGuid))
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        var sourceLedgerNames = candidates.SelectMany(x => x.Postings).Select(x => x.LedgerName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var mappedLedgerNames = sourceLedgerNames.Where(ledgerMap.ContainsKey).ToList();
        var unmappedLedgerNames = sourceLedgerNames.Except(mappedLedgerNames, StringComparer.OrdinalIgnoreCase).ToList();

        var tableState = await CheckTablesAsync();
        if (!tableState.Headers || !tableState.Entries)
            errors.Add("Independent Sales Register tables do not exist yet; preview is read-only and apply is unavailable until separately approved schema creation.");

        var existingGuidMap = new Dictionary<string, SalesRegisterEntity>(StringComparer.OrdinalIgnoreCase);
        var crossCompanyGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingRowsInPeriod = 0;
        var unexpectedExistingRows = 0;
        if (tableState.Headers)
        {
            var stored = await _db.SalesRegister.AsNoTracking().Include(x => x.Entries).Where(x => x.CompanyId == companyId).ToListAsync();
            var candidateGuidSet = candidates.Select(x => x.Guid.Trim()).Where(x => x.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var storedInPeriod = stored.Where(x => x.VoucherDate >= from && x.VoucherDate <= to).ToList();
            existingRowsInPeriod = storedInPeriod.Count;
            unexpectedExistingRows = storedInPeriod.Count(x => !candidateGuidSet.Contains(x.TallyGuid));
            if (unexpectedExistingRows > 0)
                errors.Add($"SalesRegister_tbl contains {unexpectedExistingRows} period row(s) whose GUID is not in Tally's Sales result.");
            foreach (var group in stored.GroupBy(x => x.TallyGuid, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() != 1) errors.Add($"SalesRegister_tbl has duplicate GUID {group.Key} for this company.");
                else existingGuidMap[group.Key] = group.Single();
            }
            var incomingGuids = candidates.Select(x => x.Guid.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (incomingGuids.Length > 0)
                foreach (var row in await _db.SalesRegister.AsNoTracking().Where(x => x.CompanyId != companyId && incomingGuids.Contains(x.TallyGuid)).Select(x => x.TallyGuid).ToListAsync())
                    crossCompanyGuids.Add(row);
            if (crossCompanyGuids.Count > 0) errors.Add($"{crossCompanyGuids.Count} Sales voucher GUID(s) are already stored under another company.");
        }

        int headerCount = 0, postingCount = 0, skippedExisting = 0, skippedInvalid = 0;
        decimal absoluteTotal = 0;
        var plan = new List<(VoucherDto Source, decimal Amount, LedgerEntity Party, List<(VoucherPostingDto Posting, LedgerEntity Ledger)> Postings)>();
        foreach (var v in candidates)
        {
            var problems = new List<string>();
            if (v.SourceValidationError is not null) problems.Add(v.SourceValidationError);
            if (string.IsNullOrWhiteSpace(v.Guid) || !v.Guid.StartsWith(company.TallyGuid + "-", StringComparison.OrdinalIgnoreCase)) problems.Add("missing or foreign company voucher GUID");
            if (!DateTime.TryParseExact(v.Date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date < from || date > to) problems.Add("invalid/out-of-range date");
            if (string.IsNullOrWhiteSpace(v.VoucherNumber)) problems.Add("missing voucher number");
            LedgerEntity? party = null;
            if (string.IsNullOrWhiteSpace(v.PartyName) || !ledgerMap.TryGetValue(v.PartyName.Trim(), out party)) problems.Add("party ledger has no unique company-scoped SQL GUID mapping");
            var hasAmount = decimal.TryParse(v.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAmount);
            if (!hasAmount) problems.Add("invalid voucher header amount");
            if (v.Postings.Count == 0 || v.Postings.Sum(x => x.Amount) != 0m) problems.Add("signed postings missing or not balanced to zero");
            var mapped = new List<(VoucherPostingDto Posting, LedgerEntity Ledger)>();
            foreach (var posting in v.Postings)
            {
                if (!ledgerMap.TryGetValue(posting.LedgerName.Trim(), out var ledger)) problems.Add($"ledger '{posting.LedgerName}' has no unique company-scoped SQL GUID mapping");
                else mapped.Add((posting, ledger));
            }
            if (problems.Count > 0) { skippedInvalid++; errors.Add($"Voucher {v.VoucherNumber} ({v.Guid}): {string.Join("; ", problems)}."); continue; }
            var amount = Math.Abs(parsedAmount);
            absoluteTotal += amount;
            if (existingGuidMap.TryGetValue(v.Guid, out var existing))
            {
                if (existing.VoucherNumber != v.VoucherNumber || existing.VoucherDate != date || existing.VoucherType != v.VoucherType || existing.Amount != amount || existing.PartyLedgerName != v.PartyName)
                { skippedInvalid++; errors.Add($"Stored voucher {v.Guid} identity/amount differs from Tally."); continue; }
                var expectedEntries = mapped.Select(x => (Guid: x.Ledger.TallyGuid!, x.Posting.EntryIndex, x.Posting.Amount)).OrderBy(x => x.EntryIndex).ToArray();
                var storedEntries = existing.Entries.Select(x => (Guid: x.LedgerGuid, x.EntrySequence, x.SignedAmount)).OrderBy(x => x.EntrySequence).ToArray();
                if (storedEntries.Length == 0 || !expectedEntries.SequenceEqual(storedEntries.Select(x => (x.Guid, x.EntrySequence, x.SignedAmount))))
                { skippedInvalid++; errors.Add($"Stored voucher {v.Guid} has missing or mismatched signed ledger entries."); continue; }
                skippedExisting++;
                continue;
            }
            plan.Add((v, amount, party!, mapped));
            headerCount++; postingCount += mapped.Count;
        }

        return new { preview = !apply, applied = apply, companyId, companyName = company.Name, companyTallyGuid = company.TallyGuid, fromDate, toDate,
            salesVoucherTypes = salesTypes.OrderBy(x => x).ToArray(), sourceVoucherCount = sourceAll.Count, salesVoucherCount = candidates.Count,
            skippedNonSales = sourceAll.Count - sourceSales.Count, unclassifiedVoucherCount = sourceAll.Count(x => !knownTypes.Contains(x.VoucherType.Trim())),
            sourceSalesVoucherCount = sourceSales.Count, cancelledSalesVoucherCount = cancelledSales.Count,
            cancelledVouchers = cancelledSales.Select(v => new { guid = v.Guid, voucherNumber = v.VoucherNumber, date = v.Date, voucherType = v.VoucherType, isCancelled = v.IsCancelled, cancellationStatus = v.CancellationStatus }),
            invalidActiveVoucherCount = skippedInvalid,
            unbalancedActiveVoucherCount = candidates.Count(v => v.Postings.Count == 0 || v.Postings.Sum(p => p.Amount) != 0m),
            ledgerMappingErrorCount = unmappedLedgerNames.Count + candidates.Count(v => string.IsNullOrWhiteSpace(v.PartyName) || !ledgerMap.ContainsKey(v.PartyName.Trim())),
            monthly = sourceSales.GroupBy(v => v.Date.Length >= 6 ? v.Date[..6] : "invalid").OrderBy(g => g.Key).Select(g => new {
                month = g.Key, activeSalesVouchers = g.Count(v => !v.IsCancelled), cancelledSalesVouchers = g.Count(v => v.IsCancelled),
                signedPostings = g.Where(v => !v.IsCancelled).Sum(v => v.Postings.Count),
                missingActiveSalesVouchers = g.Count(v => !v.IsCancelled && !existingGuidMap.ContainsKey(v.Guid.Trim())),
                absoluteSalesTotal = g.Where(v => !v.IsCancelled).Sum(v => decimal.TryParse(v.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? Math.Abs(amount) : 0m)
            }),
            duplicateSourceGuidRows = duplicateSourceRows, crossCompanyGuidMatches = crossCompanyGuids.Count,
            existingSalesVouchers = skippedExisting, existingSalesRegisterRowsInPeriod = existingRowsInPeriod,
            unexpectedExistingSalesRegisterRows = unexpectedExistingRows,
            distinctPostingLedgerNames = sourceLedgerNames.Count, mappedPostingLedgerNames = mappedLedgerNames.Count,
            distinctMappedPostingLedgerGuids = mappedLedgerNames.Select(x => ledgerMap[x].TallyGuid).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            unmappedPostingLedgerNames = unmappedLedgerNames,
            headersToAdd = headerCount, signedPostingsToAdd = postingCount,
            absoluteSalesTotal = absoluteTotal, validationErrors = errors, tablesExist = tableState.Headers && tableState.Entries,
            dataValidationPassed = errors.Count == 0 && tableState.Headers && tableState.Entries, safeToApply = false };
    }

    private async Task<(bool Headers, bool Entries)> CheckTablesAsync()
    {
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        try
        {
            if (!wasOpen) await conn.OpenAsync();
            await using var command = conn.CreateCommand();
            command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesRegister_tbl', N'U') IS NULL THEN 0 ELSE 1 END, CASE WHEN OBJECT_ID(N'dbo.SalesRegisterEntries_tbl', N'U') IS NULL THEN 0 ELSE 1 END";
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return (reader.GetInt32(0) == 1, reader.GetInt32(1) == 1);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }

    private static string Child(XElement? parent, string name) => parent?.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
}

