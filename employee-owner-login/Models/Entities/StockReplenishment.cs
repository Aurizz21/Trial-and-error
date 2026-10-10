namespace PoultryOS.Models.Entities;

// One row per restock event. Owner buys from a supplier; stock increases.
public class StockReplenishment
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public decimal Quantity { get; set; }
    public DateTime ReplenishedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
}