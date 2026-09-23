namespace TallyWebAPI.DTOs
{
    public class VoucherDetailDto
    {
        public string Date { get; set; } = "";
        public string VoucherNumber { get; set; } = "";
        public string VoucherType { get; set; } = "";

        public string PartyName { get; set; } = "";
        public string PartyGstin { get; set; } = "";

        public string State { get; set; } = "";
        public string PlaceOfSupply { get; set; } = "";

        public string Reference { get; set; } = "";
        public string Narration { get; set; } = "";
        public string Guid { get; set; } = "";

        public string TotalAmount { get; set; } = "";

        public List<VoucherLedgerEntryDto> LedgerEntries { get; set; } = new();
        public List<VoucherItemEntryDto> Items { get; set; } = new();
    }

    public class VoucherLedgerEntryDto
    {
        public string LedgerName { get; set; } = "";
        public string Amount { get; set; } = "";
    }

    public class VoucherItemEntryDto
    {
        public string ItemName { get; set; } = "";
        public string Quantity { get; set; } = "";
        public string Rate { get; set; } = "";
        public string Amount { get; set; } = "";
    }
}