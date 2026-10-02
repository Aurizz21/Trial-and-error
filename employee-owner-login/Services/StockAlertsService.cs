using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;

namespace PoultryOS.Services;

public class StockAlertsService : IStockAlertsService
{
    private readonly AppDbContext _db;

    public StockAlertsService(AppDbContext db) => _db = db;

    public async Task<StockAlertsPageViewModel> GetActiveAsync(string statusFilter)
    {
        // Normalise filter -- treat anything that isn't Critical/Warning as "All"
        var filter = statusFilter?.Trim() ?? "All";
        if (filter != "Critical" && filter != "Warning") filter = "All";

        // Base query: active alerts only (ResolvedAt is null), newest first
        var query = _db.StockAlerts
            .AsNoTracking()
            .Include(a => a.Product)
            .Where(a => a.ResolvedAt == null);

        // Counts BEFORE status filter -- so the KPI strip always shows the totals
        var criticalCount = await query.CountAsync(a => a.Status == "Critical");
        var warningCount = await query.CountAsync(a => a.Status == "Warning");
        var totalActive = criticalCount + warningCount;

        // Apply the status filter for the actual list
        if (filter == "Critical") query = query.Where(a => a.Status == "Critical");
        else if (filter == "Warning") query = query.Where(a => a.Status == "Warning");

        var rows = await query
            .OrderByDescending(a => a.Status == "Critical")   // Critical first
            .ThenByDescending(a => a.CreatedAt)               // then newest
            .Select(a => new StockAlertRowViewModel
            {
                Id = a.Id,
                ProductId = a.ProductId,
                ProductName = a.Product.Name,
                Units = a.Product.Units,
                Status = a.Status,
                CurrentStock = a.CurrentStock,
                DaysRemaining = a.DaysRemaining,
                SuggestedReorderQuantity = a.SuggestedReorderQuantity,
                Message = a.Message,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return new StockAlertsPageViewModel
        {
            Alerts = rows,
            TotalActive = totalActive,
            CriticalCount = criticalCount,
            WarningCount = warningCount,
            StatusFilter = filter
        };
    }
}