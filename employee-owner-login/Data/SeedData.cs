using Microsoft.EntityFrameworkCore;
using PoultryOS.Models.Entities;

namespace PoultryOS.Data;

// Fills the database with sample data on first run.
// Safe to call every startup — it checks if data exists first.
public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (db.Users.Any())
        {
            return;
        }

        var rng = new Random(42); // fixed seed -> same data every fresh install

        // ---------------------------------------------------------------
        // 1. Users — using the SAME password hashes as AuthService.cs
        //    so login works with: owner/owner123 and employee/employee123
        // ---------------------------------------------------------------
        var owner = new User
        {
            Username = "owner",
            PasswordHash = "AQAAAAEAAYagAAAAENBVJptoK2HVEbw0IGMWsZDsSvQa0rVhb9pUBKduNqEC1b+9B3SoTSDFB9vBoCuc3w==",
            Role = "Owner",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-120)
        };

        var employee = new User
        {
            Username = "employee",
            PasswordHash = "AQAAAAEAAYagAAAAEEvy1hUK0cXXRnsY69v7OHrid8VsnyBCkjksWPax32ggkOIZtHoKp5QWayjjLJL+bw==",
            Role = "Employee",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-90)
        };

        db.Users.AddRange(owner, employee);
        db.SaveChanges();

        // ---------------------------------------------------------------
        // 2. Products
        // ---------------------------------------------------------------
        var products = new List<Product>
        {
            new Product { Name = "Whole Chicken",  Category = "Whole Chicken", Units = "kg", Supplier = "Sunrise Farms",   CurrentStock = 85, ReorderThreshold = 30 },
            new Product { Name = "Chicken Breast", Category = "Cuts & Parts",  Units = "kg", Supplier = "Sunrise Farms",   CurrentStock = 42, ReorderThreshold = 25 },
            new Product { Name = "Chicken Wings",  Category = "Cuts & Parts",  Units = "kg", Supplier = "Metro Poultry",   CurrentStock = 18, ReorderThreshold = 20 },
            new Product { Name = "Chicken Thighs", Category = "Cuts & Parts",  Units = "kg", Supplier = "Metro Poultry",   CurrentStock = 60, ReorderThreshold = 25 },
            new Product { Name = "Chicken Legs",   Category = "Cuts & Parts",  Units = "kg", Supplier = "Highland Poultry", CurrentStock = 12, ReorderThreshold = 20 }
        };

        db.Products.AddRange(products);
        db.SaveChanges();

        // ---------------------------------------------------------------
        // 3. Sales — realistic pattern over the last 100 days.
        //    Each product sells most days with a slight upward trend and weekend spikes.
        // ---------------------------------------------------------------
        var sales = new List<Sale>();
        var today = DateTime.Today;
        var startDay = today.AddDays(-100);
        var totalDays = 101; // inclusive of today

        for (var dayIndex = 0; dayIndex < totalDays; dayIndex++)
        {
            var day = startDay.AddDays(dayIndex);
            var isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;

            foreach (var product in products)
            {
                // Baseline daily sales per product.
                // Whole Chicken sells more by volume; cuts sell less.
                var baseDaily = product.Category == "Whole Chicken" ? 6.0 : 4.0;

                // Slight upward trend over 100 days (+30% growth across the window).
                var trend = baseDaily * (1.0 + 0.3 * (dayIndex / (double)totalDays));

                // Weekend bump: +60%
                var weekendMultiplier = isWeekend ? 1.6 : 1.0;

                // Small realistic noise: ±15%
                var noise = 1.0 + (rng.NextDouble() * 0.3 - 0.15);

                var quantity = Math.Round((decimal)Math.Max(1.0, trend * weekendMultiplier * noise), 2);

                var hour = rng.Next(7, 19);
                var minute = rng.Next(0, 60);

                sales.Add(new Sale
                {
                    ProductId = product.Id,
                    UserId = rng.Next(2) == 0 ? owner.Id : employee.Id,
                    Quantity = quantity,
                    SoldAt = day.AddHours(hour).AddMinutes(minute),
                    Notes = null
                });

                // Small chance of an extra sale for variety (25% of product-days)
                if (rng.NextDouble() < 0.25)
                {
                    var extraQty = Math.Round((decimal)Math.Max(1.0, baseDaily * 0.5 * (1 + rng.NextDouble() * 0.5)), 2);
                    sales.Add(new Sale
                    {
                        ProductId = product.Id,
                        UserId = rng.Next(2) == 0 ? owner.Id : employee.Id,
                        Quantity = extraQty,
                        SoldAt = day.AddHours(rng.Next(7, 19)).AddMinutes(rng.Next(0, 60)),
                        Notes = null
                    });
                }
            }
        }

        db.Sales.AddRange(sales);
        db.SaveChanges();

        // ---------------------------------------------------------------
        // 4. Stock Alerts for low-stock products + matching Notifications
        // ---------------------------------------------------------------
        var lowStockProducts = products
            .Where(p => p.CurrentStock <= p.ReorderThreshold)
            .ToList();

        foreach (var p in lowStockProducts)
        {
            var status = p.CurrentStock <= p.ReorderThreshold ? "Critical" : "Warning";
            var suggestedQty = Math.Round(p.ReorderThreshold * 2, 2);

            var alert = new StockAlert
            {
                ProductId = p.Id,
                Status = status,
                Message = $"{p.Name} stock is low: {p.CurrentStock} {p.Units} remaining (threshold {p.ReorderThreshold}).",
                CurrentStock = p.CurrentStock,
                DaysRemaining = 2,
                SuggestedReorderQuantity = suggestedQty,
                CreatedAt = DateTime.Now.AddHours(-rng.Next(1, 48))
            };

            db.StockAlerts.Add(alert);
            db.SaveChanges();

            db.Notifications.Add(new Notification
            {
                Type = status == "Critical" ? "Critical" : "Warning",
                Title = $"{status}: {p.Name}",
                Message = alert.Message,
                CreatedAt = alert.CreatedAt,
                IsRead = false,
                StockAlertId = alert.Id
            });
        }

        // ---------------------------------------------------------------
        // 5. AuditLog
        // ---------------------------------------------------------------
        db.AuditLogs.AddRange(
            new AuditLog { Timestamp = DateTime.Now.AddDays(-120), UserId = owner.Id,    ActionType = "Login",  Description = "Owner account created during initial setup." },
            new AuditLog { Timestamp = DateTime.Now.AddDays(-90),  UserId = owner.Id,    ActionType = "Login",  Description = "Employee account created." },
            new AuditLog { Timestamp = DateTime.Now.AddDays(-1),   UserId = owner.Id,    ActionType = "Sale",   Description = "Recorded batch sale entry." },
            new AuditLog { Timestamp = DateTime.Now.AddHours(-3),  UserId = employee.Id, ActionType = "Sale",   Description = "Recorded sale entry." },
            new AuditLog { Timestamp = DateTime.Now.AddHours(-1),  UserId = null,        ActionType = "System", Description = "Stock alerts regenerated based on current inventory." }
        );

        db.SaveChanges();
    }
}