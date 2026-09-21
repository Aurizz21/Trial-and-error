namespace PoultryOS.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderThreshold { get; set; }
    public List<decimal> SalesHistory { get; set; } = new();
}
