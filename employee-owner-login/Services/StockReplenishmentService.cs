using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using PoultryOS.Models.Entities;
using Product = PoultryOS.Models.Entities.Product;

namespace PoultryOS.Services;

public class StockReplenishmentService : IStockReplenishmentService
{
    private readonly AppDbContext _db;

    public StockReplenishmentService(AppDbContext db) => _db = db;

    public async Task<RestockResult> RestockAsync(RestockRequest request, string username)
    {
        // ---------- Validate ----------
        if (request.Quantity <= 0)
            return Fail("Quantity must be greater than zero.");

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);
        if (product is null)
            return Fail("That product no longer exists.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
            return Fail("Could not find your user account.");

        // ---------- Transaction: all-or-nothing ----------
        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            var stockBefore = product.CurrentStock;

            // 1. Insert the replenishment record
            var replenishment = new StockReplenishment
            {
                ProductId = product.Id,
                UserId = user.Id,
                Quantity = request.Quantity,
                ReplenishedAt = DateTime.Now,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            _db.StockReplenishments.Add(replenishment);

            // 2. Increase stock
            product.CurrentStock += request.Quantity;

            // 3. Resolve any open alert for this product
            var openAlert = await _db.StockAlerts
                .FirstOrDefaultAsync(a => a.ProductId == product.Id && a.ResolvedAt == null);

            var alertResolved = false;
            if (openAlert is not null)
            {
                openAlert.ResolvedAt = DateTime.Now;
                alertResolved = true;

                // Also add a notification that the alert was resolved
                _db.Notifications.Add(new Notification
                {
                    Type = "Info",
                    Title = $"Resolved: {product.Name}",
                    Message = $"{product.Name} restocked by {request.Quantity:0.##} {product.Units}. Stock is now {product.CurrentStock:0.##} {product.Units}.",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    StockAlertId = openAlert.Id
                });
            }

            // 4. Audit log
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = user.Id,
                ActionType = "Restock",
                Description = $"Restocked {product.Name}: +{request.Quantity:0.##} {product.Units} (was {stockBefore:0.##}, now {product.CurrentStock:0.##})."
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var message = alertResolved
                ? $"{product.Name} restocked by {request.Quantity:0.##} {product.Units}. Low-stock alert resolved."
                : $"{product.Name} restocked by {request.Quantity:0.##} {product.Units}.";

            return new RestockResult
            {
                Success = true,
                Message = message,
                NewStock = product.CurrentStock,
                ProductName = product.Name,
                AlertResolved = alertResolved
            };
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return Fail("Could not save the restock. Please try again.");
        }
    }

    private static RestockResult Fail(string message)
        => new() { Success = false, Message = message };
}