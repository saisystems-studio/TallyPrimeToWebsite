-- Read-only inspection and post-sync reconciliation. No schema or data changes.
SET NOCOUNT ON;
SELECT name FROM sys.tables
WHERE name IN ('LedgerReportSnapshots_tbl', 'LedgerReportSnapshotDetails_tbl');
SELECT COUNT_BIG(*) AS HeaderCount FROM dbo.LedgerReportSnapshots_tbl;
SELECT COUNT_BIG(*) AS DetailCount FROM dbo.LedgerReportSnapshotDetails_tbl;

SELECT s.*, c.Name AS CompanyName, l.Name AS LedgerName
FROM dbo.LedgerReportSnapshots_tbl s
JOIN dbo.Companies_tbl c ON c.Id=s.CompanyId AND c.TallyGuid=s.CompanyTallyGuid
JOIN dbo.Ledgers_tbl l ON l.Id=s.LedgerId AND l.CompanyId=c.Id AND l.TallyGuid=s.LedgerTallyGuid
WHERE s.CompanyId=1 AND s.LedgerId=188
  AND s.FromDate=CONVERT(date,'20240401',112)
  AND s.ToDate=CONVERT(date,'20241231',112);

SELECT d.*
FROM dbo.LedgerReportSnapshotDetails_tbl d
JOIN dbo.LedgerReportSnapshots_tbl s ON s.Id=d.SnapshotId
WHERE s.CompanyId=1 AND s.LedgerId=188
  AND s.FromDate=CONVERT(date,'20240401',112)
  AND s.ToDate=CONVERT(date,'20241231',112)
ORDER BY s.Id,d.PostingOrder;

SELECT s.Id,s.VoucherCount,COUNT(d.Id) AS PostingCount,
       SUM(d.Debit) AS DetailDebit,SUM(d.Credit) AS DetailCredit,
       s.OpeningBalance,s.TotalDebit,s.TotalCredit,s.ClosingBalance,
       s.OpeningBalance-SUM(d.Debit)+SUM(d.Credit) AS CalculatedClosingBalance
FROM dbo.LedgerReportSnapshots_tbl s
LEFT JOIN dbo.LedgerReportSnapshotDetails_tbl d ON d.SnapshotId=s.Id
WHERE s.CompanyId=1 AND s.LedgerId=188
  AND s.FromDate=CONVERT(date,'20240401',112)
  AND s.ToDate=CONVERT(date,'20241231',112)
GROUP BY s.Id,s.VoucherCount,s.OpeningBalance,s.TotalDebit,s.TotalCredit,s.ClosingBalance;
