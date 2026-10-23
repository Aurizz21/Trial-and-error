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
                return (cachedCurve, cachedForecast.PredictedDepletionDate, null);
        }

        // 1. Get the last 730 days of sales for this product
        var sales = await _db.Sales
            .Where(s => s.ProductId == productId && s.SoldAt >= today.AddDays(-729))
            .OrderBy(s => s.SoldAt)
            .Select(s => s.Quantity)
            .ToListAsync();

        const int windowSize = 30;
        const int minimumTrainingSize = windowSize * 2 + 1;
        if (sales.Count < minimumTrainingSize)
            return (
                Array.Empty<float>(),
                null,
                $"At least {minimumTrainingSize} sales data points are required for a {windowSize}-day window; found {sales.Count}.");

        // 2. Load data into ML.NET format
        var data = sales.Select(q => new SaleData { Quantity = (float)q }).ToList();
        var dataView = _mlContext.Data.LoadFromEnumerable(data);

        var trainSize = sales.Count;

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
                $"SSA forecasting could not run with {sales.Count} sales data points " +
                $"(train size {trainSize}, window size {windowSize}). " +
                $"More data may be required. Details: {ex.Message}");
        }

        // 5. Calculate totals and depletion date
        float dailyAverage = forecast.ForecastedQuantities.Average();

        DateTime? depletionDate = null;
        if (dailyAverage > 0)
        {
            int daysUntilEmpty = (int)Math.Ceiling(currentStock / dailyAverage);
            depletionDate = today.AddDays(daysUntilEmpty);
        }

        _db.Forecasts.Add(new PoultryOS.Models.Entities.Forecast
        {
            ProductId = productId,
            ModelType = "ML.NET",
            GeneratedAt = DateTime.Now,
            ForecastNext7Days = (decimal)forecast.ForecastedQuantities.Take(7).Sum(),
            AiForecast30DaysJson = JsonSerializer.Serialize(forecast.ForecastedQuantities),
            PredictedDepletionDate = depletionDate,
            Status = "Healthy"
        });
        await _db.SaveChangesAsync();

        return (forecast.ForecastedQuantities, depletionDate, null);
    }
}