namespace TallyWebAPI.Models;

public sealed class SalesRegisterEntryEntity
{
    public int Id { get; set; }
    public int SalesRegisterId { get; set; }
    public SalesRegisterEntity? SalesRegister { get; set; }
    public string LedgerGuid { get; set; } = "";
    public string LedgerName { get; set; } = "";
    public decimal SignedAmount { get; set; }
    public int EntrySequence { get; set; }
}
