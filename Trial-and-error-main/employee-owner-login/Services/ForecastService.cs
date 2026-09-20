using PoultryOS.Models;

namespace PoultryOS.Services;

/// <summary>
/// Turns raw inventory data into everything the dashboard shows: 7-day weighted moving average
/// forecasts, depletion dates, alert status, reorder suggestions and forecast accuracy.
/// It is a pure calculation (no storage, no clock), so it is easy to unit test.
/// </summary>
public sealed class ForecastService
{
    /// <summary>Days of sales the moving average looks at.</summary>
    public const int WindowDays = 7;

    /// <summary>Days shown on the sales chart (ends with today).</summary>
    public const int ChartDays = 14;

    /// <summary>A reorder suggestion covers this many days of forecast demand.</summary>
    public const int SafetyBufferDays = 14;

    /// <summary>Critical when stock is under this share of last week's consumption.</summary>
    public const double CriticalShareOfWeeklyUse = 0.20;

    /// <summary>Warning when stock is within this multiple of the owner's reorder level.</summary>
    public const double WarningThresholdFactor = 1.5;

    /// <summary>Warning when the forecast says stock runs out within this many days.</summary>
    public const int WarningDaysLeft = 7;

    /// <summary>Non-functional requirement: forecasts should reach at least 80% accuracy.</summary>
    public const double AccuracyTarget = 0.80;

    // A product must have sold at least this many units in the scoring week to count towards
    // the headline accuracy. Two vials in a week says nothing about the model.
    private const int MinUnitsToScore = 7;

    public DashboardSnapshot Build(InventoryData data, DateOnly today, bool includeActivity)
    {
        var soldByProduct = data.Sales
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.GroupBy(s => s.Date).ToDictionary(d => d.Key, d => d.Sum(s => s.Quantity)));

        var dates = Enumerable.Range(0, ChartDays).Select(i => today.AddDays(i - (ChartDays - 1))).ToList();

        var scored = data.Products
            .Select(p => BuildProduct(
                p,
                soldByProduct.TryGetValue(p.Id, out var sold) ? sold : new Dictionary<DateOnly, int>(),
                today,
                dates))
            .ToList();

        var products = scored
            .Select(s => s.Forecast)
            .OrderBy(p => StockStatus.Severity(p.Status))
            .ThenBy(p => p.DaysLeft ?? double.MaxValue)
            .ThenBy(p => p.Name)
            .ToList();

        var accuracies = scored
            .Where(s => s.BacktestUnits >= MinUnitsToScore && s.Forecast.Confidence.HasValue)
            .Select(s => s.Forecast.Confidence!.Value)
            .ToList();

        var kpis = new DashboardKpis(
            TotalStock: products.Sum(p => p.Stock),
            ProductCount: products.Count,
            SoldToday: products.Sum(p => p.SoldToday),
            EntriesToday: data.Sales.Count(s => s.Date == today),
            Critical: products.Count(p => p.Status == StockStatus.Critical),
            Warning: products.Count(p => p.Status == StockStatus.Warning),
            Healthy: products.Count(p => p.Status == StockStatus.Healthy),
            Accuracy: accuracies.Count > 0 ? accuracies.Average() : null,
            AccuracyTarget: AccuracyTarget);

        var categories = products
            .GroupBy(p => p.Category)
            .Select(g => new CategoryUsage(g.Key, g.Sum(p => p.WeeklyConsumption)))
            .OrderByDescending(c => c.Units)
            .ToList();

        var activity = includeActivity
            ? data.Audit
                .OrderByDescending(a => a.Timestamp)
                .Take(8)
                .Select(a => new ActivityItem(a.Timestamp, a.User, a.Action, a.Description))
                .ToList()
            : null;

