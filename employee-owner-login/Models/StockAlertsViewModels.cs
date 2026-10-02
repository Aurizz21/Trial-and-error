namespace PoultryOS.Models;

// One row in the Stock Alerts table.
public class StockAlertRowViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;              // "Warning" or "Critical"
    public decimal CurrentStock { get; set; }
    public double DaysRemaining { get; set; }
    public decimal SuggestedReorderQuantity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// What the page needs in one object.
public class StockAlertsPageViewModel
{
    public List<StockAlertRowViewModel> Alerts { get; set; } = new();

    // Counts for the KPI strip at the top.
    public int TotalActive { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }

    // The currently selected status filter ("All", "Critical", "Warning").
    public string StatusFilter { get; set; } = "All";
}