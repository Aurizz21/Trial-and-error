using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using Product = PoultryOS.Models.Entities.Product;

namespace PoultryOS.Services;

public class DashboardService : IDashboardService
{
    private const int TrendDays = 14;
    private const int WmaWindow = 7;
    private const double WarningDaysRemaining = 3d;

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    // ==============================================================
    //  ENTRY POINT
    // ==============================================================

    public async Task<DashboardSummaryViewModel> GetSummaryAsync(string username, string role)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;
        var trendStart = today.AddDays(-(TrendDays - 1));
        var weekStart = today.AddDays(-(WmaWindow - 1));

        // --- Products ---
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // --- Sales in the trend window ---
        var salesInTrend = await _db.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt >= trendStart && s.SoldAt < today.AddDays(1))
            .Select(s => new SaleRow(s.ProductId, s.Quantity, s.SoldAt))
            .ToListAsync();

        // --- Per-product sales history over the trend window ---
        var historyByProduct = products.ToDictionary(
            p => p.Id,
            p => BuildDailyHistoryForProduct(p.Id, trendStart, TrendDays, salesInTrend));

        // --- Weekly consumption (last 7 days) per product ---
        var weeklyByProduct = new Dictionary<int, decimal>();
        for (var d = 0; d < TrendDays; d++)
        {
            var day = trendStart.AddDays(d);
            if (day < weekStart) continue;

            foreach (var product in products)
            {
                if (!historyByProduct.TryGetValue(product.Id, out var days)) continue;
                var value = days.ElementAtOrDefault(d);
                weeklyByProduct[product.Id] = weeklyByProduct.GetValueOrDefault(product.Id) + value;
            }
        }

        // --- Today's sales ---
        var todaySales = salesInTrend.Where(s => s.SoldAt.Date == today).ToList();
        var wholeChickenIds = products.Where(p => p.Category == "Whole Chicken").Select(p => p.Id).ToHashSet();
        var cutsIds = products.Where(p => p.Category == "Cuts & Parts").Select(p => p.Id).ToHashSet();

        var wholeChickenToday = todaySales.Where(s => wholeChickenIds.Contains(s.ProductId)).Sum(s => s.Quantity);
        var cutsToday = todaySales.Where(s => cutsIds.Contains(s.ProductId)).Sum(s => s.Quantity);

        // --- Same weekday last week for change % ---
        var lastWeekCompareDay = today.AddDays(-7);
        var lastWeekSalesTotal = await _db.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt >= lastWeekCompareDay && s.SoldAt < lastWeekCompareDay.AddDays(1))
            .SumAsync(s => (decimal?)s.Quantity) ?? 0m;

        var todayTotal = todaySales.Sum(s => s.Quantity);
        var changePct = lastWeekSalesTotal == 0
            ? 0m
            : Math.Round((todayTotal - lastWeekSalesTotal) / lastWeekSalesTotal * 100m, 1);

        // --- Active alerts ---
        var activeAlerts = await _db.StockAlerts
            .AsNoTracking()
            .Include(a => a.Product)
            .Where(a => a.ResolvedAt == null)
            .OrderByDescending(a => a.Status == "Critical")
            .ThenBy(a => a.CreatedAt)
            .ToListAsync();

        // --- Recent activity (owner only) ---
        List<AuditEntry> recentActivity = new();
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            recentActivity = await _db.AuditLogs
                .AsNoTracking()
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(8)
                .Select(a => new AuditEntry
                {
                    Timestamp = a.Timestamp,
                    User = a.User != null ? a.User.Username : "System",
                    ActionType = a.ActionType,
                    Description = a.Description
                })
                .ToListAsync();
        }

        // --- Assemble ---
        var summary = new DashboardSummaryViewModel
        {
            Username = username,
            Role = role,
            PageGreeting = GetGreeting(now),
            FullDate = now.ToString("D", CultureInfo.InvariantCulture),
            CurrentDateLabel = now.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
            LastUpdatedLabel = now.ToString("h:mm tt", CultureInfo.InvariantCulture),
            FarmName = "Manok ni Rene",
            TotalProducts = products.Count,
            WholeChickenStock = products.Where(p => p.Category == "Whole Chicken").Sum(p => p.CurrentStock),
            CutsAndPartsStock = products.Where(p => p.Category == "Cuts & Parts").Sum(p => p.CurrentStock),
            WholeChickenTodaySales = wholeChickenToday,
            CutsAndPartsTodaySales = cutsToday,
            TodaySalesChangePercent = changePct,
            ForecastAccuracy = CalculateForecastAccuracy(products, historyByProduct),
            DailySalesTrend = BuildDailySalesTrend(products, historyByProduct, today),
            WeeklyConsumption = BuildWeeklyConsumption(products, weeklyByProduct),
            LowStockDistribution = BuildLowStockDistribution(activeAlerts),
            StockOverview = BuildStockOverview(products, weeklyByProduct, historyByProduct),
            ActiveAlerts = activeAlerts.Select(ToAlertItem).ToList(),
            Products = products.Select(ToViewModelProduct).ToList(),
            CriticalAlertCount = activeAlerts.Count(a => a.Status == "Critical"),
            WarningAlertCount = activeAlerts.Count(a => a.Status == "Warning"),
            RecentActivity = recentActivity,
            OfflineBanner = null
        };

        summary.DepletionBanner = BuildDepletionBanner(summary.ActiveAlerts);
        return summary;
    }

    // ==============================================================
    //  DATA PREP
    // ==============================================================

    private static List<decimal> BuildDailyHistoryForProduct(
        int productId,
        DateTime start,
        int days,
        IEnumerable<SaleRow> sales)
    {
        var result = new List<decimal>(days);
        for (var d = 0; d < days; d++)
        {
            var dayStart = start.AddDays(d);
            var dayEnd = dayStart.AddDays(1);
            var total = sales
                .Where(s => s.ProductId == productId && s.SoldAt >= dayStart && s.SoldAt < dayEnd)
                .Sum(s => s.Quantity);
            result.Add(total);
        }
        return result;
    }

    // ==============================================================
    //  KPIs
    // ==============================================================

        private static double CalculateForecastAccuracy(
        List<Product> products,
        Dictionary<int, List<decimal>> history)
    {
        var scores = new List<double>();

        foreach (var product in products)
        {
            if (!history.TryGetValue(product.Id, out var days) || days.Count < WmaWindow + 1)
                continue;

            var prior7 = days.Skip(days.Count - WmaWindow - 1).Take(WmaWindow).ToList();
            var actual = days[days.Count - 1];

            // Skip if there weren't enough selling days to make a real forecast.
            if (prior7.Count(d => d > 0) < 3) continue;

            var forecast = CalculateWma(prior7);
            if (forecast <= 0) continue;

            var error = actual == 0
                ? 0d
                : Math.Abs((double)actual - forecast) / (double)actual * 100d;
            scores.Add(100d - Math.Min(100d, error));
        }

        if (scores.Count == 0) return 0d;
        return Math.Round(scores.Average(), 1);
    }

    // ==============================================================
    //  CHART BUILDERS
    // ==============================================================

    private static List<DailySalesPoint> BuildDailySalesTrend(
        List<Product> products,
        Dictionary<int, List<decimal>> history,
        DateTime today)
    {
        var points = new List<DailySalesPoint>();
        var start = today.AddDays(-(TrendDays - 1));

        for (var d = 0; d < TrendDays; d++)
        {
            var day = start.AddDays(d);
            foreach (var product in products)
            {
                if (!history.TryGetValue(product.Id, out var days)) continue;

                points.Add(new DailySalesPoint
                {
                    Label = day.ToString("MMM d", CultureInfo.InvariantCulture),
                    Product = product.Name,
                    Category = product.Category,
                    Units = product.Units,
                    Value = days.ElementAtOrDefault(d)
                });
            }
        }

        return points;
    }

    private static List<ProductSalesSummary> BuildWeeklyConsumption(
        List<Product> products,
        Dictionary<int, decimal> weeklyByProduct)
    {
        return products
            .Select(p => new ProductSalesSummary
            {
                Product = p.Name,
                Category = p.Category,
                Units = p.Units,
                Value = weeklyByProduct.GetValueOrDefault(p.Id)
            })
            .ToList();
    }

    private static List<DonutSlice> BuildLowStockDistribution(
        List<PoultryOS.Models.Entities.StockAlert> alerts)
    {
        var slices = alerts
            .Select(a => new DonutSlice
            {
                Label = a.Product?.Name ?? $"Product #{a.ProductId}",
                Value = (int)Math.Ceiling(a.CurrentStock),
                Color = a.Status == "Critical" ? "#ef4444" : "#f59e0b"
            })
            .ToList();

        if (slices.Count == 0)
        {
            slices.Add(new DonutSlice { Label = "All products are healthy", Value = 1, Color = "#10b981" });
        }

        return slices;
    }

    // ==============================================================
    //  STOCK OVERVIEW
    // ==============================================================

    private static List<StockOverviewRow> BuildStockOverview(
        List<Product> products,
        Dictionary<int, decimal> weeklyByProduct,
        Dictionary<int, List<decimal>> history)
    {
        var rows = new List<StockOverviewRow>();

        foreach (var product in products)
        {
            if (!history.TryGetValue(product.Id, out var days))
                days = new List<decimal>();

            var weekly = weeklyByProduct.GetValueOrDefault(product.Id);
            var forecast = CalculateWma(days);
            var daysRemaining = forecast > 0 ? (double)product.CurrentStock / forecast : 0d;
            var status = DetermineStatus(product, weekly, daysRemaining);

            var predictedDate = forecast > 0 && product.CurrentStock > 0
                ? DateTime.Today.AddDays((double)product.CurrentStock / forecast)
                : DateTime.Today;

            rows.Add(new StockOverviewRow
            {
                ProductId = product.Id,
                Product = product.Name,
                Category = product.Category,
                CurrentStock = product.CurrentStock,
                WeeklyConsumption = weekly,
                DaysRemaining = forecast > 0 ? $"{daysRemaining:0.0} days" : "No recent sales",
                PredictedDepletionDate = forecast > 0 && product.CurrentStock > 0
                    ? predictedDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                    : "Insufficient Data",
                Status = status
            });
        }

        return rows;
    }

    // ==============================================================
    //  ALERTS + BANNER
    // ==============================================================

        private static AlertItem ToAlertItem(PoultryOS.Models.Entities.StockAlert alert)
    {
        var perDayForecast = alert.DaysRemaining > 0
            ? (double)alert.CurrentStock / alert.DaysRemaining
            : 0d;

        return new AlertItem
        {
            ProductId = alert.ProductId,
            ProductName = alert.Product?.Name ?? $"Product #{alert.ProductId}",
            Status = alert.Status,
            CurrentStock = alert.CurrentStock,
            DaysRemaining = alert.DaysRemaining,
            Forecast = perDayForecast,
            SuggestedReorderQuantity = alert.SuggestedReorderQuantity,
            Units = alert.Product?.Units ?? string.Empty,
            Message = alert.Message
        };
    }

    private static string BuildDepletionBanner(List<AlertItem> activeAlerts)
    {
        if (activeAlerts.Count == 0)
            return "Inventory is stable. No urgent replenishment required.";

        var mostCritical = activeAlerts
            .OrderBy(a => a.Status == "Critical" ? 0 : 1)
            .ThenBy(a => a.DaysRemaining)
            .First();

        var warning = mostCritical.SuggestedReorderQuantity > 0
            ? $"Reorder {mostCritical.SuggestedReorderQuantity:0.##} {mostCritical.Units} immediately."
            : "No immediate reorder needed based on the current forecast.";

        return $"Stock depletion in ~{Math.Max(1, (int)Math.Round(mostCritical.DaysRemaining))} day(s). {warning}";
    }

    // ==============================================================
    //  MAPPING
    // ==============================================================

    private static PoultryOS.Models.Product ToViewModelProduct(Product entity)
    {
        return new PoultryOS.Models.Product
        {
            Id = entity.Id,
            Name = entity.Name,
            Category = entity.Category,
            Units = entity.Units,
            Supplier = entity.Supplier,
            CurrentStock = entity.CurrentStock,
            ReorderThreshold = entity.ReorderThreshold,
            SalesHistory = new List<decimal>()
        };
    }

    // ==============================================================
    //  MATH + RULES
    // ==============================================================

        private static double CalculateWma(IReadOnlyList<decimal> sales)
    {
        // Skip zero-sales days so a quiet day doesn't drag the forecast down.
        var selling = sales.Where(d => d > 0).TakeLast(WmaWindow).ToList();

        if (selling.Count < 3) return 0;    // too few data points for a meaningful WMA

        var weightedTotal = 0d;
        for (var i = 0; i < selling.Count; i++)
        {
            weightedTotal += (i + 1) * (double)selling[i];
        }

        // Divisor is the sum of weights: for n days, sum = n*(n+1)/2.
        var divisor = (double)selling.Count * (selling.Count + 1) / 2d;
        return weightedTotal / divisor;
    }

    private static string DetermineStatus(Product product, decimal weeklyConsumption, double daysRemaining)
    {
        if (product.CurrentStock < weeklyConsumption * 0.2m || product.CurrentStock <= product.ReorderThreshold)
            return "Critical";

        if (daysRemaining <= WarningDaysRemaining)
            return "Warning";

        return "Healthy";
    }

    private static string GetGreeting(DateTime now)
    {
        var hour = now.Hour;
        return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
    }

    // ==============================================================
    //  INTERNAL ROW TYPE
    // ==============================================================

    private sealed record SaleRow(int ProductId, decimal Quantity, DateTime SoldAt);
}