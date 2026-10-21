namespace TallyWebAPI.DTOs;

public class StockSummaryDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public string Source { get; set; } = "TallyPrime Stock Summary";
    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;
    public int TotalItems { get; set; }
    public List<StockSummaryGroupDto> Groups { get; set; } = [];
    public StockSummaryTotalsDto GrandTotal { get; set; } = new();
}

public class StockSummaryTotalsDto
{
    // Null quantities/rates cannot be combined (mixed or compound units).
    public string Unit { get; set; } = "";
    public decimal? OpeningQuantity { get; set; }
    public decimal? OpeningRate { get; set; }
    public decimal? InwardQuantity { get; set; }
    public decimal? OutwardQuantity { get; set; }
    public decimal? ClosingQuantity { get; set; }
    public decimal? InwardRate { get; set; }
    public decimal? OutwardRate { get; set; }
    public decimal? ClosingRate { get; set; }
    // Null means Tally left the native report amount blank; zero is an actual reported zero.
    public decimal? OpeningValue { get; set; }
    public decimal? InwardValue { get; set; }
    public decimal? OutwardValue { get; set; }
    public decimal? ClosingValue { get; set; }
}

public class StockSummaryGroupDto : StockSummaryTotalsDto
{
    public string GroupName { get; set; } = "";
    public string TallyGuid { get; set; } = "";
    public List<StockSummaryGroupDto> Children { get; set; } = [];
    public List<StockSummaryItemDto> Items { get; set; } = [];
}

public class StockSummaryItemDto : StockSummaryTotalsDto
{
    public string StockItemName { get; set; } = "";
    public string StockGroup { get; set; } = "";
    public string TallyGuid { get; set; } = "";
    public string OpeningQuantityText { get; set; } = "";
    public string OpeningRateText { get; set; } = "";
    public string InwardQuantityText { get; set; } = "";
    public string OutwardQuantityText { get; set; } = "";
    public string ClosingQuantityText { get; set; } = "";
    public string InwardRateText { get; set; } = "";
    public string OutwardRateText { get; set; } = "";
    public string ClosingRateText { get; set; } = "";
}

