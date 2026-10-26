namespace TallyWebAPI.Models;

public sealed class SalesRegisterEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public CompanyEntity? Company { get; set; }
    public string TallyGuid { get; set; } = "";
    public string VoucherNumber { get; set; } = "";
    public DateTime VoucherDate { get; set; }
    public string VoucherType { get; set; } = "";
    public string PartyLedgerName { get; set; } = "";
    public string? PartyLedgerGuid { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<SalesRegisterEntryEntity> Entries { get; set; } = new();
}
