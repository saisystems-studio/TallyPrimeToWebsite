using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Data;
using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services;

public class StockSummarySnapshotService(AppDbContext db, StockSummaryService tally)
{
    private const string GroupRow = "Group";
    private const string ItemRow = "Item";
    private const string GrandTotalRow = "GrandTotal";

    public async Task<StockSummarySyncResult> SyncAsync(
        int companyId, string? fromDate = null, string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) throw new ArgumentException("Please select a company.");
        var company = await db.Companies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected company was not found.");
        // Verify the SQL-to-Tally identity before using the company name for any report query.
        await tally.VerifyCompanyIdentityAsync(company.Name, company.TallyGuid, cancellationToken);
        var (from, to) = await tally.ResolveRequestedPeriodAsync(fromDate, toDate,
            company.BooksFrom, company.Name, cancellationToken);
        var fromDay = DateOnly.ParseExact(from, "yyyyMMdd", CultureInfo.InvariantCulture);
        var toDay = DateOnly.ParseExact(to, "yyyyMMdd", CultureInfo.InvariantCulture);

        var report = await tally.GetStockSummaryAsync(from, to, company.Name, cancellationToken);
        // Recheck after report generation so an identity change or unavailable response cannot reach SQL.
        await tally.VerifyCompanyIdentityAsync(company.Name, company.TallyGuid, cancellationToken);
        var newRows = CreateRows(report);
        if (newRows.Count == 0 || newRows.All(x => x.RowType != GrandTotalRow))
            throw new InvalidDataException("Tally returned an incomplete Stock Summary. The existing saved snapshot was left unchanged.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var snapshot = await db.StockSummarySnapshots
                .Include(x => x.Details)
                .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.FromDate == fromDay && x.ToDate == toDay,
                    cancellationToken);
            var isNew = snapshot is null;
            snapshot ??= new StockSummarySnapshotEntity
            {
                CompanyId = companyId,
                FromDate = fromDay,
                ToDate = toDay
            };
            snapshot.SyncedAt = DateTime.UtcNow;

            if (isNew)
                db.StockSummarySnapshots.Add(snapshot);
            else
                db.StockSummaryDetails.RemoveRange(snapshot.Details);

