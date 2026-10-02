using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using PoultryOS.Models.Entities;
using Product = PoultryOS.Models.Entities.Product;

namespace PoultryOS.Services;

public class ProductService : IProductService
{
    private static readonly string[] AllowedCategories = { "Whole Chicken", "Cuts & Parts" };
    private static readonly string[] AllowedUnits = { "pcs", "kg" };

    private readonly AppDbContext _db;

    public ProductService(AppDbContext db) => _db = db;

    // ---------- READ ----------

    public async Task<List<ProductRowViewModel>> GetAllAsync()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var weekly = await GetWeeklyTotalsAsync();

        return products
            .Select(p => ToRow(p, weekly.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<ProductRowViewModel?> GetByIdAsync(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product is null) return null;

        var weekly = await GetWeeklyTotalsAsync();
        return ToRow(product, weekly.GetValueOrDefault(product.Id));
    }

    // ---------- CREATE ----------

    public async Task<ServiceResult> CreateAsync(ProductFormRequest request, string username)
    {
        var error = Validate(request);
        if (error is not null) return ServiceResult.Fail(error);

        var name = request.Name.Trim();

        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Name == name);
        if (existing is not null && existing.IsActive)
            return ServiceResult.Fail($"A product named \"{name}\" already exists.");

        Product product;
        if (existing is not null)
        {
            product = existing;
            product.IsActive = true;
        }
        else
        {
            product = new Product();
            _db.Products.Add(product);
        }

        product.Name = name;
        product.Category = request.Category;
        product.Units = request.Units;
        product.Supplier = (request.Supplier ?? string.Empty).Trim();
        product.CurrentStock = request.CurrentStock;
        product.ReorderThreshold = request.ReorderThreshold;

        _db.AuditLogs.Add(await NewAuditAsync(username, $"Added product {name}."));
        return await SaveAsync($"{name} was added.");
    }

    // ---------- UPDATE ----------

    public async Task<ServiceResult> UpdateAsync(int id, ProductFormRequest request, string username)
    {
        var error = Validate(request);
        if (error is not null) return ServiceResult.Fail(error);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product is null) return ServiceResult.Fail("That product no longer exists.");

        var name = request.Name.Trim();
        var nameTaken = await _db.Products.AnyAsync(p => p.Id != id && p.Name == name);
        if (nameTaken) return ServiceResult.Fail($"A product named \"{name}\" already exists.");

        var changes = new List<string>();
        if (product.CurrentStock != request.CurrentStock)
            changes.Add($"stock {product.CurrentStock:0.##} to {request.CurrentStock:0.##}");
        if (product.ReorderThreshold != request.ReorderThreshold)
            changes.Add($"threshold {product.ReorderThreshold:0.##} to {request.ReorderThreshold:0.##}");

        product.Name = name;
        product.Category = request.Category;
        product.Supplier = (request.Supplier ?? string.Empty).Trim();
        product.CurrentStock = request.CurrentStock;
        product.ReorderThreshold = request.ReorderThreshold;

        var detail = changes.Count > 0 ? $" ({string.Join(", ", changes)})" : string.Empty;
        _db.AuditLogs.Add(await NewAuditAsync(username, $"Updated product {name}{detail}."));
        return await SaveAsync($"{name} was updated.");
    }

    public Task<ServiceResult> UpdateThresholdAsync(int id, decimal threshold, string username)
        => UpdateThresholdsAsync(new Dictionary<int, decimal> { [id] = threshold }, username);

    public async Task<ServiceResult> UpdateThresholdsAsync(IDictionary<int, decimal> thresholds, string username)
    {
        if (thresholds.Count == 0) return ServiceResult.Fail("No thresholds were sent.");
        if (thresholds.Values.Any(v => v < 0)) return ServiceResult.Fail("Thresholds must be 0 or more.");

        var ids = thresholds.Keys.ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync();

        var changed = 0;
        foreach (var product in products)
        {
            var newValue = thresholds[product.Id];
            if (product.ReorderThreshold == newValue) continue;
            product.ReorderThreshold = newValue;
            changed++;
        }

        if (changed == 0) return ServiceResult.Ok("No changes to save.");

        _db.AuditLogs.Add(await NewAuditAsync(username, $"Updated reorder thresholds for {changed} product(s)."));
        return await SaveAsync("Thresholds saved.");
    }

    // ---------- DELETE (soft delete) ----------

    public async Task<ServiceResult> DeleteAsync(int id, string username)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product is null) return ServiceResult.Fail("That product no longer exists.");

        product.IsActive = false;

        var openAlerts = await _db.StockAlerts
            .Where(a => a.ProductId == id && a.ResolvedAt == null)
            .ToListAsync();
        foreach (var alert in openAlerts) alert.ResolvedAt = DateTime.Now;

        _db.AuditLogs.Add(await NewAuditAsync(username, $"Deleted product {product.Name}."));
        return await SaveAsync($"{product.Name} was deleted.");
    }

    // ---------- helpers ----------

    private async Task<Dictionary<int, decimal>> GetWeeklyTotalsAsync()
    {
        var since = DateTime.Today.AddDays(-6);
        return await _db.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt >= since)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Total);
    }

    private static ProductRowViewModel ToRow(Product p, decimal weekly)
    {
        var (status, days) = Evaluate(p.CurrentStock, p.ReorderThreshold, weekly);
        return new ProductRowViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Category = p.Category,
            Units = p.Units,
            Supplier = p.Supplier,
            CurrentStock = p.CurrentStock,
            ReorderThreshold = p.ReorderThreshold,
            WeeklyConsumption = weekly,
            DaysRemaining = days,
            Status = status
        };
    }

    private static (string Status, double? DaysRemaining) Evaluate(decimal stock, decimal threshold, decimal weekly)
    {
        var avgDaily = weekly / 7m;
        double? days = avgDaily > 0 ? (double)(stock / avgDaily) : null;

        var critical = stock <= threshold || (weekly > 0 && stock < weekly * 0.20m);
        if (critical) return ("Critical", days);
        if (days.HasValue && days.Value <= 3) return ("Warning", days);
        return ("Healthy", days);
    }

    private static string? Validate(ProductFormRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (!AllowedCategories.Contains(r.Category)) return "Please choose a valid category.";
        if (!AllowedUnits.Contains(r.Units)) return "Please choose a valid unit.";
        if (r.CurrentStock < 0) return "Stock must be 0 or more.";
        if (r.ReorderThreshold < 0) return "Threshold must be 0 or more.";
        return null;
    }

    private async Task<AuditLog> NewAuditAsync(string username, string description)
    {
        var userId = await _db.Users
            .Where(u => u.Username == username)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        return new AuditLog
        {
            Timestamp = DateTime.Now,
            UserId = userId,
            ActionType = "Product",
            Description = description
        };
    }

    private async Task<ServiceResult> SaveAsync(string successMessage)
    {
        try
        {
            await _db.SaveChangesAsync();
            return ServiceResult.Ok(successMessage);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Could not save. Please check the values and try again.");
        }
    }
}