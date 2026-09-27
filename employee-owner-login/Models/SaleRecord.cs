namespace PoultryOS.Models;

public sealed class SaleRecord
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string EnteredBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
