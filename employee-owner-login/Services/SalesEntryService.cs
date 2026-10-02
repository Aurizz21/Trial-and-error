using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using PoultryOS.Models.Entities;
using Product = PoultryOS.Models.Entities.Product;
using Sale = PoultryOS.Models.Entities.Sale;

namespace PoultryOS.Services;

public class SalesEntryService : ISalesEntryService
{
    private readonly AppDbContext _db;

    public SalesEntryService(AppDbContext db) => _db = db;

    // ==============================================================
    //  PAGE LOAD
    // ==============================================================

    public async Task<SalesEntryViewModel> GetEntryPageAsync()
    {
        // Active products, alphabetical
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PoultryOS.Models.Product   // ← the OLD view-model Product
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category,
                Units = p.Units,
                Supplier = p.Supplier,
                CurrentStock = p.CurrentStock,
                ReorderThreshold = p.ReorderThreshold,
                SalesHistory = new List<decimal>()      // not used on entry page
            })
            .ToListAsync();

        // Today's sales (00:00 → 23:59)
        var todayStart = DateTime.Today;
        var todayEnd = todayStart.AddDays(1);

        var todaySales = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.User)
            .Where(s => s.SoldAt >= todayStart && s.SoldAt < todayEnd)
            .OrderByDescending(s => s.SoldAt)
            .ToListAsync();

        var summary = new SalesSummaryViewModel
        {
            EntriesCount = todaySales.Count,
            ProductsAffectedCount = todaySales.Select(s => s.ProductId).Distinct().Count(),
            TotalProducts = products.Count,
            TotalPcsSold = todaySales
                .Where(s => s.Product.Units == "pcs")
                .Sum(s => s.Quantity),
            TotalKgSold = todaySales
                .Where(s => s.Product.Units == "kg")
                .Sum(s => s.Quantity)
        };

        var entries = todaySales.Select(s => new SaleRecord
        {
            Id = s.Id,
            ProductId = s.ProductId,
            ProductName = s.Product.Name,
            Quantity = s.Quantity,
            Unit = s.Product.Units,
            Date = s.SoldAt,
            EnteredBy = s.User.Username,
            Notes = s.Notes ?? string.Empty
        }).ToList();

        // Active alerts (unresolved)
        var alerts = await _db.StockAlerts
            .AsNoTracking()
            .Include(a => a.Product)
            .Where(a => a.ResolvedAt == null)
            .OrderByDescending(a => a.Status == "Critical")
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AlertItem
            {
                ProductId = a.ProductId,
                ProductName = a.Product.Name,
                Status = a.Status,
                CurrentStock = a.CurrentStock,
                DaysRemaining = a.DaysRemaining,
                Forecast = 0,
                SuggestedReorderQuantity = a.SuggestedReorderQuantity,
                Units = a.Product.Units,
                Message = a.Message
            })
            .ToListAsync();

        return new SalesEntryViewModel
        {
            Products = products,
            TodaysSummary = summary,
            TodaysEntries = entries,
            LowStockProducts = alerts
        };
    }

    // ==============================================================
    //  RECORD A SALE
    // ==============================================================

    public async Task<RecordSaleResult> RecordSaleAsync(
        int productId,
        decimal quantity,
        string username,
        string? notes,
        DateTime date)
    {
        // --- Validate ---
        if (quantity <= 0)
            return Fail("Quantity must be greater than zero.");

        if (date.Date > DateTime.Today)
            return Fail("Sale date cannot be in the future.");

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

        if (product is null)
            return Fail("That product no longer exists.");

        if (quantity > product.CurrentStock)
            return Fail($"Not enough stock. Only {product.CurrentStock:0.##} {product.Units} available.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
            return Fail("Could not find your user account.");

        // --- Start a transaction so all-or-nothing ---
        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1. Insert the sale
            var sale = new Sale
            {
                ProductId = product.Id,
                UserId = user.Id,
                Quantity = quantity,
                SoldAt = date.Date + DateTime.Now.TimeOfDay,   // use provided date + current time
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };
            _db.Sales.Add(sale);

            // 2. Decrement stock
            var stockBefore = product.CurrentStock;
            product.CurrentStock -= quantity;

            // 3. Audit log
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = user.Id,
                ActionType = "Sale",
                Description = $"Recorded sale: {quantity:0.##} {product.Units} of {product.Name}."
            });

            // Save so the sale gets an Id (needed for alerts/notifications)
            await _db.SaveChangesAsync();

            // 4. Regenerate alert for this product if needed
            await RegenerateAlertForProductAsync(product, user.Id);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // --- Build result ---
            var updatedProduct = new PoultryOS.Models.Product
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category,
                Units = product.Units,
                Supplier = product.Supplier,
                CurrentStock = product.CurrentStock,
                ReorderThreshold = product.ReorderThreshold
            };

            var newEntry = new SaleRecord
            {
                Id = sale.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = quantity,
                Unit = product.Units,
                Date = sale.SoldAt,
                EnteredBy = user.Username,
                Notes = sale.Notes ?? string.Empty
            };

            return new RecordSaleResult
            {
                Success = true,
                Message = $"Sale recorded: {quantity:0.##} {product.Units} of {product.Name}.",
                UpdatedProduct = updatedProduct,
                NewEntry = newEntry
            };
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return Fail("Could not save the sale. Please try again.");
        }
    }

    // ==============================================================
    //  ALERT REGENERATION
    // ==============================================================

    private async Task RegenerateAlertForProductAsync(Product product, int userId)
    {
        var isLow = product.CurrentStock <= product.ReorderThreshold;

        var existingAlert = await _db.StockAlerts
            .FirstOrDefaultAsync(a => a.ProductId == product.Id && a.ResolvedAt == null);

        if (isLow)
        {
            var status = "Critical";   // by paper: stock <= threshold → Critical
            var message = $"{product.Name} stock is low: {product.CurrentStock:0.##} {product.Units} remaining (threshold {product.ReorderThreshold:0.##}).";

            if (existingAlert is null)
            {
                // New alert
                var alert = new StockAlert
                {
                    ProductId = product.Id,
                    Status = status,
                    Message = message,
                    CurrentStock = product.CurrentStock,
                    DaysRemaining = 2,
                    SuggestedReorderQuantity = product.ReorderThreshold * 2,
                    CreatedAt = DateTime.Now
                };
                _db.StockAlerts.Add(alert);

                await _db.SaveChangesAsync();   // get the alert Id for the notification

                _db.Notifications.Add(new Notification
                {
                    Type = status,
                    Title = $"{status}: {product.Name}",
                    Message = message,
                    CreatedAt = alert.CreatedAt,
                    IsRead = false,
                    StockAlertId = alert.Id
                });

                _db.AuditLogs.Add(new AuditLog
                {
                    Timestamp = DateTime.Now,
                    UserId = userId,
                    ActionType = "Alert",
                    Description = $"{status} alert created for {product.Name}."
                });
            }
            else
            {
                // Update existing alert with the fresh stock value
                existingAlert.CurrentStock = product.CurrentStock;
                existingAlert.Message = message;
                existingAlert.Status = status;
            }
        }
        else
        {
            // Not low anymore -- resolve any open alert
            if (existingAlert is not null)
            {
                existingAlert.ResolvedAt = DateTime.Now;

                _db.AuditLogs.Add(new AuditLog
                {
                    Timestamp = DateTime.Now,
                    UserId = userId,
                    ActionType = "Alert",
                    Description = $"Alert resolved for {product.Name} (stock recovered)."
                });
            }
        }
    }

    // ==============================================================
    //  HELPER
    // ==============================================================

    private static RecordSaleResult Fail(string message)
        => new() { Success = false, Message = message };
}