namespace PoultryOS.Models;

public sealed class AlertItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public double DaysRemaining { get; set; }
    public int SuggestedReorderQuantity { get; set; }
    public string Message { get; set; } = string.Empty;
}
