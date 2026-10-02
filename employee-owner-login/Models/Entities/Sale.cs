namespace PoultryOS.Models.Entities;

public class Sale
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public decimal Quantity { get; set; }
    public DateTime SoldAt { get; set; }
    public string? Notes { get; set; }
}
