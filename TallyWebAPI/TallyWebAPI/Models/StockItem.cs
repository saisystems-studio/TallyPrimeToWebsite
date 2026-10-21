namespace TallyWebAPI.Models
{
    public class StockItem
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        public CompanyEntity? Company { get; set; }

        // Stable unique ID from Tally
        public string? TallyGuid { get; set; }

        public long? MasterId { get; set; }

        // Changes when the master is altered in Tally
        public long? AlterId { get; set; }

        public string Name { get; set; } = "";

        public string StockGroup { get; set; } = "";

        public string Unit { get; set; } = "";

        public decimal OpeningQuantity { get; set; }

        public decimal OpeningStock { get; set; }

        public decimal OpeningRate { get; set; }

        public decimal ClosingQuantity { get; set; }

        // Standard Selling Rate
        public decimal? StandardSellingRate { get; set; }

        // Standard Cost Rate
        public decimal? StandardCostRate { get; set; }

        // Maximum Retail Price
        public decimal? MRP { get; set; }

        // HSN / SAC Code
        public string? HsnSac { get; set; }

        // Part Number
        public string? PartNumber { get; set; }

        public DateTime LastSyncedAt { get; set; }
    }
}
