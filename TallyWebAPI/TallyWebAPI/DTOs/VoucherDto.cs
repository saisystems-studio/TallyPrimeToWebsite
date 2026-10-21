namespace TallyWebAPI.DTOs
{
    public class VoucherDto
    {
        public string Date { get; set; } = "";
        public string VoucherNumber { get; set; } = "";
        public string VoucherType { get; set; } = "";

        public string PartyName { get; set; } = "";
        public string PartyGstin { get; set; } = "";

        public string State { get; set; } = "";
        public string PlaceOfSupply { get; set; } = "";

        public string Amount { get; set; } = "";
        public string Narration { get; set; } = "";

        public string Guid { get; set; } = "";
        public List<VoucherPostingDto> Postings { get; set; } = new();
    }

    public class VoucherPostingDto
    {
        public string LedgerName { get; set; } = "";
        public decimal Amount { get; set; }
        public int EntryIndex { get; set; }
    }
}
