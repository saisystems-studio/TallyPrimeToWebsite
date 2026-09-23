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
        }
    }
}