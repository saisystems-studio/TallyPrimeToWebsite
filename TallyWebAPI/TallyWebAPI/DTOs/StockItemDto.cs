namespace TallyWebAPI.DTOs
{
    public class StockItemDto
    {
        public string Name { get; set; } = "";
        public string StockGroup { get; set; } = "";
        public string Unit { get; set; } = "";

        public string GstApplicable { get; set; } = "";
        public string TypeOfSupply { get; set; } = "";

        public string HsnCode { get; set; } = "";
        public string HsnSource { get; set; } = "";

        public string GstSource { get; set; } = "";
        public string GstRate { get; set; } = "";

        public string OpeningBalance { get; set; } = "";
        public string OpeningRate { get; set; } = "";
        public string OpeningValue { get; set; } = "";
    }
}