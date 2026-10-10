using Microsoft.EntityFrameworkCore;
using PoultryOS.Models.Entities;

namespace PoultryOS.Data;

// The bridge between your C# classes and the SQL Server database.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Each DbSet becomes one table.
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StockAlert> StockAlerts => Set<StockAlert>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StockReplenishment> StockReplenishments => Set<StockReplenishment>();
    public DbSet<Forecast> Forecasts => Set<Forecast>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Units).HasMaxLength(10);
            e.Property(x => x.Supplier).HasMaxLength(100);
            e.Property(x => x.CurrentStock).HasPrecision(18, 2);
            e.Property(x => x.ReorderThreshold).HasPrecision(18, 2);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.ProductId, x.SoldAt });

            e.HasOne(x => x.Product).WithMany(p => p.Sales)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(u => u.Sales)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.ActionType).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Timestamp);

            e.HasOne(x => x.User).WithMany(u => u.AuditLogs)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StockAlert>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.CurrentStock).HasPrecision(18, 2);
            e.Property(x => x.SuggestedReorderQuantity).HasPrecision(18, 2);

            e.HasOne(x => x.Product).WithMany(p => p.StockAlerts)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Message).HasMaxLength(500);

            e.HasOne(x => x.StockAlert).WithMany(a => a.Notifications)
                .HasForeignKey(x => x.StockAlertId).OnDelete(DeleteBehavior.SetNull);
        });

                modelBuilder.Entity<StockReplenishment>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.ProductId, x.ReplenishedAt });

            e.HasOne(x => x.Product).WithMany(p => p.Replenishments)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
                modelBuilder.Entity<Forecast>(e =>
        {
            e.Property(x => x.WeightedMovingAverage).HasPrecision(18, 2);
            e.Property(x => x.ForecastNext7Days).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.ProductId, x.GeneratedAt });

            e.HasOne(x => x.Product).WithMany(p => p.Forecasts)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}