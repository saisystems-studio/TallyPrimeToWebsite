namespace TallyWebAPI.DTOs
{
    public class CompanyDetailsDto
    {
        // Basic Details
        public string Name { get; set; } = "";
        public string MailingName { get; set; } = "";
        public string Address { get; set; } = "";

        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public string Pincode { get; set; } = "";

        public string Telephone { get; set; } = "";
        public string Mobile { get; set; } = "";
        public string Email { get; set; } = "";
        public string Website { get; set; } = "";

        // Financial Details
        public string StartingFrom { get; set; } = "";
        public string BooksFrom { get; set; } = "";

        public string CurrencySymbol { get; set; } = "";
        public string CurrencyFormalName { get; set; } = "";

        // GST Details
        public string Gstin { get; set; } = "";
        public string GstRegistrationType { get; set; } = "";
        public string GstRegistrationStatus { get; set; } = "";
        public string Gstr1Periodicity { get; set; } = "";

        // e-Invoice / GST Filing
        public string GstUsername { get; set; } = "";
        public string ModeOfFiling { get; set; } = "";
        public string EInvoiceApplicable { get; set; } = "";
    }
}