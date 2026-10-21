namespace TallyWebAPI.Models;

public class StockSummarySnapshotEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public CompanyEntity? Company { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public DateTime SyncedAt { get; set; }
    public List<StockSummaryDetailEntity> Details { get; set; } = [];
}

public class StockSummaryDetailEntity
{
    public int Id { get; set; }
    public int StockSummaryId { get; set; }
    public StockSummarySnapshotEntity? StockSummary { get; set; }

    // Group rows carry group totals, item rows carry native Tally item values,
    // and GrandTotal rows preserve Tally's overall report summary.
    public string RowType { get; set; } = "";
    public string StockGroupName { get; set; } = "";
    public string? StockGroupGuid { get; set; }
    public string? ParentGroupName { get; set; }
    public string? ParentGroupGuid { get; set; }
    public string? StockItemName { get; set; }
    public string? StockItemGuid { get; set; }
    public string? Unit { get; set; }
    public int Level { get; set; }
    public int DisplayOrder { get; set; }

    public decimal? OpeningQty { get; set; }
    public string? OpeningQtyText { get; set; }
    public decimal? OpeningRate { get; set; }
    public string? OpeningRateText { get; set; }
    public decimal? OpeningValue { get; set; }
    public decimal? InwardQty { get; set; }
    public string? InwardQtyText { get; set; }
    public decimal? InwardRate { get; set; }
    public string? InwardRateText { get; set; }
    public decimal? InwardValue { get; set; }
    public decimal? OutwardQty { get; set; }
    public string? OutwardQtyText { get; set; }
    public decimal? OutwardRate { get; set; }
    public string? OutwardRateText { get; set; }
    public decimal? OutwardValue { get; set; }
    public decimal? ClosingQty { get; set; }
    public string? ClosingQtyText { get; set; }
    public decimal? ClosingRate { get; set; }
    public string? ClosingRateText { get; set; }
    public decimal? ClosingValue { get; set; }
}
