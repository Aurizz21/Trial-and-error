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
    private readonly StockRiskEvaluator _riskEvaluator;
    private readonly IAlertService _alertService;

    public ProductService(
        AppDbContext db,
        StockRiskEvaluator riskEvaluator,
        IAlertService alertService)
    {
        _db = db;
        _riskEvaluator = riskEvaluator;
        _alertService = alertService;
    }

    // ---------- READ ----------

    public async Task<List<ProductRowViewModel>> GetAllAsync()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var salesMetrics = await GetWeeklyMetricsAsync();
        var aiDailyRates = await GetAiDailyRatesAsync(products.Select(p => p.Id).ToList());

        return products
            .Select(p => ToRow(
                p,
                salesMetrics.GetValueOrDefault(p.Id),
                aiDailyRates.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<ProductRowViewModel?> GetByIdAsync(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product is null) return null;

        var salesMetrics = await GetWeeklyMetricsAsync();
        var aiDailyRates = await GetAiDailyRatesAsync(new[] { product.Id });
        return ToRow(
            product,
            salesMetrics.GetValueOrDefault(product.Id),
            aiDailyRates.GetValueOrDefault(product.Id));
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
        var successMessage = $"{name} was added.";
        var saveResult = await SaveAsync(successMessage);
        if (!saveResult.Success) return saveResult;

        var userId = await _db.Users
            .Where(u => u.Username == username)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
        await _alertService.RegenerateAlertForProductAsync(product, userId);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(entry => entry.State != EntityState.Unchanged)
                         .ToList())
                entry.State = EntityState.Detached;
        }

        return ServiceResult.Ok(successMessage);
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

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (product.Units != request.Units
                && (await _db.Sales.AnyAsync(s => s.ProductId == product.Id)
                    || await _db.StockReplenishments.AnyAsync(r => r.ProductId == product.Id)))
            {
                await tx.RollbackAsync();
                return ServiceResult.Fail("Unit cannot be changed because this product already has sales or restocks.");
            }

            var changes = new List<string>();
            if (product.CurrentStock != request.CurrentStock)
                changes.Add($"stock {product.CurrentStock:0.##} to {request.CurrentStock:0.##}");
            if (product.ReorderThreshold != request.ReorderThreshold)
                changes.Add($"threshold {product.ReorderThreshold:0.##} to {request.ReorderThreshold:0.##}");
            if (product.Units != request.Units)
                changes.Add($"unit {product.Units} to {request.Units}");

            product.Name = name;
            product.Category = request.Category;
            product.Units = request.Units;
            product.Supplier = (request.Supplier ?? string.Empty).Trim();
            product.CurrentStock = request.CurrentStock;
            product.ReorderThreshold = request.ReorderThreshold;

            var userId = await _db.Users
                .Where(u => u.Username == username)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
            await _alertService.RegenerateAlertForProductAsync(product, userId);

            var detail = changes.Count > 0 ? $" ({string.Join(", ", changes)})" : string.Empty;
            _db.AuditLogs.Add(await NewAuditAsync(username, $"Updated product {name}{detail}."));
            var result = await SaveAsync($"{name} was updated.");
            if (!result.Success)
            {
                await tx.RollbackAsync();
                return result;
            }

            await tx.CommitAsync();
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public Task<ServiceResult> UpdateThresholdAsync(int id, decimal threshold, string username)
        => UpdateThresholdsAsync(new Dictionary<int, decimal> { [id] = threshold }, username);

    public async Task<ServiceResult> UpdateThresholdsAsync(IDictionary<int, decimal> thresholds, string username)
    {
        if (thresholds.Count == 0) return ServiceResult.Fail("No thresholds were sent.");
        if (thresholds.Values.Any(v => v < 0)) return ServiceResult.Fail("Thresholds must be 0 or more.");

        var ids = thresholds.Keys.ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync();

        var changedProducts = products
            .Where(product => product.ReorderThreshold != thresholds[product.Id])
            .ToList();

        if (changedProducts.Count == 0) return ServiceResult.Ok("No changes to save.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var userId = await _db.Users
                .Where(u => u.Username == username)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
            foreach (var product in changedProducts)
            {
                product.ReorderThreshold = thresholds[product.Id];
                await _alertService.RegenerateAlertForProductAsync(product, userId);
            }

            _db.AuditLogs.Add(await NewAuditAsync(username, $"Updated reorder thresholds for {changedProducts.Count} product(s)."));
            var result = await SaveAsync("Thresholds saved.");
            if (!result.Success)
            {
                await tx.RollbackAsync();
                return result;
            }

            await tx.CommitAsync();
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
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

    private async Task<Dictionary<int, ProductSalesMetrics>> GetWeeklyMetricsAsync()
    {
        var since = DateTime.Today.AddDays(-6);
        var sales = await _db.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt >= since)
            .Select(s => new { s.ProductId, s.Quantity, s.SoldAt })
            .ToListAsync();

        return sales
            .GroupBy(s => s.ProductId)
            .ToDictionary(group => group.Key, group =>
            {
                var dailyTotals = group
                    .GroupBy(s => s.SoldAt.Date)
                    .ToDictionary(day => day.Key, day => day.Sum(s => s.Quantity));
                var dailySales = Enumerable.Range(0, 7)
                    .Select(offset => dailyTotals.GetValueOrDefault(since.Date.AddDays(offset)))
                    .ToList();

                return new ProductSalesMetrics(
                    dailySales.Sum(),
                    _riskEvaluator.CalculateDailyWmaRate(dailySales));
            });
    }

    private async Task<Dictionary<int, double>> GetAiDailyRatesAsync(IReadOnlyCollection<int> productIds)
    {
        if (productIds.Count == 0) return new Dictionary<int, double>();

        var today = DateTime.Today;
        var forecasts = await _db.Forecasts
            .AsNoTracking()
            .Where(f =>
                productIds.Contains(f.ProductId)
                && f.ModelType == "ML.NET"
                && f.GeneratedAt >= today)
            .OrderByDescending(f => f.GeneratedAt)
            .Select(f => new { f.ProductId, f.ForecastNext7Days, f.GeneratedAt })
            .ToListAsync();

        return forecasts
            .GroupBy(f => f.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (double)group.First().ForecastNext7Days / 7d);
    }

    private ProductRowViewModel ToRow(Product p, ProductSalesMetrics metrics, double aiDailyRate)
    {
        var risk = _riskEvaluator.Evaluate(
            p.CurrentStock,
            p.ReorderThreshold,
            metrics.DailyWmaRate,
            aiDailyRate,
            useAiForAlerts: true);
        return new ProductRowViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Category = p.Category,
            Units = p.Units,
            Supplier = p.Supplier,
            CurrentStock = p.CurrentStock,
            ReorderThreshold = p.ReorderThreshold,
            WeeklyConsumption = metrics.WeeklyTotal,
            DaysRemaining = metrics.DailyWmaRate > 0 ? risk.DaysRemaining : null,
            Status = risk.Status
        };
    }

    private readonly record struct ProductSalesMetrics(decimal WeeklyTotal, double DailyWmaRate);

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