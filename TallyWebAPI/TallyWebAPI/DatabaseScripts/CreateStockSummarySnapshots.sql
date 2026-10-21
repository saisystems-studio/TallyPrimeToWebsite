/*
  Manual, additive schema script for Stock Summary period snapshots.
  Review and apply only after explicit approval. This file is not run at API startup.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.StockSummary_tbl', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.StockSummaryDetails_tbl', N'U') IS NOT NULL
    THROW 51000, 'A Stock Summary snapshot table already exists; inspect its schema before applying this script.', 1;

CREATE TABLE dbo.StockSummary_tbl
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockSummary_tbl PRIMARY KEY,
    CompanyId int NOT NULL,
    FromDate date NOT NULL,
    ToDate date NOT NULL,
    SyncedAt datetime2(3) NOT NULL,
    CONSTRAINT FK_StockSummary_tbl_Companies_tbl
        FOREIGN KEY (CompanyId) REFERENCES dbo.Companies_tbl(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_StockSummary_Period CHECK (FromDate <= ToDate)
);

CREATE UNIQUE INDEX UX_StockSummary_Company_Period
    ON dbo.StockSummary_tbl (CompanyId, FromDate, ToDate);

CREATE TABLE dbo.StockSummaryDetails_tbl
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockSummaryDetails_tbl PRIMARY KEY,
    StockSummaryId int NOT NULL,
    RowType nvarchar(16) NOT NULL,
    StockGroupName nvarchar(250) NOT NULL,
    StockGroupGuid nvarchar(100) NULL,
    ParentGroupName nvarchar(250) NULL,
    ParentGroupGuid nvarchar(100) NULL,
    StockItemName nvarchar(250) NULL,
    StockItemGuid nvarchar(100) NULL,
    Unit nvarchar(100) NULL,
    Level int NOT NULL,
    DisplayOrder int NOT NULL,
    OpeningQty decimal(28,8) NULL,
    OpeningQtyText nvarchar(250) NULL,
    OpeningRate decimal(28,8) NULL,
    OpeningRateText nvarchar(100) NULL,
    OpeningValue decimal(28,8) NULL,
    InwardQty decimal(28,8) NULL,
    InwardQtyText nvarchar(250) NULL,
    InwardRate decimal(28,8) NULL,
    InwardRateText nvarchar(100) NULL,
    InwardValue decimal(28,8) NULL,
    OutwardQty decimal(28,8) NULL,
    OutwardQtyText nvarchar(250) NULL,
    OutwardRate decimal(28,8) NULL,
    OutwardRateText nvarchar(100) NULL,
    OutwardValue decimal(28,8) NULL,
    ClosingQty decimal(28,8) NULL,
    ClosingQtyText nvarchar(250) NULL,
    ClosingRate decimal(28,8) NULL,
    ClosingRateText nvarchar(100) NULL,
    ClosingValue decimal(28,8) NULL,
    CONSTRAINT FK_StockSummaryDetails_tbl_StockSummary_tbl
        FOREIGN KEY (StockSummaryId) REFERENCES dbo.StockSummary_tbl(Id) ON DELETE CASCADE,
    CONSTRAINT CK_StockSummaryDetails_RowType
        CHECK (RowType IN (N'Group', N'Item', N'GrandTotal')),
    CONSTRAINT CK_StockSummaryDetails_RowIdentity CHECK
    (
        (RowType = N'Group' AND StockItemName IS NULL AND StockItemGuid IS NULL)
        OR (RowType = N'Item' AND StockItemName IS NOT NULL)
        OR (RowType = N'GrandTotal' AND StockItemName IS NULL AND StockItemGuid IS NULL)
    )
);

CREATE INDEX IX_StockSummaryDetails_Snapshot_Order
    ON dbo.StockSummaryDetails_tbl (StockSummaryId, DisplayOrder);

CREATE INDEX IX_StockSummaryDetails_Snapshot_Groups
    ON dbo.StockSummaryDetails_tbl (StockSummaryId, StockGroupGuid, ParentGroupGuid);

COMMIT TRANSACTION;
