namespace PoultryOS.Models;

public sealed class DashboardSummaryViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PageGreeting { get; set; } = string.Empty;
    public string FullDate { get; set; } = string.Empty;
    public string FarmName { get; set; } = "Sunrise Poultry Farm";
    public int CriticalAlertCount { get; set; }
    public int WarningAlertCount { get; set; }
    public int TotalProducts { get; set; }
    public decimal WholeChickenStock { get; set; }
    public decimal CutsAndPartsStock { get; set; }
    public decimal WholeChickenTodaySales { get; set; }
    public decimal CutsAndPartsTodaySales { get; set; }
    public decimal TodaySalesChangePercent { get; set; }
    public double ForecastAccuracy { get; set; }
    public List<DailySalesPoint> DailySalesTrend { get; set; } = new();
    public List<ProductSalesSummary> WeeklyConsumption { get; set; } = new();
    public List<DonutSlice> LowStockDistribution { get; set; } = new();
    public List<StockOverviewRow> StockOverview { get; set; } = new();
    public List<AlertItem> ActiveAlerts { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<AuditEntry> RecentActivity { get; set; } = new();
    public string DepletionBanner { get; set; } = string.Empty;
    public bool IsOwner => string.Equals(Role, "Owner", StringComparison.OrdinalIgnoreCase);
    public int ProductTypesCount => Products.Count;
    public string CurrentDateLabel { get; set; } = string.Empty;
    public string LastUpdatedLabel { get; set; } = string.Empty;
    public string? OfflineBanner { get; set; }
}

public sealed class DonutSlice
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class StockOverviewRow
{
    public int ProductId { get; set; }
    public string Product { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal WeeklyConsumption { get; set; }
    public string DaysRemaining { get; set; } = string.Empty;
    public string PredictedDepletionDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class DailySalesPoint
{
    public string Label { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public sealed class ProductSalesSummary
{
    public string Product { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
