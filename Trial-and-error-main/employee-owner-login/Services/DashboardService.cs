using PoultryOS.Models;

namespace PoultryOS.Services;

/// <summary>
/// Glue between the store and the forecast calculation. Also writes an "Alert" line to the audit
/// trail whenever a sale pushes a product into a worse status, so alerts are logged with a time.
/// </summary>
public sealed class DashboardService(IInventoryStore store, ForecastService forecast)
{
    public DashboardSnapshot GetSnapshot(bool includeActivity) =>
        forecast.Build(store.Read(), AppClock.Today, includeActivity);

    public StockChangeResult RecordSale(StockChangeRequest? request, string user, bool includeActivity) =>
        Apply(request, user, includeActivity, isSale: true);

    public StockChangeResult RecordRestock(StockChangeRequest? request, string user, bool includeActivity) =>
        Apply(request, user, includeActivity, isSale: false);

    private StockChangeResult Apply(StockChangeRequest? request, string user, bool includeActivity, bool isSale)
    {
        if (request is null)
            return new StockChangeResult(false, "Choose a product and a quantity.", null);

        var now = AppClock.Now;
        var today = DateOnly.FromDateTime(now.DateTime);

        var before = forecast.Build(store.Read(), today, includeActivity: false)
            .Products.ToDictionary(p => p.Id, p => p.Status);

        var result = isSale
            ? store.RecordSale(request.ProductId, request.Quantity, user, now)
            : store.RecordRestock(request.ProductId, request.Quantity, user, now);

        if (!result.Ok)
            return new StockChangeResult(false, result.Message, null);

        if (isSale)
            LogNewAlerts(before, today, now);

        return new StockChangeResult(true, result.Message, forecast.Build(store.Read(), today, includeActivity));
    }

    private void LogNewAlerts(IReadOnlyDictionary<int, string> before, DateOnly today, DateTimeOffset now)
    {
        var after = forecast.Build(store.Read(), today, includeActivity: false).Products;

        foreach (var p in after)
        {
            if (p.Status == StockStatus.Healthy) continue;
            if (!before.TryGetValue(p.Id, out var previous)) continue;
            if (StockStatus.Severity(p.Status) >= StockStatus.Severity(previous)) continue;

            var reorder = p.ReorderQty is { } qty ? $" Suggested reorder: {UnitText.Format(qty, p.Unit)}." : "";
            store.AddAudit(now, "system", "Alert", $"{p.Name} is now {p.Status}. {p.StatusReason}.{reorder}");
        }
    }
}
