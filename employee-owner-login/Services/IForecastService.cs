using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IForecastService
{
    // Builds the full forecast page: one row per active product, KPI counts, and chart data.
    Task<ForecastPageViewModel> GetForecastAsync();
}