namespace PoultryOS.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int ReorderThreshold { get; set; }
    public List<int> SalesHistory { get; set; } = new();
}
