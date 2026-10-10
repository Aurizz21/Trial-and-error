namespace PoultryOS.Models.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderThreshold { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Sale> Sales { get; set; } = new();
    public List<StockAlert> StockAlerts { get; set; } = new();
    public List<StockReplenishment> Replenishments { get; set; } = new();
    public List<Forecast> Forecasts { get; set; } = new();
}
