namespace TallyWebAPI.DTOs
{
    public class LedgerDto
    {
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Parent { get; set; } = "";

        public string MailingName { get; set; } = "";
        public string Address { get; set; } = "";

        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public string Pincode { get; set; } = "";

        public string Pan { get; set; } = "";
        public string Gstin { get; set; } = "";
        public string RegistrationType { get; set; } = "";

        public string CreditPeriod { get; set; } = "";
        public string BillByBill { get; set; } = "";

        public string OpeningBalance { get; set; } = "";
        public string ClosingBalance { get; set; } = "";
    }
}