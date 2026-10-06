namespace TallyWebAPI.DTOs
{
    public class AgentStockItemSyncDto
    {
        // Used by VPS API to identify the correct company
        public string CompanyTallyGuid { get; set; } = "";

        // Stable Stock Item GUID from Tally
        public string TallyGuid { get; set; } = "";

        public long? MasterId { get; set; }

        public long? AlterId { get; set; }

        public string Name { get; set; } = "";

        public string StockGroup { get; set; } = "";

        public string Unit { get; set; } = "";

        public decimal OpeningQuantity { get; set; }

        public decimal ClosingQuantity { get; set; }
    }

    public class AgentStockItemSyncRequest
    {
        public List<AgentStockItemSyncDto> StockItems { get; set; } = new();
    }
}