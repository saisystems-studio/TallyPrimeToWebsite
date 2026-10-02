namespace TallyWebAPI.DTOs
{
    public class LedgerReportDto
    {
        public string LedgerName { get; set; } = "";

        public decimal OpeningBalance { get; set; }

        public decimal ClosingBalance { get; set; }

        public decimal TotalDebit { get; set; }

        public decimal TotalCredit { get; set; }

        public List<LedgerTransactionDto> Transactions { get; set; } = new();
    }

    public class LedgerTransactionDto
    {
        public string Date { get; set; } = "";

        public string Particulars { get; set; } = "";

        public string VoucherType { get; set; } = "";

        public string VoucherNumber { get; set; } = "";

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }
    }
}