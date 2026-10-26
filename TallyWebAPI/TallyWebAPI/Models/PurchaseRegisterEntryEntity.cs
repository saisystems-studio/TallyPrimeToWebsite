namespace TallyWebAPI.Models;

public sealed class PurchaseRegisterEntryEntity
{
    public int Id { get; set; }
    public int PurchaseRegisterId { get; set; }
    public PurchaseRegisterEntity? PurchaseRegister { get; set; }
    public string LedgerGuid { get; set; } = "";
    public string LedgerName { get; set; } = "";
    public decimal SignedAmount { get; set; }
    public int EntrySequence { get; set; }
    public bool IsPurchaseLedger { get; set; }
}
