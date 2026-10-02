namespace PoultryOS.Models.Entities;

public class Notification
{
    public int Id { get; set; }
    public string Type { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }

    public int? StockAlertId { get; set; }
    public StockAlert? StockAlert { get; set; }
}
