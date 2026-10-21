namespace TallyWebAPI.DTOs
{
    public class VoucherDetailDto
    {
        public string Date { get; set; } = "";
        public string VoucherNumber { get; set; } = "";
        public string VoucherType { get; set; } = "";

        public string PartyName { get; set; } = "";
        public string PartyLedgerName { get; set; } = "";
        public string PartyAddress { get; set; } = "";
        public string PartyCity { get; set; } = "";
        public string PartyPincode { get; set; } = "";
        public string PartyCountry { get; set; } = "";
        public string PartyStateCode { get; set; } = "";
        public string PartyGstin { get; set; } = "";
        public InvoicePartyDto Consignee { get; set; } = new();
        public string CompanyGstin { get; set; } = "";
        public string CompanyState { get; set; } = "";

        public string State { get; set; } = "";
        public string PlaceOfSupply { get; set; } = "";

        public string Reference { get; set; } = "";
        public string ReferenceDate { get; set; } = "";
        public string OtherReferences { get; set; } = "";
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
        public string Discount { get; set; } = "";
    }

    public class InvoicePartyDto
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Gstin { get; set; } = "";
        public string State { get; set; } = "";
        public string StateCode { get; set; } = "";
        public string Pincode { get; set; } = "";
        public string Country { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Mobile { get; set; } = "";
    }

    public class InvoiceCompanyDto : InvoicePartyDto
    {
        public string TallyGuid { get; set; } = "";
    }
}