        return new DashboardSnapshot(
            GeneratedAt: DateTimeOffset.UtcNow,
            Today: today.ToString("yyyy-MM-dd"),
            Dates: dates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            Kpis: kpis,
            Products: products,
            Categories: categories,
            Activity: activity);
    }

    private static (ProductForecast Forecast, int BacktestUnits) BuildProduct(
        Product p,
        IReadOnlyDictionary<DateOnly, int> sold,
        DateOnly today,
        IReadOnlyList<DateOnly> chartDates)
    {
        int Sold(DateOnly d) => sold.TryGetValue(d, out var q) ? q : 0;

        // 7-day weighted moving average of the seven complete days before `asOf`.
        // Oldest day weighs 1, the most recent weighs 7, and the total is divided by 28.
        double Wma(DateOnly asOf)
        {
            double sum = 0;
            for (var i = 0; i < WindowDays; i++)
                sum += Sold(asOf.AddDays(i - WindowDays)) * (i + 1);
            return sum / (WindowDays * (WindowDays + 1) / 2);
        }

        bool CanForecast(DateOnly d) => d.DayNumber - p.TrackedSince.DayNumber >= WindowDays;

        var insufficient = !CanForecast(today);
        double? rate = insufficient ? null : Wma(today);

        var weekly = Enumerable.Range(1, WindowDays).Sum(i => Sold(today.AddDays(-i)));

        double? daysLeft = rate is > 0 ? p.Stock / rate.Value : null;
        string? depletion = daysLeft is <= 365
            ? today.AddDays((int)Math.Floor(daysLeft.Value)).ToString("yyyy-MM-dd")
            : null;

        int? reorderQty = rate is > 0
            ? (int)Math.Ceiling(Math.Round(rate.Value * SafetyBufferDays, 6))
            : null;

        // Backtest: how well did the WMA that was in force each of the last 7 days predict
        // that day's actual sales? Accuracy = 1 - (total absolute error / total actual sales).
        double absError = 0;
        var actualUnits = 0;
        for (var i = 1; i <= WindowDays; i++)
        {
            var day = today.AddDays(-i);
            if (!CanForecast(day)) continue;
            absError += Math.Abs(Sold(day) - Wma(day));
            actualUnits += Sold(day);
        }
        double? confidence = actualUnits > 0 ? Math.Clamp(1 - absError / actualUnits, 0, 1) : null;

        // Alert rules. Critical: at or below the owner's reorder level, or under 20% of weekly
        // consumption. Warning: close to the reorder level, or forecast to run out within a week.
        var atThreshold = p.Stock <= p.ReorderThreshold;
        var underWeeklyShare = weekly > 0 && p.Stock < CriticalShareOfWeeklyUse * weekly;
        var critical = atThreshold || underWeeklyShare;
        var nearThreshold = p.Stock <= p.ReorderThreshold * WarningThresholdFactor;
        var runsOutSoon = daysLeft is <= WarningDaysLeft;

        string status, reason;
        if (critical)
        {
            status = StockStatus.Critical;
            reason = atThreshold
                ? $"At or below the reorder level ({UnitText.Format(p.ReorderThreshold, p.Unit)})"
                : "Under 20% of last week's use";
        }
        else if (nearThreshold || runsOutSoon)
        {
            status = StockStatus.Warning;
            reason = runsOutSoon
                ? "Forecast to run out within a week"
                : $"Close to the reorder level ({UnitText.Format(p.ReorderThreshold, p.Unit)})";
        }
        else
        {
            status = StockStatus.Healthy;
            reason = "Above the reorder level";
        }

        var forecast = new ProductForecast(
            Id: p.Id,
            Name: p.Name,
            Category: p.Category,
            Unit: p.Unit,
            Stock: p.Stock,
            Threshold: p.ReorderThreshold,
            SoldToday: Sold(today),
            WeeklyConsumption: weekly,
            DailyRate: rate.HasValue ? Math.Round(rate.Value, 2) : null,
            DaysLeft: daysLeft.HasValue ? Math.Round(daysLeft.Value, 1) : null,
            DepletionDate: depletion,
            Status: status,
            StatusReason: reason,
            ReorderQty: reorderQty,
            Confidence: confidence.HasValue ? Math.Round(confidence.Value, 3) : null,
            InsufficientData: insufficient,
            History: chartDates.Select(Sold).ToList(),
            Wma: chartDates.Select(d => CanForecast(d) ? (double?)Math.Round(Wma(d), 2) : null).ToList());

        return (forecast, actualUnits);
    }
}
