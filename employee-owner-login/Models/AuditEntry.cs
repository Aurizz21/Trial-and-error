namespace PoultryOS.Models;

public sealed class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
