using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;

namespace PoultryOS.Services;

public class BacktestService
{
    private readonly MLContext _mlContext = new();

    public class SaleData { public float Quantity { get; set; } }
    public class ForecastOutput { public float[] ForecastedQuantities { get; set; } = Array.Empty<float>(); }

    public (double AiAccuracy, double WmaAccuracy) CalculateAccuracies(List<float> historicalSales)
    {
        if (historicalSales.Count < 30) return (0, 0);

        // 1. Split: Train on everything except the last 7 days
        var trainData = historicalSales.Take(historicalSales.Count - 7).ToList();
        var actualLast7 = historicalSales.TakeLast(7).ToList();

        // 2. AI Backtest
        var dataView = _mlContext.Data.LoadFromEnumerable(trainData.Select(q => new SaleData { Quantity = q }));
        var pipeline = _mlContext.Forecasting.ForecastBySsa(
            outputColumnName: nameof(ForecastOutput.ForecastedQuantities),
            inputColumnName: nameof(SaleData.Quantity),
            windowSize: 7, seriesLength: trainData.Count, trainSize: trainData.Count, horizon: 7);

        var model = pipeline.Fit(dataView);
        var engine = model.CreateTimeSeriesEngine<SaleData, ForecastOutput>(_mlContext);
        var aiPreds = engine.Predict().ForecastedQuantities;

        // 3. WMA Backtest (Calculate the 7-day WMA of the training data)
        float wmaPred = 0;
        var last7Train = trainData.TakeLast(7).ToList();
        float weightSum = 0;
        for (int i = 0; i < 7; i++) { wmaPred += (i + 1) * last7Train[i]; weightSum += (i + 1); }
        wmaPred /= weightSum;

        // 4. Calculate Errors (Mean Absolute Percentage Error)
        double aiError = 0, wmaError = 0;
        for (int i = 0; i < 7; i++)
        {
            if (actualLast7[i] > 0)
            {
                aiError += Math.Abs((actualLast7[i] - aiPreds[i]) / actualLast7[i]);
                wmaError += Math.Abs((actualLast7[i] - wmaPred) / actualLast7[i]);
            }
        }

        // Convert to Accuracy %
        double aiAcc = Math.Max(0, 100 - ((aiError / 7) * 100));
        double wmaAcc = Math.Max(0, 100 - ((wmaError / 7) * 100));

        return (Math.Round(aiAcc, 1), Math.Round(wmaAcc, 1));
    }
}