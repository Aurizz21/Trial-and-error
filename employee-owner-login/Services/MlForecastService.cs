using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using PoultryOS.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PoultryOS.Services;

public class MlForecastService
{
    private readonly AppDbContext _db;
    private readonly MLContext _mlContext;

    public MlForecastService(AppDbContext db)
    {
        _db = db;
        _mlContext = new MLContext();
    }

    public class SaleData
    {
        public float Quantity { get; set; }
    }

    public class ForecastOutput
    {
        public float[] ForecastedQuantities { get; set; } = Array.Empty<float>();
    }

    public async Task<(float[] Next30Days, DateTime? DepletionDate, string? ErrorMessage)> PredictAsync(int productId, float currentStock)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var cachedForecast = await _db.Forecasts
            .AsNoTracking()
            .Where(f => f.ProductId == productId && f.ModelType == "ML.NET" &&
                        f.GeneratedAt >= today && f.GeneratedAt < tomorrow)
            .OrderByDescending(f => f.GeneratedAt)
            .FirstOrDefaultAsync();

        if (cachedForecast?.AiForecast30DaysJson is not null)
        {
            var cachedCurve = JsonSerializer.Deserialize<float[]>(cachedForecast.AiForecast30DaysJson);
            if (cachedCurve is { Length: 30 })
            {
                var nonNegativeCachedCurve = cachedCurve
                    .Select(value => Math.Max(0, value))
                    .ToArray();
                var cachedDailyAverage = nonNegativeCachedCurve.Average();
                return (
                    nonNegativeCachedCurve,
                    CalculateDepletionDate(currentStock, cachedDailyAverage, today),
                    null);
            }
        }

        const int windowSize = 30;
        const int minimumTrainingSize = windowSize * 2 + 1;
        var startOfRange = today.AddDays(-729);
        var firstSale = await _db.Sales
            .Where(s => s.ProductId == productId && s.SoldAt < tomorrow)
            .Select(s => (DateTime?)s.SoldAt)
            .MinAsync();
        if (firstSale is null)
            return (
                Array.Empty<float>(),
                null,
                $"At least {minimumTrainingSize} daily data points are required for a {windowSize}-day window; found 0.");

        var startDay = firstSale.Value.Date > startOfRange ? firstSale.Value.Date : startOfRange;
        var sales = await _db.Sales
            .Where(s => s.ProductId == productId && s.SoldAt >= startDay && s.SoldAt < tomorrow)
            .Select(s => new { s.SoldAt, s.Quantity })
            .ToListAsync();
        var salesByDay = sales
            .GroupBy(s => s.SoldAt.Date)
            .ToDictionary(group => group.Key, group => (float)group.Sum(s => s.Quantity));
        var dailySales = Enumerable.Range(0, (today - startDay).Days + 1)
            .Select(day => salesByDay.GetValueOrDefault(startDay.AddDays(day)))
            .ToList();

        if (dailySales.Count < minimumTrainingSize)
            return (
                Array.Empty<float>(),
                null,
                $"At least {minimumTrainingSize} daily data points are required for a {windowSize}-day window; found {dailySales.Count}.");

        // 2. Load data into ML.NET format
        var data = dailySales.Select(q => new SaleData { Quantity = q }).ToList();
        var dataView = _mlContext.Data.LoadFromEnumerable(data);

        var trainSize = dailySales.Count;

        // 3. Train the AI (SSA - Singular Spectrum Analysis)
        var pipeline = _mlContext.Forecasting.ForecastBySsa(
            outputColumnName: nameof(ForecastOutput.ForecastedQuantities),
            inputColumnName: nameof(SaleData.Quantity),
            windowSize: windowSize,
            seriesLength: trainSize,
            trainSize: trainSize,
            horizon: 30,
            discountFactor: 0.9f);

        ForecastOutput forecast;
        try
        {
            var model = pipeline.Fit(dataView);

            // 4. Make the prediction
            var forecastEngine = model.CreateTimeSeriesEngine<SaleData, ForecastOutput>(_mlContext);
            forecast = forecastEngine.Predict();
        }
        catch (InvalidOperationException ex)
        {
            return (
                Array.Empty<float>(),
                null,
                $"SSA forecasting could not run with {dailySales.Count} daily data points " +
                $"(train size {trainSize}, window size {windowSize}). " +
                $"More data may be required. Details: {ex.Message}");
        }

        // 5. Calculate totals and depletion date
        var forecastedQuantities = forecast.ForecastedQuantities
            .Select(value => Math.Max(0, value))
            .ToArray();
        float dailyAverage = forecastedQuantities.Average();

        var depletionDate = CalculateDepletionDate(currentStock, dailyAverage, today);

        _db.Forecasts.Add(new PoultryOS.Models.Entities.Forecast
        {
            ProductId = productId,
            ModelType = "ML.NET",
            GeneratedAt = DateTime.Now,
            ForecastNext7Days = (decimal)forecastedQuantities.Take(7).Sum(),
            AiForecast30DaysJson = JsonSerializer.Serialize(forecastedQuantities),
            PredictedDepletionDate = depletionDate,
            Status = "Healthy"
        });
        await _db.SaveChangesAsync();

        return (forecastedQuantities, depletionDate, null);
    }

    private static DateTime? CalculateDepletionDate(float currentStock, float dailyAverage, DateTime today)
    {
        if (dailyAverage <= 0)
            return null;

        int daysUntilEmpty = (int)Math.Ceiling(currentStock / dailyAverage);
        return today.AddDays(daysUntilEmpty);
    }
}