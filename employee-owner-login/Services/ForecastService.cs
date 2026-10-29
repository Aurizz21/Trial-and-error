using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using Product = PoultryOS.Models.Entities.Product;

namespace PoultryOS.Services;

public class ForecastService : IForecastService
{
    private const int HistoryDays = 730;
    private const int WmaWindow = 7;

    private readonly AppDbContext _db;
    private readonly MlForecastService _mlService;
    private readonly StockRiskEvaluator _riskEvaluator;
    private readonly BacktestService _backtestService;

    public ForecastService(
        AppDbContext db,
        MlForecastService mlService,
        StockRiskEvaluator riskEvaluator,
        BacktestService backtestService)
    {
        _db = db;
        _mlService = mlService;
        _riskEvaluator = riskEvaluator;
        _backtestService = backtestService;
    }

    public async Task<ForecastPageViewModel> GetForecastAsync(bool useAi = true)
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
        var wma30DayCurve = new decimal[30];
        var ai30DayCurve = new float[30];
        foreach (var product in products)
        {
            var days = historyByProduct[product.Id];

            var last7 = days.Skip(days.Count - WmaWindow).Take(WmaWindow).ToList();
            var last7Sum = last7.Sum();
            var wma = _riskEvaluator.CalculateDailyWmaRate(last7);
            var forecast7 = Math.Round((decimal)wma * WmaWindow, 2);

            var suggestedReorder = wma > 0
                ? Math.Max(0m, Math.Round(forecast7 - product.CurrentStock, 0))
                : 0m;

            // Accuracy: use the 7 days before the most recent selling day.
            double? accuracy = null;
            if (days.Count >= WmaWindow + 1)
            {
                var lastSellingIndex = -1;
                for (var i = days.Count - 2; i >= 0; i--)   // days[^1] is today, which is incomplete
                {
                    if (days[i] > 0) { lastSellingIndex = i; break; }
                }

                if (lastSellingIndex >= WmaWindow)
                {
                    var actual = days[lastSellingIndex];
                    var prior = days.Skip(lastSellingIndex - WmaWindow).Take(WmaWindow).ToList();
                    var predicted = _riskEvaluator.CalculateDailyWmaRate(prior);
                    if (predicted > 0)
                    {
                        var error = Math.Abs((double)actual - predicted) / (double)actual * 100d;
                        accuracy = Math.Round(100d - Math.Min(100d, error), 1);
                    }
                }
            }

            // --- CALL THE AI HERE ---
            var mlResult = await _mlService.PredictAsync(product.Id, (float)product.CurrentStock);
            bool useAiForAlerts = useAi;
            var aiForecast7 = Math.Round((decimal)mlResult.Next30Days.Take(WmaWindow).Sum(), 2);
            var risk = _riskEvaluator.Evaluate(
                product.CurrentStock,
                product.ReorderThreshold,
                wma,
                (double)aiForecast7 / WmaWindow,
                useAiForAlerts);
            var activeForecast7 = risk.ActiveModel == "ML.NET" ? aiForecast7 : forecast7;
            var depletion = activeForecast7 > 0 && product.CurrentStock > 0
                ? DateTime.Today.AddDays((double)product.CurrentStock / ((double)activeForecast7 / WmaWindow))
                : DateTime.Today;
            var (aiAccuracy, wmaAccuracy) = _backtestService.CalculateAccuracies(
                days.Take(days.Count - 1).Select(quantity => (float)quantity).ToList());

            for (var dayIndex = 0; dayIndex < 30; dayIndex++)
            {
                wma30DayCurve[dayIndex] += (decimal)wma;
                if (dayIndex < mlResult.Next30Days.Length)
                    ai30DayCurve[dayIndex] += mlResult.Next30Days[dayIndex];
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
                ForecastNext7Days = forecast7,
                DaysRemaining = risk.DaysRemaining,
                PredictedDepletionDate = activeForecast7 > 0 && product.CurrentStock > 0
                    ? depletion.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                    : "Insufficient data",
                Status = risk.Status,
                SuggestedReorderQuantity = suggestedReorder,
                ForecastAccuracy = accuracy,
                WmaAccuracy = wmaAccuracy,
                AiAccuracy = aiAccuracy,
                UseAiForAlerts = useAiForAlerts,

                // --- AI FIELDS ---
                MlForecast7Days = aiForecast7,
                MlDepletionDate = mlResult.DepletionDate.HasValue
                    ? mlResult.DepletionDate.Value.ToString("MMM d, yyyy")
                    : "Insufficient data"
            });
        }

        // --- KPI counts ---
        var totalProducts = rows.Count;
        var criticalCount = rows.Count(r => r.Status == "Critical");
        var warningCount = rows.Count(r => r.Status == "Warning");
        var accuracyScores = rows.Where(r => r.ForecastAccuracy.HasValue).Select(r => r.ForecastAccuracy!.Value).ToList();
        var avgAccuracy = accuracyScores.Count > 0 ? Math.Round(accuracyScores.Average(), 1) : 0d;

        var chartData = rows
            .Select(r => new ForecastChartPoint
            {
                Product = r.ProductName,
                Units = r.Units,
                Last7DaysActual = r.Last7DaysSales,
                Next7DaysForecast = r.ForecastNext7Days,
                MlForecast7Days = r.MlForecast7Days
            })
            .ToList();

        // --- Persist snapshots (WMA) ---
        var forecastDayStart = DateTime.Today;
        var forecastDayEnd = forecastDayStart.AddDays(1);

        var existingToday = await _db.Forecasts
            .Where(f => f.GeneratedAt >= forecastDayStart && f.GeneratedAt < forecastDayEnd && f.ModelType == "WMA")
            .ToListAsync();

        foreach (var row in rows)
        {
            var existing = existingToday.FirstOrDefault(f => f.ProductId == row.ProductId);
            DateTime? depletionDate = null;
            if (DateTime.TryParse(row.PredictedDepletionDate, out var parsed))
                depletionDate = parsed;

            if (existing is null)
            {
                _db.Forecasts.Add(new PoultryOS.Models.Entities.Forecast
                {
                    ProductId = row.ProductId,
                    GeneratedAt = DateTime.Now,
                    WeightedMovingAverage = row.WeightedMovingAverage,
                    ForecastNext7Days = row.ForecastNext7Days,
                    PredictedDepletionDate = depletionDate,
                    ConfidenceScore = row.ForecastAccuracy,
                    Status = row.Status,
                    ModelType = "WMA"
                });
            }
            else
            {
                existing.GeneratedAt = DateTime.Now;
                existing.WeightedMovingAverage = row.WeightedMovingAverage;
                existing.ForecastNext7Days = row.ForecastNext7Days;
                existing.PredictedDepletionDate = depletionDate;
                existing.ConfidenceScore = row.ForecastAccuracy;
                existing.Status = row.Status;
            }
        }

        try { await _db.SaveChangesAsync(); } catch (DbUpdateException) { }

        return new ForecastPageViewModel
        {
            Rows = rows,
            TotalProducts = totalProducts,
            CriticalCount = criticalCount,
            WarningCount = warningCount,
            AverageAccuracy = avgAccuracy,
            Chart30DayLabels = Enumerable.Range(0, 30)
                .Select(dayOffset => today.AddDays(dayOffset).ToString("MMM d", CultureInfo.InvariantCulture))
                .ToList(),
            Wma30DayCurve = wma30DayCurve.ToList(),
            Ai30DayCurve = ai30DayCurve.ToList(),
            GeneratedAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture),
            Model = "7-Day Weighted Moving Average + ML.NET SSA",
            ChartData = chartData
        };
    }

}