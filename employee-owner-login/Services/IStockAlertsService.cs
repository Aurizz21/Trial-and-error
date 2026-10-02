using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IStockAlertsService
{
    // Returns active (unresolved) alerts, optionally filtered by status ("All", "Critical", "Warning").
    Task<StockAlertsPageViewModel> GetActiveAsync(string statusFilter);
}