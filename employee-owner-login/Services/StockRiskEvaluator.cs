namespace PoultryOS.Services;

public class StockRiskEvaluator
{
    public record RiskResult(
        string Status, 
        double DaysRemaining, 
        string ActiveModel, 
        double WmaDaysRemaining, 
        double AiDaysRemaining
    );

    public RiskResult Evaluate(decimal currentStock, decimal reorderThreshold, double wmaDailyRate, double aiDailyRate, bool useAiForAlerts)
    {
        // 1. Calculate Days Remaining for both models
        double wmaDays = wmaDailyRate > 0 ? (double)currentStock / wmaDailyRate : 999;
        double aiDays = aiDailyRate > 0 ? (double)currentStock / aiDailyRate : 999;

        // 2. Claude's rule: "Worst-case model selection"
        // If the user wants AI, use AI. If AI fails (0), fallback to WMA.
        double activeDays = useAiForAlerts && aiDays < 999 ? aiDays : wmaDays;
        string activeModel = (useAiForAlerts && aiDays < 999) ? "ML.NET" : "WMA";

        // 3. Claude's rule: "Matching status thresholds to legacy rules"
        string status = "Healthy";
        if (currentStock <= reorderThreshold || (activeDays <= 3 && activeDays > 0))
        {
            status = currentStock <= reorderThreshold ? "Critical" : "Warning";
        }
        
        // Cap the display at 99+ days so it doesn't show 999
        activeDays = Math.Min(activeDays, 99);

        return new RiskResult(status, Math.Round(activeDays, 1), activeModel, Math.Round(wmaDays, 1), Math.Round(aiDays, 1));
    }
}