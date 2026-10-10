namespace PoultryOS.Models.Entities;

// A daily snapshot of the forecast for one product.
// One row per product per day -- used for historical accuracy tracking.
public class Forecast
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    public decimal WeightedMovingAverage { get; set; }
    public decimal ForecastNext7Days { get; set; }
    public DateTime? PredictedDepletionDate { get; set; }
    public double? ConfidenceScore { get; set; }
    public string Status { get; set; } = "Healthy";
}