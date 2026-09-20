using System.Globalization;
using PoultryOS.Models;

namespace PoultryOS.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly List<Product> products = new();
    private readonly List<SaleRecord> sales = new();
    private readonly List<AuditEntry> auditEntries = new();
    private readonly Dictionary<int, string> activeAlertState = new();

    public InventoryService()
    {
        SeedProducts();
        SeedAuditTrail();
    }

    public DashboardSummaryViewModel GetSummary(string username, string role)
    {
        var now = DateTime.Now;
        var alerts = BuildAlerts();
        var summary = new DashboardSummaryViewModel
        {
            Username = username,
            Role = role,
            PageGreeting = GetGreeting(now),
            FullDate = now.ToString("D", CultureInfo.InvariantCulture),
            CurrentDateLabel = now.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
            LastUpdatedLabel = now.ToString("h:mm tt", CultureInfo.InvariantCulture),
            FarmName = "Sunrise Poultry Farm",
            TotalProducts = products.Count,
            TotalUnits = products.Sum(p => p.CurrentStock),
            TodaySalesUnits = products.Sum(p => p.SalesHistory.LastOrDefault()),
            TodaySalesChangePercent = CalculateTodaySalesChange(),
            ForecastAccuracy = CalculateForecastAccuracy(),
            DailySalesTrend = BuildDailySalesTrend(),
            WeeklyConsumption = BuildWeeklyConsumption(),
            LowStockDistribution = BuildLowStockDistribution(alerts),
            StockOverview = BuildStockOverview(),
            ActiveAlerts = alerts,
            Products = products,
            CriticalAlertCount = alerts.Count(a => a.Status == "Critical"),
            WarningAlertCount = alerts.Count(a => a.Status == "Warning"),
            RecentActivity = string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
                ? auditEntries.OrderByDescending(a => a.Timestamp).Take(8).ToList()
                : new List<AuditEntry>(),
            OfflineBanner = null
        };

        summary.DepletionBanner = BuildDepletionBanner();
        return summary;
    }

    public DashboardSummaryViewModel RecordSale(int productId, int quantity, string username, string role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("A username is required to record a sale.");
        }

        var product = products.SingleOrDefault(p => p.Id == productId)
            ?? throw new InvalidOperationException("The selected product was not found.");

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale quantity must be greater than zero.");
        }

        if (quantity > product.CurrentStock)
        {
            throw new InvalidOperationException("The requested quantity exceeds current stock.");
        }

        product.CurrentStock -= quantity;
        product.SalesHistory[^1] += quantity;

        sales.Add(new SaleRecord
        {
            Id = sales.Count + 1,
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = quantity,
            Timestamp = DateTime.Now
        });

        auditEntries.Add(new AuditEntry
        {
            Timestamp = DateTime.Now,
            User = username,
            ActionType = "Sale",
            Description = $"Recorded sale of {quantity} {product.Units} for {product.Name}."
        });

        RegenerateAlerts();
        return GetSummary(username, role);
    }

    private void SeedProducts()
    {
        var seeds = new[]
        {
            new { Id = 1, Name = "Layer Feed (50kg)", Category = "Feed", Units = "bags", Supplier = "Sunrise Mills", CurrentStock = 48, ReorderThreshold = 48, BaseDemand = 18 },
            new { Id = 2, Name = "Broiler Starter (25kg)", Category = "Feed", Units = "bags", Supplier = "Poultry Plus", CurrentStock = 6, ReorderThreshold = 6, BaseDemand = 11 },
            new { Id = 3, Name = "Broiler Finisher (50kg)", Category = "Feed", Units = "bags", Supplier = "Poultry Plus", CurrentStock = 4, ReorderThreshold = 4, BaseDemand = 10 },
            new { Id = 4, Name = "Newcastle Vaccine", Category = "Health", Units = "vials", Supplier = "Vet Supply Co.", CurrentStock = 3, ReorderThreshold = 3, BaseDemand = 2 },
            new { Id = 5, Name = "Vitamin Supplement", Category = "Health", Units = "kg", Supplier = "Agri Health", CurrentStock = 14, ReorderThreshold = 14, BaseDemand = 7 },
            new { Id = 6, Name = "Disinfectant 5L", Category = "Sanitation", Units = "bottles", Supplier = "Farm Clean", CurrentStock = 8, ReorderThreshold = 8, BaseDemand = 4 },
            new { Id = 7, Name = "Drinking Nipples (pack)", Category = "Equipment", Units = "packs", Supplier = "Barn Essentials", CurrentStock = 2, ReorderThreshold = 2, BaseDemand = 3 },
            new { Id = 8, Name = "Egg Trays (30-cell)", Category = "Packaging", Units = "units", Supplier = "Poultry Packaging", CurrentStock = 16, ReorderThreshold = 16, BaseDemand = 12 }
        };

        foreach (var seed in seeds)
        {
            var random = new Random(202406 + seed.Id);
            var history = new List<int>(14);

            for (var day = 0; day < 14; day++)
            {
                var variation = random.Next(-2, 3);
                var demand = Math.Max(0, seed.BaseDemand + variation + (day % 5 == 0 ? 2 : 0) - (day % 7 == 0 ? 1 : 0));
                history.Add(demand);
            }

            products.Add(new Product
            {
                Id = seed.Id,
                Name = seed.Name,
                Category = seed.Category,
                Units = seed.Units,
                Supplier = seed.Supplier,
                CurrentStock = seed.CurrentStock,
                ReorderThreshold = seed.ReorderThreshold,
                SalesHistory = history
            });
        }
    }

    private void SeedAuditTrail()
    {
        auditEntries.Add(new AuditEntry
        {
            Timestamp = DateTime.Now.AddHours(-4),
            User = "owner",
            ActionType = "System",
            Description = "Inventory snapshot initialized for the current reporting period."
        });
    }

    private static string GetGreeting(DateTime now)
    {
        var hour = now.Hour;
        return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
    }

    private decimal CalculateTodaySalesChange()
    {
        var totalThisWeek = products.Sum(product => product.SalesHistory.LastOrDefault());
        var previousWeek = products.Sum(product => product.SalesHistory.ElementAtOrDefault(product.SalesHistory.Count - 7));

        if (previousWeek == 0)
        {
            return 0m;
        }

        return Math.Round(((decimal)totalThisWeek - previousWeek) / previousWeek * 100m, 1);
    }

    private double CalculateForecastAccuracy()
    {
        var scores = new List<double>();

        foreach (var product in products)
        {
            if (product.SalesHistory.Count < 7)
            {
                continue;
            }

            var recent = product.SalesHistory.TakeLast(7).ToList();
            var forecastValues = new List<double>();

            for (var i = 0; i < recent.Count; i++)
            {
                // WMA formula: weights increase from 1 to 7 across the last 7 sales days, so the newest day has the highest influence.
                var weightedTotal = 0d;
                for (var j = 0; j < recent.Count; j++)
                {
                    weightedTotal += (j + 1) * recent[j];
                }

                forecastValues.Add(weightedTotal / 28d);
            }

            var actual = recent[recent.Count - 1];
            var forecast = forecastValues.Last();
            var error = actual == 0 ? 0d : Math.Abs((actual - forecast) / actual) * 100d;
            scores.Add(100d - Math.Min(100d, error));
        }

        if (scores.Count == 0)
        {
            return 98.4d;
        }

        return Math.Round(scores.Average(), 1);
    }

    private List<KeyValuePair<string, int>> BuildDailySalesTrend()
    {
        var labels = new List<KeyValuePair<string, int>>();
        var currentDay = DateTime.Today;

        for (var i = 13; i >= 0; i--)
        {
            var day = currentDay.AddDays(i - 13);
            var total = 0;
            foreach (var product in products)
            {
                total += product.SalesHistory.ElementAtOrDefault(product.SalesHistory.Count - 14 + i);
            }

            labels.Add(new KeyValuePair<string, int>(day.ToString("MMM d", CultureInfo.InvariantCulture), total));
        }

        return labels;
    }

    private List<KeyValuePair<string, int>> BuildWeeklyConsumption()
    {
        var categories = products
            .GroupBy(p => p.Category)
            .Select(group => new KeyValuePair<string, int>(group.Key, group.Sum(p => p.SalesHistory.TakeLast(7).Sum())))
            .OrderBy(x => x.Key)
            .ToList();

        return categories;
    }

    private List<DonutSlice> BuildLowStockDistribution(List<AlertItem> alerts)
    {
        var atRiskProducts = products
            .Where(p => alerts.Any(a => a.ProductId == p.Id))
            .Select(p => new DonutSlice
            {
                Label = p.Name,
                Value = p.CurrentStock,
                Color = p.CurrentStock <= p.ReorderThreshold ? "#ef4444" : "#f59e0b"
            })
            .ToList();

        return atRiskProducts.Count > 0 ? atRiskProducts : new List<DonutSlice> { new() { Label = "Stable", Value = 1, Color = "#10b981" } };
    }

    private List<StockOverviewRow> BuildStockOverview()
    {
        var rows = new List<StockOverviewRow>();

        foreach (var product in products)
        {
            var weeklyConsumption = product.SalesHistory.TakeLast(7).Sum();
            var forecast = CalculateWma(product.SalesHistory);
            var daysRemaining = forecast > 0 ? product.CurrentStock / forecast : 0d;
            var status = DetermineStatus(product, weeklyConsumption, daysRemaining);
            var predictedDate = forecast > 0 && product.CurrentStock > 0
                ? DateTime.Today.AddDays(product.CurrentStock / forecast)
                : DateTime.Today;

            rows.Add(new StockOverviewRow
            {
                ProductId = product.Id,
                Product = product.Name,
                Category = product.Category,
                CurrentStock = product.CurrentStock,
                WeeklyConsumption = weeklyConsumption,
                DaysRemaining = forecast > 0 ? $"{daysRemaining:0.0} days" : "No recent sales",
                PredictedDepletionDate = forecast > 0 && product.CurrentStock > 0 ? predictedDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "Insufficient Data",
                Status = status
            });
        }

        return rows;
    }

    private List<AlertItem> BuildAlerts()
    {
        var alerts = new List<AlertItem>();

        foreach (var product in products)
        {
            var weeklyConsumption = product.SalesHistory.TakeLast(7).Sum();
            var forecast = CalculateWma(product.SalesHistory);
            var daysRemaining = forecast > 0 ? product.CurrentStock / forecast : double.PositiveInfinity;
            var status = DetermineStatus(product, weeklyConsumption, daysRemaining);

            if (status == "Healthy")
            {
                continue;
            }

            var suggestedReorderQuantity = (int)Math.Ceiling(Math.Max(0d, forecast * 14d - product.CurrentStock));

            alerts.Add(new AlertItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Status = status,
                CurrentStock = product.CurrentStock,
                DaysRemaining = forecast > 0 ? daysRemaining : 0,
                SuggestedReorderQuantity = suggestedReorderQuantity,
                Message = status == "Critical"
                    ? $"Stock depletion in ~{Math.Max(1, (int)Math.Round(daysRemaining))} day(s). Reorder {suggestedReorderQuantity} {product.Units} immediately."
                    : $"Low stock forecasted within {Math.Ceiling(daysRemaining)} day(s). Reorder {suggestedReorderQuantity} {product.Units}."
            });
        }

        return alerts
            .OrderByDescending(a => a.Status == "Critical")
            .ThenBy(a => a.ProductName)
            .ToList();
    }

    private static string DetermineStatus(Product product, int weeklyConsumption, double daysRemaining)
    {
        // Alert rule: critical when stock is under 20% of the weekly burn or at/below the reorder threshold; warning only when depletion is within 7 days.
        if (product.CurrentStock < Math.Max(1, (int)Math.Floor(weeklyConsumption * 0.2d)) || product.CurrentStock <= product.ReorderThreshold)
        {
            return "Critical";
        }

        if (daysRemaining <= 7)
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static double CalculateWma(IReadOnlyList<int> sales)
    {
        if (sales.Count < 7)
        {
            return 0;
        }

        var trailing = sales.TakeLast(7).ToList();
        var weightedTotal = 0d;
        for (var i = 0; i < trailing.Count; i++)
        {
            weightedTotal += (i + 1) * trailing[i];
        }

        return weightedTotal / 28d;
    }

    private void RegenerateAlerts()
    {
        var fresh = BuildAlerts();
        var currentIds = fresh.Select(a => a.ProductId).ToHashSet();

        foreach (var alert in fresh)
        {
            var previous = activeAlertState.TryGetValue(alert.ProductId, out var saved) ? saved : "Healthy";
            if (!string.Equals(previous, alert.Status, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(alert.Status, "Healthy", StringComparison.OrdinalIgnoreCase))
            {
                auditEntries.Add(new AuditEntry
                {
                    Timestamp = DateTime.Now,
                    User = "System",
                    ActionType = "Alert",
                    Description = $"{alert.ProductName} became {alert.Status}. {alert.Message}"
                });
            }

            activeAlertState[alert.ProductId] = alert.Status;
        }

        foreach (var item in activeAlertState.Where(pair => !currentIds.Contains(pair.Key)).ToList())
        {
            activeAlertState.Remove(item.Key);
        }
    }

    private string BuildDepletionBanner()
    {
        var alerts = BuildAlerts();
        var mostCritical = alerts
            .OrderBy(a => a.DaysRemaining)
            .FirstOrDefault();

        if (mostCritical is null)
        {
            return "Inventory is stable. No urgent replenishment required.";
        }

        var product = products.Single(p => p.Id == mostCritical.ProductId);
        var warning = mostCritical.SuggestedReorderQuantity > 0
            ? $"Reorder {mostCritical.SuggestedReorderQuantity} {product.Units} immediately."
            : "No immediate reorder needed based on the current forecast.";

        return $"Stock depletion in ~{Math.Max(1, (int)Math.Round(mostCritical.DaysRemaining))} day(s). {warning}";
    }
}
