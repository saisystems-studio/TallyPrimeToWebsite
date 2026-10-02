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
        public DbSet<OutstandingEntity> Outstandings => Set<OutstandingEntity>();

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

                entity.Property(x => x.ClosingQuantity)
                    .HasPrecision(18, 3);

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
        }
    }
}