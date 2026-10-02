using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IDashboardService
{
    // Loads the entire dashboard page from the DB.
    Task<DashboardSummaryViewModel> GetSummaryAsync(string username, string role);
}