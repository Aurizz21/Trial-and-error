namespace PoultryOS.Services;

public class StockRiskEvaluator
{
    public double CalculateDailyWmaRate(IReadOnlyList<decimal> sales)
    {
        var sellingDays = sales.Where(quantity => quantity > 0).TakeLast(7).ToList();
        if (sellingDays.Count < 2) return 0;

        var weightedTotal = 0d;
        for (var i = 0; i < sellingDays.Count; i++)
            weightedTotal += (i + 1) * (double)sellingDays[i];

        var divisor = (double)sellingDays.Count * (sellingDays.Count + 1) / 2d;
        return weightedTotal / divisor;
    }

    public record RiskResult(
        string Status, 
        double DaysRemaining, 
        string ActiveModel, 
        double WmaDaysRemaining, 
        double AiDaysRemaining
    );

    public RiskResult Evaluate(
        decimal currentStock,
        decimal reorderThreshold,
        double wmaDailyRate,
        double aiDailyRate,
        bool useAiForAlerts)
    {
        // Calculate days remaining from each model's daily forecast rate.
        double wmaDays = wmaDailyRate > 0 ? (double)currentStock / wmaDailyRate : 999;
        double aiDays = aiDailyRate > 0 ? (double)currentStock / aiDailyRate : 999;

        // Use AI for alerts when requested, falling back to WMA if AI has no forecast.
        var useAi = useAiForAlerts && aiDailyRate > 0;
        double activeDays = useAi ? aiDays : wmaDays;
        string activeModel = useAi ? "ML.NET" : "WMA";

        string status = "Healthy";

        if (currentStock <= reorderThreshold || activeDays <= 3)
        {
            status = "Critical";
        }
        else if (currentStock <= reorderThreshold * 1.20m || activeDays <= 7)
        {
            status = "Warning";
        }
        
        // Cap the display at 99+ days so it doesn't show 999
        activeDays = Math.Min(activeDays, 99);

        return new RiskResult(status, Math.Round(activeDays, 1), activeModel, Math.Round(wmaDays, 1), Math.Round(aiDays, 1));
    }
}