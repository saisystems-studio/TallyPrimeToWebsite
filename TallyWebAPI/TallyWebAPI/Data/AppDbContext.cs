using Microsoft.EntityFrameworkCore;
using TallyWebAPI.Models;

namespace TallyWebAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<StockItem> StockItems => Set<StockItem>();
        public DbSet<LedgerEntity> Ledgers => Set<LedgerEntity>();
        public DbSet<CompanyEntity> Companies => Set<CompanyEntity>();
        public DbSet<VoucherEntity> Vouchers => Set<VoucherEntity>();
        public DbSet<VoucherLedgerEntryEntity> VoucherLedgerEntries => Set<VoucherLedgerEntryEntity>();
        public DbSet<OutstandingEntity> Outstandings => Set<OutstandingEntity>();
        public DbSet<StockSummarySnapshotEntity> StockSummarySnapshots => Set<StockSummarySnapshotEntity>();
        public DbSet<StockSummaryDetailEntity> StockSummaryDetails => Set<StockSummaryDetailEntity>();
        public DbSet<LedgerReportSnapshotEntity> LedgerReportSnapshots => Set<LedgerReportSnapshotEntity>();
        public DbSet<LedgerReportSnapshotDetailEntity> LedgerReportSnapshotDetails => Set<LedgerReportSnapshotDetailEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users_tbl");

                entity.HasKey(x => x.Id);


                entity.Property(x => x.Username)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => x.Username)
                    .IsUnique();

                entity.Property(x => x.PasswordHash)
                    .IsRequired();

                entity.Property(x => x.FullName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.Role)
                    .HasMaxLength(50)
                    .IsRequired();
            });

            // Stock Items
            modelBuilder.Entity<StockItem>(entity =>
            {
                entity.ToTable("StockItems_tbl");

                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.Name)
                    .HasMaxLength(250)
                    .IsRequired();

                entity.Property(x => x.StockGroup)
                    .HasMaxLength(150);

                entity.Property(x => x.Unit)
                    .HasMaxLength(50);


                entity.Property(x => x.OpeningQuantity)
                    .HasPrecision(18, 3);

                entity.Property(x => x.OpeningStock)
                    .HasPrecision(18, 2);

                entity.Property(x => x.OpeningRate)
                    .HasPrecision(18, 2);

                entity.Property(x => x.ClosingQuantity)
                    .HasPrecision(18, 3);

                // Standard Selling Rate
                entity.Property(x => x.StandardSellingRate)
                    .HasPrecision(18, 2);

                // Standard Cost Rate
                entity.Property(x => x.StandardCostRate)
                    .HasPrecision(18, 2);

                // Maximum Retail Price
                entity.Property(x => x.MRP)
                    .HasPrecision(18, 2);

                // HSN / SAC Code
                entity.Property(x => x.HsnSac)
                    .HasMaxLength(50);

                // Part Number
                entity.Property(x => x.PartNumber)
                    .HasMaxLength(100);

                entity.Property(x => x.TallyGuid)
                 .HasMaxLength(100);

                entity.HasIndex(x => new
                {
                    x.CompanyId,
                    x.TallyGuid
                })
                .IsUnique()
                .HasDatabaseName("UX_StockItems_Company_TallyGuid")
                .HasFilter("[CompanyId] IS NOT NULL AND [TallyGuid] IS NOT NULL");

                entity.Property(x => x.MasterId);

                entity.Property(x => x.AlterId);
            });

            modelBuilder.Entity<LedgerEntity>(entity =>
            {
                entity.ToTable("Ledgers_tbl");

                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Company)
       .WithMany()
       .HasForeignKey(x => x.CompanyId)
       .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.TallyGuid)
                    .HasMaxLength(100);

                entity.HasIndex(x => x.TallyGuid)
                    .IsUnique()
                    .HasFilter("[TallyGuid] IS NOT NULL");

                entity.Property(x => x.Name)
                    .HasMaxLength(250)
                    .IsRequired();

                entity.Property(x => x.Parent)
                    .HasMaxLength(250);

                entity.Property(x => x.Alias)
                    .HasMaxLength(250);

                entity.Property(x => x.MailingName)
                    .HasMaxLength(250);

                entity.Property(x => x.State)
                    .HasMaxLength(150);

                entity.Property(x => x.Country)
                    .HasMaxLength(150);

                entity.Property(x => x.Pincode)
                    .HasMaxLength(20);

                entity.Property(x => x.Pan)
                    .HasMaxLength(50);

                entity.Property(x => x.Gstin)
                    .HasMaxLength(50);

                entity.Property(x => x.RegistrationType)
                    .HasMaxLength(100);

                entity.Property(x => x.CreditPeriod)
                    .HasMaxLength(100);

                entity.Property(x => x.BillByBill)
                    .HasMaxLength(20);

                entity.Property(x => x.OpeningBalance)
                    .HasMaxLength(100);

                entity.Property(x => x.ClosingBalance)
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<CompanyEntity>(entity =>
            {
                entity.ToTable("Companies_tbl");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.TallyGuid)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => x.TallyGuid)
                    .IsUnique();

                entity.Property(x => x.Name)
                    .HasMaxLength(250)
                    .IsRequired();

                entity.Property(x => x.FormalName)
                    .HasMaxLength(250);

                entity.Property(x => x.State)
                    .HasMaxLength(150);

                entity.Property(x => x.Country)
                    .HasMaxLength(150);

                entity.Property(x => x.Pincode)
                    .HasMaxLength(20);

                entity.Property(x => x.Email)
                    .HasMaxLength(250);

                entity.Property(x => x.Phone)
                    .HasMaxLength(50);

                entity.Property(x => x.Gstin)
                    .HasMaxLength(50);

                entity.Property(x => x.GstRegistrationType)
                    .HasMaxLength(100);

                entity.Property(x => x.StartingFrom)
                    .HasMaxLength(8);

                entity.Property(x => x.FinancialYearEnd)
                    .HasMaxLength(8);

                entity.Property(x => x.BooksFrom)
                    .HasMaxLength(8);
            });

            // Vouchers
            modelBuilder.Entity<VoucherEntity>(entity =>
            {
                entity.ToTable("Vouchers_tbl");

                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.TallyGuid)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => new
                {
                    x.CompanyId,
                    x.TallyGuid
                })
                .IsUnique()
                .HasDatabaseName("UX_Vouchers_Company_TallyGuid");

                entity.Property(x => x.VoucherNumber)
                    .HasMaxLength(100);

                entity.Property(x => x.VoucherType)
                    .HasMaxLength(100);

                entity.Property(x => x.VoucherDate)
                    .HasMaxLength(8);

                entity.Property(x => x.PartyName)
                    .HasMaxLength(250);

                entity.Property(x => x.PartyGstin)
                    .HasMaxLength(50);

                entity.Property(x => x.State)
                    .HasMaxLength(150);

                entity.Property(x => x.PlaceOfSupply)
                    .HasMaxLength(150);

                entity.Property(x => x.Amount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Narration);

                entity.HasMany(x => x.LedgerEntries)
                    .WithOne(x => x.Voucher)
                    .HasForeignKey(x => x.VoucherId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<VoucherLedgerEntryEntity>(entity =>
            {
                entity.ToTable("VoucherLedgerEntries_tbl");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.LedgerName)
                    .HasMaxLength(250)
                    .IsRequired();
                entity.Property(x => x.Amount)
                    .HasPrecision(18, 4);
                entity.HasIndex(x => new { x.LedgerId, x.VoucherId })
                    .HasDatabaseName("IX_VoucherLedgerEntries_Ledger_Voucher");
            });

            modelBuilder.Entity<OutstandingEntity>(entity =>
            {
                entity.ToTable("Outstanding_tbl");

                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.LedgerName)
                    .HasMaxLength(250)
                    .IsRequired();

                entity.Property(x => x.TallyGuid)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.VoucherNumber).HasMaxLength(100);
                entity.Property(x => x.VoucherType).HasMaxLength(100);
                entity.Property(x => x.VoucherDate).HasMaxLength(8);

                entity.Property(x => x.BillReference)
                    .HasMaxLength(250)
                    .IsRequired();

                entity.Property(x => x.BillType).HasMaxLength(50);
                entity.Property(x => x.BillDate).HasMaxLength(8);
                entity.Property(x => x.CreditPeriod).HasMaxLength(100);

                entity.Property(x => x.Amount)
                    .HasPrecision(18, 2);

                entity.HasIndex(x => new
                {
                    x.CompanyId,
                    x.TallyGuid,
                    x.LedgerName,
                    x.BillReference,
                    x.BillType
                })
                .HasDatabaseName("IX_Outstanding_Company_Voucher_Bill");
            });

            modelBuilder.Entity<StockSummarySnapshotEntity>(entity =>
            {
                entity.ToTable("StockSummary_tbl", table => table.HasCheckConstraint(
                    "CK_StockSummary_Period", "[FromDate] <= [ToDate]"));
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(x => x.FromDate).HasColumnType("date");
                entity.Property(x => x.ToDate).HasColumnType("date");
                entity.Property(x => x.SyncedAt).HasColumnType("datetime2(3)");
                entity.HasIndex(x => new { x.CompanyId, x.FromDate, x.ToDate })
                    .IsUnique()
                    .HasDatabaseName("UX_StockSummary_Company_Period");
            });

            modelBuilder.Entity<StockSummaryDetailEntity>(entity =>
            {
                entity.ToTable("StockSummaryDetails_tbl", table =>
                {
                    table.HasCheckConstraint("CK_StockSummaryDetails_RowType",
                        "[RowType] IN ('Group', 'Item', 'GrandTotal')");
                    table.HasCheckConstraint("CK_StockSummaryDetails_RowIdentity",
                        "([RowType] = 'Group' AND [StockItemName] IS NULL AND [StockItemGuid] IS NULL) OR ([RowType] = 'Item' AND [StockItemName] IS NOT NULL) OR ([RowType] = 'GrandTotal' AND [StockItemName] IS NULL AND [StockItemGuid] IS NULL)");
                });
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.StockSummary)
                    .WithMany(x => x.Details)
                    .HasForeignKey(x => x.StockSummaryId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(x => x.RowType).HasMaxLength(16).IsRequired();
                entity.Property(x => x.StockGroupName).HasMaxLength(250).IsRequired();
                entity.Property(x => x.StockGroupGuid).HasMaxLength(100);
                entity.Property(x => x.ParentGroupName).HasMaxLength(250);
                entity.Property(x => x.ParentGroupGuid).HasMaxLength(100);
                entity.Property(x => x.StockItemName).HasMaxLength(250);
                entity.Property(x => x.StockItemGuid).HasMaxLength(100);
                entity.Property(x => x.Unit).HasMaxLength(100);
                entity.Property(x => x.OpeningQtyText).HasMaxLength(250);
                entity.Property(x => x.OpeningRateText).HasMaxLength(100);
                entity.Property(x => x.InwardQtyText).HasMaxLength(250);
                entity.Property(x => x.InwardRateText).HasMaxLength(100);
                entity.Property(x => x.OutwardQtyText).HasMaxLength(250);
                entity.Property(x => x.OutwardRateText).HasMaxLength(100);
                entity.Property(x => x.ClosingQtyText).HasMaxLength(250);
                entity.Property(x => x.ClosingRateText).HasMaxLength(100);
                entity.HasIndex(x => new { x.StockSummaryId, x.DisplayOrder })
                    .HasDatabaseName("IX_StockSummaryDetails_Snapshot_Order");
                entity.HasIndex(x => new { x.StockSummaryId, x.StockGroupGuid, x.ParentGroupGuid })
                    .HasDatabaseName("IX_StockSummaryDetails_Snapshot_Groups");
                foreach (var property in new[]
                {
                    nameof(StockSummaryDetailEntity.OpeningQty), nameof(StockSummaryDetailEntity.OpeningRate),
                    nameof(StockSummaryDetailEntity.OpeningValue), nameof(StockSummaryDetailEntity.InwardQty),
                    nameof(StockSummaryDetailEntity.InwardRate), nameof(StockSummaryDetailEntity.InwardValue),
                    nameof(StockSummaryDetailEntity.OutwardQty), nameof(StockSummaryDetailEntity.OutwardRate),
                    nameof(StockSummaryDetailEntity.OutwardValue), nameof(StockSummaryDetailEntity.ClosingQty),
                    nameof(StockSummaryDetailEntity.ClosingRate), nameof(StockSummaryDetailEntity.ClosingValue)
                })
                    entity.Property<decimal?>(property).HasPrecision(28, 8);
            });

            modelBuilder.Entity<LedgerReportSnapshotEntity>(entity =>
            {
                entity.ToTable("LedgerReportSnapshots_tbl", table => table.HasCheckConstraint(
                    "CK_LedgerReportSnapshots_Period", "[FromDate] <= [ToDate]"));
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(x => x.CompanyTallyGuid).HasMaxLength(100).IsRequired();
                entity.Property(x => x.LedgerTallyGuid).HasMaxLength(100).IsRequired();
                entity.Property(x => x.FromDate).HasColumnType("date");
                entity.Property(x => x.ToDate).HasColumnType("date");
                entity.Property(x => x.SyncedAt).HasColumnType("datetime2(3)");
                foreach (var property in new[] { nameof(LedgerReportSnapshotEntity.OpeningBalance), nameof(LedgerReportSnapshotEntity.TotalDebit), nameof(LedgerReportSnapshotEntity.TotalCredit), nameof(LedgerReportSnapshotEntity.ClosingBalance) })
                    entity.Property<decimal>(property).HasPrecision(19, 2);
                entity.HasOne<LedgerEntity>().WithMany().HasForeignKey(x => x.LedgerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(x => new { x.CompanyId, x.CompanyTallyGuid, x.LedgerId, x.LedgerTallyGuid, x.FromDate, x.ToDate }).IsUnique()
                    .HasDatabaseName("UX_LedgerReportSnapshots_Scope");
            });
            modelBuilder.Entity<LedgerReportSnapshotDetailEntity>(entity =>
            {
                entity.ToTable("LedgerReportSnapshotDetails_tbl");
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Snapshot).WithMany(x => x.Details).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
                entity.Property(x => x.VoucherDate).HasMaxLength(8).IsRequired();
                entity.Property(x => x.Particulars).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.VoucherType).HasMaxLength(250).IsRequired();
                entity.Property(x => x.VoucherNumber).HasMaxLength(250).IsRequired();
                entity.Property(x => x.Debit).HasPrecision(19, 2);
                entity.Property(x => x.Credit).HasPrecision(19, 2);
                entity.HasIndex(x => new { x.SnapshotId, x.PostingOrder }).HasDatabaseName("IX_LedgerReportSnapshotDetails_Order");
            });
        }
    }
}
