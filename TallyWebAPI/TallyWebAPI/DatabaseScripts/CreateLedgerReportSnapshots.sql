/* Additive Ledger Report snapshots. Inspect an existing table before applying; this script never drops or replaces data. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.LedgerReportSnapshots_tbl', N'U') IS NOT NULL OR OBJECT_ID(N'dbo.LedgerReportSnapshotDetails_tbl', N'U') IS NOT NULL
    THROW 51001, 'A Ledger Report snapshot table already exists; inspect its schema and data before applying this script.', 1;
CREATE TABLE dbo.LedgerReportSnapshots_tbl
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LedgerReportSnapshots_tbl PRIMARY KEY,
    CompanyId int NOT NULL, CompanyTallyGuid nvarchar(100) NOT NULL,
    LedgerId int NOT NULL, LedgerTallyGuid nvarchar(100) NOT NULL,
    FromDate date NOT NULL, ToDate date NOT NULL, VoucherCount int NOT NULL,
    OpeningBalance decimal(19,2) NOT NULL, TotalDebit decimal(19,2) NOT NULL,
    TotalCredit decimal(19,2) NOT NULL, ClosingBalance decimal(19,2) NOT NULL,
    SyncedAt datetime2(3) NOT NULL,
    CONSTRAINT FK_LedgerReportSnapshots_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies_tbl(Id),
    CONSTRAINT FK_LedgerReportSnapshots_Ledgers FOREIGN KEY (LedgerId) REFERENCES dbo.Ledgers_tbl(Id),
    CONSTRAINT CK_LedgerReportSnapshots_Period CHECK (FromDate <= ToDate)
);
CREATE UNIQUE INDEX UX_LedgerReportSnapshots_Scope ON dbo.LedgerReportSnapshots_tbl(CompanyId, CompanyTallyGuid, LedgerId, LedgerTallyGuid, FromDate, ToDate);
CREATE TABLE dbo.LedgerReportSnapshotDetails_tbl
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LedgerReportSnapshotDetails_tbl PRIMARY KEY,
    SnapshotId int NOT NULL, PostingOrder int NOT NULL, VoucherDate nvarchar(8) NOT NULL,
    Particulars nvarchar(1000) NOT NULL, VoucherType nvarchar(250) NOT NULL, VoucherNumber nvarchar(250) NOT NULL,
    Debit decimal(19,2) NOT NULL, Credit decimal(19,2) NOT NULL,
    CONSTRAINT FK_LedgerReportSnapshotDetails_Snapshots FOREIGN KEY (SnapshotId) REFERENCES dbo.LedgerReportSnapshots_tbl(Id) ON DELETE CASCADE
);
CREATE INDEX IX_LedgerReportSnapshotDetails_Order ON dbo.LedgerReportSnapshotDetails_tbl(SnapshotId, PostingOrder);
COMMIT TRANSACTION;