            foreach (var row in newRows)
                row.StockSummary = snapshot;
            db.StockSummaryDetails.AddRange(newRows);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new StockSummarySyncResult(companyId, company.Name, from, to, report.TotalItems,
                newRows.Count, snapshot.SyncedAt, !isNew, isNew ? "Created" : "Updated");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<StockSummaryDto?> GetAsync(int companyId, string? fromDate, string? toDate,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) throw new ArgumentException("Please select a company.");
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected company was not found.");
        var (from, to) = await tally.ResolveRequestedPeriodAsync(fromDate, toDate,
            company.BooksFrom, company.Name, cancellationToken);
        return await GetResolvedAsync(companyId, from, to, cancellationToken);
    }

    private async Task<StockSummaryDto?> GetResolvedAsync(int companyId, string from, string to,
        CancellationToken cancellationToken)
    {
        var fromDay = DateOnly.ParseExact(from, "yyyyMMdd", CultureInfo.InvariantCulture);
        var toDay = DateOnly.ParseExact(to, "yyyyMMdd", CultureInfo.InvariantCulture);
        var snapshot = await db.StockSummarySnapshots.AsNoTracking()
            .Include(x => x.Company)
            .Include(x => x.Details)
            .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.FromDate == fromDay && x.ToDate == toDay,
                cancellationToken);
        if (snapshot is null) return null;

        var rows = snapshot.Details.OrderBy(x => x.DisplayOrder).ToList();
        var groupRows = rows.Where(x => x.RowType == GroupRow).ToList();
        var groups = new Dictionary<string, (StockSummaryGroupDto Dto, StockSummaryDetailEntity Row)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in groupRows)
        {
            var key = GroupKey(row.StockGroupGuid, row.StockGroupName);
            if (!groups.TryAdd(key, (MapGroup(row), row)))
                throw new InvalidDataException($"Saved Stock Summary contains duplicate group identity '{row.StockGroupName}'.");
        }

        var roots = new List<StockSummaryGroupDto>();
        foreach (var pair in groups.Values.OrderBy(x => x.Row.DisplayOrder))
        {
            var parentKey = GroupKey(pair.Row.ParentGroupGuid, pair.Row.ParentGroupName);
            if (string.IsNullOrWhiteSpace(pair.Row.ParentGroupName) ||
                (pair.Row.ParentGroupName.Equals("Primary", StringComparison.OrdinalIgnoreCase) && !groups.ContainsKey(parentKey)))
            {
                roots.Add(pair.Dto);
                continue;
            }
            if (!groups.TryGetValue(parentKey, out var parent))
                throw new InvalidDataException($"Saved Stock Summary group '{pair.Row.StockGroupName}' has a missing parent.");
            parent.Dto.Children.Add(pair.Dto);
        }

        foreach (var row in rows.Where(x => x.RowType == ItemRow))
        {
            var key = GroupKey(row.StockGroupGuid, row.StockGroupName);
            if (!groups.TryGetValue(key, out var parent))
                throw new InvalidDataException($"Saved Stock Summary item '{row.StockItemName}' has a missing Stock Group.");
            parent.Dto.Items.Add(MapItem(row));
        }

        var grandTotal = rows.SingleOrDefault(x => x.RowType == GrandTotalRow)
            ?? throw new InvalidDataException("Saved Stock Summary is missing its grand total row.");
        return new StockSummaryDto
        {
            CompanyId = snapshot.CompanyId,
            CompanyName = snapshot.Company?.Name ?? "",
            FromDate = from,
            ToDate = to,
            RetrievedAtUtc = snapshot.SyncedAt,
            TotalItems = rows.Count(x => x.RowType == ItemRow),
            Groups = roots,
            GrandTotal = MapTotals(grandTotal)
        };
    }

    private static List<StockSummaryDetailEntity> CreateRows(StockSummaryDto report)
    {
        var rows = new List<StockSummaryDetailEntity>();
        var order = 0;
        void AddItem(StockSummaryItemDto item, StockSummaryGroupDto group, string? parentName, string? parentGuid, int level)
        {
            rows.Add(new StockSummaryDetailEntity
            {
                RowType = ItemRow,
                StockGroupName = group.GroupName,
                StockGroupGuid = NullIfEmpty(group.TallyGuid),
                ParentGroupName = parentName,
                ParentGroupGuid = parentGuid,
                StockItemName = item.StockItemName,
                StockItemGuid = NullIfEmpty(item.TallyGuid),
                Unit = NullIfEmpty(item.Unit),
                Level = level,
                DisplayOrder = order++,
                OpeningQty = item.OpeningQuantity,
                OpeningQtyText = item.OpeningQuantityText,
                OpeningRate = item.OpeningRate,
                OpeningRateText = NullIfEmpty(item.OpeningRateText),
                OpeningValue = item.OpeningValue,
                InwardQty = item.InwardQuantity,
                InwardQtyText = item.InwardQuantityText,
                InwardRate = item.InwardRate,
                InwardRateText = NullIfEmpty(item.InwardRateText),
                InwardValue = item.InwardValue,
                OutwardQty = item.OutwardQuantity,
                OutwardQtyText = item.OutwardQuantityText,
                OutwardRate = item.OutwardRate,
                OutwardRateText = NullIfEmpty(item.OutwardRateText),
                OutwardValue = item.OutwardValue,
                ClosingQty = item.ClosingQuantity,
                ClosingQtyText = item.ClosingQuantityText,
                ClosingRate = item.ClosingRate,
                ClosingRateText = NullIfEmpty(item.ClosingRateText),
                ClosingValue = item.ClosingValue
            });
        }

        void AddGroup(StockSummaryGroupDto group, string? parentName, string? parentGuid, int level)
        {
            rows.Add(new StockSummaryDetailEntity
            {
                RowType = GroupRow,
                StockGroupName = group.GroupName,
                StockGroupGuid = NullIfEmpty(group.TallyGuid),
                ParentGroupName = parentName,
                ParentGroupGuid = parentGuid,
                Unit = NullIfEmpty(group.Unit),
                Level = level,
                DisplayOrder = order++,
                OpeningQty = group.OpeningQuantity,
                OpeningRate = group.OpeningRate,
                OpeningValue = group.OpeningValue,
                InwardQty = group.InwardQuantity,
                InwardRate = group.InwardRate,
                InwardValue = group.InwardValue,
                OutwardQty = group.OutwardQuantity,
                OutwardRate = group.OutwardRate,
                OutwardValue = group.OutwardValue,
                ClosingQty = group.ClosingQuantity,
                ClosingRate = group.ClosingRate,
                ClosingValue = group.ClosingValue
            });
            foreach (var child in group.Children)
                AddGroup(child, group.GroupName, NullIfEmpty(group.TallyGuid), level + 1);
            foreach (var item in group.Items)
                AddItem(item, group, parentName, parentGuid, level + 1);
        }

        foreach (var root in report.Groups)
            AddGroup(root, null, null, 0);
        rows.Add(new StockSummaryDetailEntity
        {
            RowType = GrandTotalRow,
            StockGroupName = "",
            DisplayOrder = order,
            OpeningQty = report.GrandTotal.OpeningQuantity,
            OpeningRate = report.GrandTotal.OpeningRate,
            OpeningValue = report.GrandTotal.OpeningValue,
            InwardQty = report.GrandTotal.InwardQuantity,
            InwardRate = report.GrandTotal.InwardRate,
            InwardValue = report.GrandTotal.InwardValue,
            OutwardQty = report.GrandTotal.OutwardQuantity,
            OutwardRate = report.GrandTotal.OutwardRate,
            OutwardValue = report.GrandTotal.OutwardValue,
            ClosingQty = report.GrandTotal.ClosingQuantity,
            ClosingRate = report.GrandTotal.ClosingRate,
            ClosingValue = report.GrandTotal.ClosingValue
        });
        return rows;
    }

    private static StockSummaryGroupDto MapGroup(StockSummaryDetailEntity row) => new()
    {
        GroupName = row.StockGroupName,
        TallyGuid = row.StockGroupGuid ?? "",
        Unit = row.Unit ?? "",
        OpeningQuantity = row.OpeningQty,
        OpeningRate = row.OpeningRate,
        OpeningValue = row.OpeningValue,
        InwardQuantity = row.InwardQty,
        InwardRate = row.InwardRate,
        InwardValue = row.InwardValue,
        OutwardQuantity = row.OutwardQty,
        OutwardRate = row.OutwardRate,
        OutwardValue = row.OutwardValue,
        ClosingQuantity = row.ClosingQty,
        ClosingRate = row.ClosingRate,
        ClosingValue = row.ClosingValue
    };

    private static StockSummaryItemDto MapItem(StockSummaryDetailEntity row) => new()
    {
        StockItemName = row.StockItemName ?? "",
        StockGroup = row.StockGroupName,
        TallyGuid = row.StockItemGuid ?? "",
        Unit = row.Unit ?? "",
        OpeningQuantity = row.OpeningQty,
        OpeningQuantityText = row.OpeningQtyText ?? "",
        OpeningRate = row.OpeningRate,
        OpeningRateText = row.OpeningRateText ?? "",
        OpeningValue = row.OpeningValue,
        InwardQuantity = row.InwardQty,
        InwardQuantityText = row.InwardQtyText ?? "",
        InwardRate = row.InwardRate,
        InwardRateText = row.InwardRateText ?? "",
        InwardValue = row.InwardValue,
        OutwardQuantity = row.OutwardQty,
        OutwardQuantityText = row.OutwardQtyText ?? "",
        OutwardRate = row.OutwardRate,
        OutwardRateText = row.OutwardRateText ?? "",
        OutwardValue = row.OutwardValue,
        ClosingQuantity = row.ClosingQty,
        ClosingQuantityText = row.ClosingQtyText ?? "",
        ClosingRate = row.ClosingRate,
        ClosingRateText = row.ClosingRateText ?? "",
        ClosingValue = row.ClosingValue
    };

    private static StockSummaryTotalsDto MapTotals(StockSummaryDetailEntity row) => new()
    {
        Unit = row.Unit ?? "",
        OpeningQuantity = row.OpeningQty,
        OpeningRate = row.OpeningRate,
        OpeningValue = row.OpeningValue,
        InwardQuantity = row.InwardQty,
        InwardRate = row.InwardRate,
        InwardValue = row.InwardValue,
        OutwardQuantity = row.OutwardQty,
        OutwardRate = row.OutwardRate,
        OutwardValue = row.OutwardValue,
        ClosingQuantity = row.ClosingQty,
        ClosingRate = row.ClosingRate,
        ClosingValue = row.ClosingValue
    };

    private static string GroupKey(string? guid, string? name) =>
        !string.IsNullOrWhiteSpace(guid) ? "G:" + guid : "N:" + (name ?? "").Trim();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record StockSummarySyncResult(
    int CompanyId, string CompanyName, string FromDate, string ToDate, int ItemCount,
    int DetailRowCount, DateTime SyncedAtUtc, bool ReplacedExistingSnapshot, string Status);
