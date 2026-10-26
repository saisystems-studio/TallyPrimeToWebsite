namespace TallyWebAPI.Models;

public sealed class PurchaseRegisterEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public CompanyEntity? Company { get; set; }
    public string TallyGuid { get; set; } = "";
    public string VoucherNumber { get; set; } = "";
    public DateTime VoucherDate { get; set; }
    public string VoucherType { get; set; } = "";
    public string PartyLedgerName { get; set; } = "";
    public string PartyLedgerGuid { get; set; } = "";
    public string PurchaseLedgerNames { get; set; } = "";
    public string PurchaseLedgerGuids { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Narration { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PurchaseRegisterEntryEntity> Entries { get; set; } = new();
}
