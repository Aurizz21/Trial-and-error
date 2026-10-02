using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using Product = PoultryOS.Models.Entities.Product;

namespace PoultryOS.Services;

public class ForecastService : IForecastService
{
    private const int HistoryDays = 8;      // need 7 for WMA + 1 actual to compare
    private const int WmaWindow = 7;
    private const double WarningDaysRemaining = 3d;

    private readonly AppDbContext _db;

    public ForecastService(AppDbContext db) => _db = db;

    public async Task<ForecastPageViewModel> GetForecastAsync()
    {
        var today = DateTime.Today;
        var historyStart = today.AddDays(-(HistoryDays - 1));

        // --- Active products ---
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // --- Last N days of sales, grouped per (product, day) ---
        var rawSales = await _db.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt >= historyStart && s.SoldAt < today.AddDays(1))
            .Select(s => new { s.ProductId, s.Quantity, s.SoldAt })
            .ToListAsync();

        // --- Build per-product daily arrays (oldest -> newest) ---
        var historyByProduct = new Dictionary<int, List<decimal>>();
        foreach (var product in products)
        {
            var days = new List<decimal>(HistoryDays);
            for (var d = 0; d < HistoryDays; d++)
            {
                var dayStart = historyStart.AddDays(d);
                var dayEnd = dayStart.AddDays(1);
                var total = rawSales
                    .Where(s => s.ProductId == product.Id && s.SoldAt >= dayStart && s.SoldAt < dayEnd)
                    .Sum(s => s.Quantity);
                days.Add(total);
            }
            historyByProduct[product.Id] = days;
        }

        // --- Build rows ---
        var rows = new List<ForecastRowViewModel>();
        foreach (var product in products)
        {
            var days = historyByProduct[product.Id];

            var last7 = days.Skip(days.Count - WmaWindow).Take(WmaWindow).ToList();
            var last7Sum = last7.Sum();
            var wma = CalculateWma(last7);
            var forecast7 = (decimal)wma * WmaWindow;

            var daysRemaining = wma > 0 ? (double)product.CurrentStock / (double)wma : 0d;
            var status = DetermineStatus(product, last7Sum, daysRemaining, wma);
            var depletion = wma > 0 && product.CurrentStock > 0
                ? DateTime.Today.AddDays((double)product.CurrentStock / (double)wma)
                : DateTime.Today;

            var suggestedReorder = wma > 0
                ? Math.Max(0m, Math.Round(forecast7 - product.CurrentStock, 0))
                : 0m;

            // Accuracy: use first 7 days to predict day 8, compare to actual.
            double? accuracy = null;
            if (days.Count >= WmaWindow + 1)
            {
                var prior7 = days.Skip(days.Count - WmaWindow - 1).Take(WmaWindow).ToList();
                var actual = days[days.Count - 1];
                if (prior7.Sum() > 0)
                {
                    var predicted = CalculateWma(prior7);
                    if (predicted > 0)
                    {
                        var error = actual == 0
                            ? 0d
                            : Math.Abs((double)actual - predicted) / (double)actual * 100d;
                        accuracy = Math.Round(100d - Math.Min(100d, error), 1);
                    }
                }
            }

            rows.Add(new ForecastRowViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Category = product.Category,
                Units = product.Units,
                CurrentStock = product.CurrentStock,
                ReorderThreshold = product.ReorderThreshold,
                Last7DaysSales = last7Sum,
                WeightedMovingAverage = Math.Round((decimal)wma, 2),
                ForecastNext7Days = Math.Round(forecast7, 2),
                DaysRemaining = Math.Round(daysRemaining, 1),
                PredictedDepletionDate = wma > 0 && product.CurrentStock > 0
                    ? depletion.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                    : "Insufficient data",
                Status = status,
                SuggestedReorderQuantity = suggestedReorder,
                ForecastAccuracy = accuracy
            });
        }

        // --- KPI counts ---
        var totalProducts = rows.Count;
        var criticalCount = rows.Count(r => r.Status == "Critical");
        var warningCount = rows.Count(r => r.Status == "Warning");
        var accuracyScores = rows.Where(r => r.ForecastAccuracy.HasValue).Select(r => r.ForecastAccuracy!.Value).ToList();
        var avgAccuracy = accuracyScores.Count > 0 ? Math.Round(accuracyScores.Average(), 1) : 0d;

        // --- Chart data ---
        var chartData = rows
            .Select(r => new ForecastChartPoint
            {
                Product = r.ProductName,
                Units = r.Units,
                Last7DaysActual = r.Last7DaysSales,
                Next7DaysForecast = r.ForecastNext7Days
            })
            .ToList();

        return new ForecastPageViewModel
        {
            Rows = rows,
            TotalProducts = totalProducts,
            CriticalCount = criticalCount,
            WarningCount = warningCount,
            AverageAccuracy = avgAccuracy,
            GeneratedAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture),
            Model = "7-Day Weighted Moving Average",
            ChartData = chartData
        };
    }

    // ==============================================================
    //  HELPERS
    // ==============================================================

    private static double CalculateWma(IReadOnlyList<decimal> sales)
    {
        if (sales.Count < WmaWindow) return 0;

        var trailing = sales.TakeLast(WmaWindow).ToList();
        var weightedTotal = 0d;
        for (var i = 0; i < trailing.Count; i++)
        {
            weightedTotal += (i + 1) * (double)trailing[i];
        }

        // Sum of weights 1+2+...+7 = 28
        return weightedTotal / 28d;
    }

    private static string DetermineStatus(Product product, decimal weeklyConsumption, double daysRemaining, double wma)
    {
        if (product.CurrentStock <= product.ReorderThreshold || product.CurrentStock < weeklyConsumption * 0.2m)
            return "Critical";

        if (wma > 0 && daysRemaining <= WarningDaysRemaining)
            return "Warning";

        return "Healthy";
    }
}