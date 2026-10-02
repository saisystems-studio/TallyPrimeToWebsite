namespace TallyWebAPI.DTOs
{
    public class StockSummaryDto
    {
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";

        public List<StockSummaryGroupDto> Groups { get; set; } = new();
    }

    public class StockSummaryGroupDto
    {
        public string GroupName { get; set; } = "";

        public decimal OpeningQuantity { get; set; }
        public decimal InwardQuantity { get; set; }
        public decimal OutwardQuantity { get; set; }
        public decimal ClosingQuantity { get; set; }

        public string Unit { get; set; } = "";

        public List<StockSummaryItemDto> Items { get; set; } = new();
    }

    public class StockSummaryItemDto
    {
        public string StockItemName { get; set; } = "";
        public string StockGroup { get; set; } = "";

        public decimal OpeningQuantity { get; set; }
        public decimal InwardQuantity { get; set; }
        public decimal OutwardQuantity { get; set; }
        public decimal ClosingQuantity { get; set; }

        public string Unit { get; set; } = "";
    }
}