namespace PoultryOS.Models;

public sealed class NotificationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceModule { get; set; } = string.Empty;
    public List<string> TargetRoles { get; set; } = new();
    public string? TargetUser { get; set; }
    public bool IsRead { get; set; }
}
