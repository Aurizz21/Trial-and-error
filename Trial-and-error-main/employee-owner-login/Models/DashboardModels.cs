namespace PoultryOS.Models;

// ---- Everything below is serialised to JSON (camelCase) for the dashboard page ---------------

public sealed record DashboardSnapshot(
    DateTimeOffset GeneratedAt,
    string Today,                         // yyyy-MM-dd in the business's time zone
    IReadOnlyList<string> Dates,          // the 14 chart days, oldest first, last one is today
    DashboardKpis Kpis,
    IReadOnlyList<ProductForecast> Products,   // most urgent first
    IReadOnlyList<CategoryUsage> Categories,
    IReadOnlyList<ActivityItem>? Activity);    // owner only (audit trail preview)

public sealed record DashboardKpis(
    int TotalStock,
    int ProductCount,
    int SoldToday,
    int EntriesToday,
    int Critical,
    int Warning,
    int Healthy,
    double? Accuracy,        // null until at least one product has enough history to score
    double AccuracyTarget);

public sealed record ProductForecast(
    int Id,
    string Name,
    string Category,
    string Unit,
    int Stock,
    int Threshold,
    int SoldToday,
    int WeeklyConsumption,             // actual units sold in the last 7 complete days
    double? DailyRate,                 // 7-day weighted moving average, units per day
    double? DaysLeft,                  // Stock / DailyRate
    string? DepletionDate,             // yyyy-MM-dd, null when there is no rate or it is > 1 year away
    string Status,                     // "critical" | "warning" | "healthy"
    string StatusReason,
    int? ReorderQty,                   // covers ForecastService.SafetyBufferDays of forecast demand
    double? Confidence,                // 0..1, 1 - WAPE of the last 7 one-day-ahead forecasts
    bool InsufficientData,             // fewer than 7 days of history
    IReadOnlyList<int> History,        // units sold on each of the 14 chart days
    IReadOnlyList<double?> Wma);       // the WMA forecast that was in force on each chart day

public sealed record CategoryUsage(string Category, int Units);

public sealed record ActivityItem(DateTimeOffset Time, string User, string Action, string Description);

// ---- Requests / responses for the quick-entry endpoints -----------------------------------

public sealed record StockChangeRequest(int ProductId, int Quantity);

public sealed record StockChangeResult(bool Ok, string Message, DashboardSnapshot? Snapshot);
