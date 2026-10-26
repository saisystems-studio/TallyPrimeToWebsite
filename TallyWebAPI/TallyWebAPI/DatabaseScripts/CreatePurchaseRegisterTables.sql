/* Additive schema only. This script is prepared for review and is not executed by the application. */
IF OBJECT_ID(N'dbo.PurchaseRegister_tbl', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseRegister_tbl
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseRegister_tbl PRIMARY KEY,
        CompanyId int NOT NULL,
        TallyGuid nvarchar(100) NOT NULL,
        VoucherNumber nvarchar(100) NOT NULL,
        VoucherDate date NOT NULL,
        VoucherType nvarchar(250) NOT NULL,
        PartyLedgerName nvarchar(250) NOT NULL,
        PartyLedgerGuid nvarchar(100) NOT NULL,
        PurchaseLedgerNames nvarchar(1000) NOT NULL,
        PurchaseLedgerGuids nvarchar(1000) NOT NULL,
        Amount decimal(19,2) NOT NULL,
        Debit decimal(19,2) NOT NULL,
        Credit decimal(19,2) NOT NULL,
        Narration nvarchar(2000) NOT NULL CONSTRAINT DF_PurchaseRegister_Narration DEFAULT(N''),
        CreatedAt datetime2(3) NOT NULL,
        UpdatedAt datetime2(3) NOT NULL,
        CONSTRAINT FK_PurchaseRegister_Companies FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies_tbl(Id) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX UX_PurchaseRegister_Company_TallyGuid
        ON dbo.PurchaseRegister_tbl(CompanyId, TallyGuid);
    CREATE INDEX IX_PurchaseRegister_Company_Date
        ON dbo.PurchaseRegister_tbl(CompanyId, VoucherDate);
END;

IF OBJECT_ID(N'dbo.PurchaseRegisterEntries_tbl', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseRegisterEntries_tbl
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseRegisterEntries_tbl PRIMARY KEY,
        PurchaseRegisterId int NOT NULL,
        LedgerGuid nvarchar(100) NOT NULL,
        LedgerName nvarchar(250) NOT NULL,
        SignedAmount decimal(19,4) NOT NULL,
        EntrySequence int NOT NULL,
        IsPurchaseLedger bit NOT NULL,
        CONSTRAINT FK_PurchaseRegisterEntries_PurchaseRegister FOREIGN KEY (PurchaseRegisterId)
            REFERENCES dbo.PurchaseRegister_tbl(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_PurchaseRegisterEntries_Voucher_Sequence
        ON dbo.PurchaseRegisterEntries_tbl(PurchaseRegisterId, EntrySequence);
    CREATE INDEX IX_PurchaseRegisterEntries_Ledger_Voucher
        ON dbo.PurchaseRegisterEntries_tbl(LedgerGuid, PurchaseRegisterId);
END;
