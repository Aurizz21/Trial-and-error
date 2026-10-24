using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models.Entities;

namespace PoultryOS.Services;

public sealed class AlertService : IAlertService
{
    private readonly AppDbContext _db;
    private readonly StockRiskEvaluator _riskEvaluator;
    private readonly MlForecastService _mlForecastService;

    public AlertService(
        AppDbContext db,
        StockRiskEvaluator riskEvaluator,
        MlForecastService mlForecastService)
    {
        _db = db;
        _riskEvaluator = riskEvaluator;
        _mlForecastService = mlForecastService;
    }

    public async Task RegenerateAlertForProductAsync(Product product, int? userId)
    {
        var weekStart = DateTime.Today.AddDays(-6);
        var sales = await _db.Sales
            .Where(s => s.ProductId == product.Id && s.SoldAt >= weekStart)
            .Select(s => new { s.Quantity, s.SoldAt })
            .ToListAsync();
        var dailySales = Enumerable.Range(0, 7)
            .Select(offset => sales
                .Where(s => s.SoldAt.Date == weekStart.Date.AddDays(offset))
                .Sum(s => s.Quantity))
            .ToList();
        var wmaDailyRate = _riskEvaluator.CalculateDailyWmaRate(dailySales);

        var today = DateTime.Today;
        var aiForecast = await _db.Forecasts
            .AsNoTracking()
            .Where(f =>
                f.ProductId == product.Id
                && f.ModelType == "ML.NET"
                && f.GeneratedAt >= today)
            .OrderByDescending(f => f.GeneratedAt)
            .Select(f => (decimal?)f.ForecastNext7Days)
            .FirstOrDefaultAsync();
        var aiDailyRate = wmaDailyRate;
        if (aiForecast.HasValue)
        {
            aiDailyRate = (double)aiForecast.Value / 7d;
        }
        else
        {
            try
            {
                var forecast = await _mlForecastService.PredictAsync(product.Id, (float)product.CurrentStock);
                if (forecast.ErrorMessage is null && forecast.Next30Days.Length > 0)
                    aiDailyRate = forecast.Next30Days.Take(7).Sum() / 7d;
            }
            catch (Exception)
            {
                aiDailyRate = wmaDailyRate;
            }
        }

        // Stored alerts use AI when available, with WMA as the fallback.
        var risk = _riskEvaluator.Evaluate(
            product.CurrentStock,
            product.ReorderThreshold,
            wmaDailyRate,
            aiDailyRate,
            useAiForAlerts: true);
        var status = risk.Status == "Healthy" ? null : risk.Status;

        var existingAlert = await _db.StockAlerts
            .FirstOrDefaultAsync(a => a.ProductId == product.Id && a.ResolvedAt == null);

        if (status is "Critical" or "Warning")
        {
            var message = status == "Critical"
                ? $"{product.Name} stock is critical: {product.CurrentStock:0.##} {product.Units} remaining."
                : $"{product.Name} stock is approaching the critical level: {product.CurrentStock:0.##} {product.Units} remaining.";
            var suggestedReorderQuantity = Math.Max(0m, product.ReorderThreshold * 2m - product.CurrentStock);
            var hasMatchingNotification = existingAlert is not null
                && await _db.Notifications.AnyAsync(n =>
                    n.StockAlertId == existingAlert.Id
                    && n.Type == status
                    && n.Title == $"{status}: {product.Name}");
            var shouldNotify = existingAlert is null
                || existingAlert.Status != status
                || !hasMatchingNotification;

            if (existingAlert is null)
            {
                existingAlert = new StockAlert
                {
                    ProductId = product.Id,
                    Status = status,
                    Message = message,
                    CurrentStock = product.CurrentStock,
                    DaysRemaining = risk.DaysRemaining,
                    SuggestedReorderQuantity = suggestedReorderQuantity,
                    CreatedAt = DateTime.Now
                };
                _db.StockAlerts.Add(existingAlert);
            }
            else
            {
                existingAlert.CurrentStock = product.CurrentStock;
                existingAlert.Message = message;
                existingAlert.Status = status;
                existingAlert.DaysRemaining = risk.DaysRemaining;
                existingAlert.SuggestedReorderQuantity = suggestedReorderQuantity;
            }

            if (shouldNotify)
            {
                _db.Notifications.Add(new Notification
                {
                    Type = status,
                    Title = $"{status}: {product.Name}",
                    Message = message,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    StockAlert = existingAlert
                });

                _db.AuditLogs.Add(new AuditLog
                {
                    Timestamp = DateTime.Now,
                    UserId = userId,
                    ActionType = "Alert",
                    Description = $"{status} alert created for {product.Name}."
                });
            }
        }
        else if (existingAlert is not null)
        {
            existingAlert.ResolvedAt = DateTime.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = userId,
                ActionType = "Alert",
                Description = $"Alert resolved for {product.Name} (stock recovered)."
            });
        }
    }
}
