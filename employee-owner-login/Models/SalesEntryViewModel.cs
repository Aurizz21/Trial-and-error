namespace PoultryOS.Models;

public sealed class SalesEntryViewModel
{
    public List<Product> Products { get; set; } = new();
    public SalesSummaryViewModel TodaysSummary { get; set; } = new();
    public List<SaleRecord> TodaysEntries { get; set; } = new();
    public List<AlertItem> LowStockProducts { get; set; } = new();
}

public sealed class SalesSummaryViewModel
{
    public decimal TotalPcsSold { get; set; }
    public decimal TotalKgSold { get; set; }
    public int EntriesCount { get; set; }
    public int ProductsAffectedCount { get; set; }
    public int TotalProducts { get; set; }
    public string TotalUnitsSold => $"{TotalPcsSold:0.##} pcs, {TotalKgSold:0.##} kg";
}

public sealed class RecordSaleResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public Product? UpdatedProduct { get; init; }
    public SaleRecord? NewEntry { get; init; }
}