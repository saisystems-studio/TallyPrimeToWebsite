namespace TallyWebAPI.Models
{
    public class OutstandingEntity
    {
        public int Id { get; set; }

        // Company
        public int CompanyId { get; set; }
        public CompanyEntity? Company { get; set; }

        // Party / Ledger
        public string LedgerName { get; set; } = "";

        // Voucher
        public string TallyGuid { get; set; } = "";
        public string? VoucherNumber { get; set; }
        public string? VoucherType { get; set; }
        public string? VoucherDate { get; set; }

        // Bill allocation
        public string BillReference { get; set; } = "";
        public string? BillType { get; set; }
        public string? BillDate { get; set; }
        public string? CreditPeriod { get; set; }

        // Amount from Tally bill allocation
        public decimal Amount { get; set; }

        // Sync
        public DateTime LastSyncedAt { get; set; }
    }
}