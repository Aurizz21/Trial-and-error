namespace PoultryOS.Models;

public sealed class SaleRecord
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime Timestamp { get; set; }
}
