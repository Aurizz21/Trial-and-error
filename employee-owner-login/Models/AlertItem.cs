namespace PoultryOS.Models;

public sealed class AlertItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public double DaysRemaining { get; set; }
    public double Forecast { get; set; }
    public decimal SuggestedReorderQuantity { get; set; }
    public string Units { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
