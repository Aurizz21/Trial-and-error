namespace PoultryOS.Models;

// One row: forecast for a single product.
public class ForecastRowViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;

    public decimal CurrentStock { get; set; }
    public decimal ReorderThreshold { get; set; }

    // Sales history totals
    public decimal Last7DaysSales { get; set; }
    public decimal WeightedMovingAverage { get; set; }   // 7-day WMA (daily rate)
    public decimal ForecastNext7Days { get; set; }        // WMA * 7

    // Depletion analysis
    public double DaysRemaining { get; set; }
    public string PredictedDepletionDate { get; set; } = string.Empty;
    public string Status { get; set; } = "Healthy";       // Healthy / Warning / Critical
    public decimal SuggestedReorderQuantity { get; set; }

    // Real accuracy vs. yesterday's actual (null when insufficient data)
    public double? ForecastAccuracy { get; set; }
}

// What the page needs in one object.
public class ForecastPageViewModel
{
    public List<ForecastRowViewModel> Rows { get; set; } = new();

    // KPI strip
    public int TotalProducts { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public double AverageAccuracy { get; set; }

    // Labels
    public string GeneratedAt { get; set; } = string.Empty;
    public string Model { get; set; } = "7-Day Weighted Moving Average";

    // For the bar chart: one entry per product
    public List<ForecastChartPoint> ChartData { get; set; } = new();
}

// A single bar-chart pair per product: actual vs forecast.
public class ForecastChartPoint
{
    public string Product { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public decimal Last7DaysActual { get; set; }
    public decimal Next7DaysForecast { get; set; }
}