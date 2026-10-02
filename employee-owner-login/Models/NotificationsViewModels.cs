namespace PoultryOS.Models;

// One row in the Notifications table.
public class NotificationRowViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = "Info";           // "Critical", "Warning", "Info"
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public int? StockAlertId { get; set; }
}

// What the page needs in one object.
public class NotificationsPageViewModel
{
    public List<NotificationRowViewModel> Notifications { get; set; } = new();

    // KPI counts
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }

    // Selected type filter ("All", "Critical", "Warning", "Info", "Unread")
    public string TypeFilter { get; set; } = "All";
}