namespace PoultryOS.Models.Entities;

public class StockAlert
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public double DaysRemaining { get; set; }
    public decimal SuggestedReorderQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ResolvedAt { get; set; }

    public List<Notification> Notifications { get; set; } = new();
}
