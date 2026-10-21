namespace TallyWebAPI.Models
{
    public class VoucherLedgerEntryEntity
    {
        public int Id { get; set; }

        public int VoucherId { get; set; }
        public VoucherEntity? Voucher { get; set; }

        public int? LedgerId { get; set; }
        public string LedgerName { get; set; } = "";
        public decimal Amount { get; set; }
        public int EntryIndex { get; set; }
    }
}
