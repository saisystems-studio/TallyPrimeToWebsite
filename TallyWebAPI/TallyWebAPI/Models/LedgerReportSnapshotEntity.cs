namespace TallyWebAPI.Models;

public sealed class LedgerReportSnapshotEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyTallyGuid { get; set; } = "";
    public int LedgerId { get; set; }
    public string LedgerTallyGuid { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int VoucherCount { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public DateTime SyncedAt { get; set; }
    public CompanyEntity? Company { get; set; }
    public List<LedgerReportSnapshotDetailEntity> Details { get; set; } = new();
}

public sealed class LedgerReportSnapshotDetailEntity
{
    public int Id { get; set; }
    public int SnapshotId { get; set; }
    public int PostingOrder { get; set; }
    public string VoucherDate { get; set; } = "";
    public string Particulars { get; set; } = "";
    public string VoucherType { get; set; } = "";
    public string VoucherNumber { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public LedgerReportSnapshotEntity? Snapshot { get; set; }
}
