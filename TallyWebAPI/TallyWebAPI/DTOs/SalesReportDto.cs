namespace TallyWebAPI.DTOs
{
    public class SalesReportDto
    {
        public string Date { get; set; } = "";

        public string LedgerName { get; set; } = "";

        public string VoucherType { get; set; } = "";

        public string VoucherNumber { get; set; } = "";

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }

        public decimal OpeningBalance { get; set; }

        public decimal ClosingBalance { get; set; }
    }
}