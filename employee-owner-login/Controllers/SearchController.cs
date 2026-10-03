using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;

namespace PoultryOS.Controllers;

[Authorize]
public class SearchController : Controller
{
    private const int MinimumTermLength = 2;
    private const int ResultsPerGroup = 5;
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Query(string? term, DateTime? from, DateTime? to)
    {
        var query = term?.Trim() ?? string.Empty;
        if (query.Length < MinimumTermLength)
        {
            return Ok(new { products = Array.Empty<object>(), alerts = Array.Empty<object>(), sales = Array.Empty<object>() });
        }

        if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
        {
            return BadRequest(new { message = "The end date must be on or after the start date." });
        }

        var products = new List<object>();
        if (User.IsInRole("Owner"))
        {
            var productMatches = await _db.Products.AsNoTracking()
                .Where(product => product.IsActive && product.Name.Contains(query))
                .OrderBy(product => product.Name)
                .Take(ResultsPerGroup)
                .Select(product => new
                {
                    name = product.Name,
                    category = product.Category,
                    currentStock = product.CurrentStock,
                    units = product.Units,
                    href = "/Products/Index"
                })
                .ToListAsync();
            products.AddRange(productMatches);
        }

        var alerts = await _db.StockAlerts.AsNoTracking()
            .Where(alert => alert.ResolvedAt == null &&
                (alert.Product.Name.Contains(query) || alert.Status.Contains(query)))
            .OrderByDescending(alert => alert.Status == "Critical")
            .ThenByDescending(alert => alert.CreatedAt)
            .Take(ResultsPerGroup)
            .Select(alert => new
            {
                name = alert.Product.Name,
                status = alert.Status,
                currentStock = alert.CurrentStock,
                units = alert.Product.Units,
                href = "/Stock/Alerts"
            })
            .ToListAsync();

        var salesQuery = _db.Sales.AsNoTracking()
            .Where(sale => sale.Product.Name.Contains(query));
        if (from.HasValue) salesQuery = salesQuery.Where(sale => sale.SoldAt >= from.Value.Date);
        if (to.HasValue)
        {
            var endExclusive = to.Value.Date.AddDays(1);
            salesQuery = salesQuery.Where(sale => sale.SoldAt < endExclusive);
        }

        var sales = await salesQuery
            .OrderByDescending(sale => sale.SoldAt)
            .Take(ResultsPerGroup)
            .Select(sale => new
            {
                name = sale.Product.Name,
                soldAt = sale.SoldAt,
                quantity = sale.Quantity,
                units = sale.Product.Units,
                href = "/Sales/History"
            })
            .ToListAsync();

        return Ok(new { products, alerts, sales });
    }
}
