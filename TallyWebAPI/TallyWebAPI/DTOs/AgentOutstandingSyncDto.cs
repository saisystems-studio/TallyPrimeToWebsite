namespace TallyWebAPI.DTOs
{
    public class AgentOutstandingSyncDto
    {
        public string CompanyTallyGuid { get; set; } = "";

        public string LedgerName { get; set; } = "";

        public string TallyGuid { get; set; } = "";

        public string? VoucherNumber { get; set; }

        public string? VoucherType { get; set; }

        public string? VoucherDate { get; set; }

        public string BillReference { get; set; } = "";

        public string? BillType { get; set; }

        public string? BillDate { get; set; }

        public string? CreditPeriod { get; set; }

        public decimal Amount { get; set; }
    }

    public class AgentOutstandingSyncRequest
    {
        public List<AgentOutstandingSyncDto> Outstandings { get; set; }
            = new();
    }
}