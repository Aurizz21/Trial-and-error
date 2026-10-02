namespace PoultryOS.Models.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
