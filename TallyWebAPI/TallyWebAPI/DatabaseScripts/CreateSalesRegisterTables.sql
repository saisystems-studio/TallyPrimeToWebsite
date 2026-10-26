/*
  PHASE 2 ONLY: execute after separate approval. This script only creates the
  independent Sales Register tables. It does not alter or write existing data.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SalesRegister_tbl', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRegister_tbl
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesRegister_tbl PRIMARY KEY,
        CompanyId int NOT NULL,
        TallyGuid nvarchar(100) NOT NULL,
        VoucherNumber nvarchar(100) NOT NULL,
        VoucherDate date NOT NULL,
        VoucherType nvarchar(250) NOT NULL,
        PartyLedgerName nvarchar(250) NOT NULL,
        PartyLedgerGuid nvarchar(100) NULL,
        Amount decimal(19,2) NOT NULL,
        CreatedAt datetime2(3) NOT NULL,
        UpdatedAt datetime2(3) NOT NULL,
        CONSTRAINT FK_SalesRegister_tbl_Companies_tbl FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies_tbl(Id)
    );
    CREATE UNIQUE INDEX UX_SalesRegister_Company_TallyGuid ON dbo.SalesRegister_tbl(CompanyId, TallyGuid);
    CREATE INDEX IX_SalesRegister_Company_Date ON dbo.SalesRegister_tbl(CompanyId, VoucherDate);
END;

IF OBJECT_ID(N'dbo.SalesRegisterEntries_tbl', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRegisterEntries_tbl
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesRegisterEntries_tbl PRIMARY KEY,
        SalesRegisterId int NOT NULL,
        LedgerGuid nvarchar(100) NOT NULL,
        LedgerName nvarchar(250) NOT NULL,
        SignedAmount decimal(19,4) NOT NULL,
        EntrySequence int NOT NULL,
        CONSTRAINT FK_SalesRegisterEntries_tbl_SalesRegister_tbl FOREIGN KEY (SalesRegisterId)
            REFERENCES dbo.SalesRegister_tbl(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_SalesRegisterEntries_Voucher_Sequence ON dbo.SalesRegisterEntries_tbl(SalesRegisterId, EntrySequence);
    CREATE INDEX IX_SalesRegisterEntries_Ledger_Voucher ON dbo.SalesRegisterEntries_tbl(LedgerGuid, SalesRegisterId);
END;

COMMIT TRANSACTION;
