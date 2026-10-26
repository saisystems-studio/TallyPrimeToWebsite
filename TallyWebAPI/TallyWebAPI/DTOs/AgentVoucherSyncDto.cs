
namespace TallyWebAPI.DTOs
{
    public class AgentVoucherLedgerEntryDto
    {
        public string LedgerName { get; set; } = "";

        public decimal Amount { get; set; }

        public int EntryIndex { get; set; }
    }

    public class AgentVoucherSyncDto
    {
        public string CompanyTallyGuid { get; set; } = "";

        public string TallyGuid { get; set; } = "";

        public string? VoucherNumber { get; set; }

        public string? VoucherType { get; set; }

        public string? VoucherDate { get; set; }

        public string? PartyName { get; set; }

        public string? PartyGstin { get; set; }

        public string? State { get; set; }

        public string? PlaceOfSupply { get; set; }

        public decimal? Amount { get; set; }

        public string? Narration { get; set; }

        // TallyPrime voucher accounting allocations
        public List<AgentVoucherLedgerEntryDto> LedgerEntries
        { get; set; } = new();
    }

    public class AgentVoucherSyncRequest
    {
        public List<AgentVoucherSyncDto> Vouchers
        { get; set; } = new();
    }
}
