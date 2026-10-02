namespace PoultryOS.Models;

// One row in the Audit Trail table.
public class AuditRowViewModel
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActionType { get; set; } = string.Empty;   // Login, Sale, Product, System, Alert
    public string Username { get; set; } = string.Empty;     // "System" if UserId is null
    public string Description { get; set; } = string.Empty;
}

// What the page needs in one object.
public class AuditPageViewModel
{
    public List<AuditRowViewModel> Rows { get; set; } = new();

    // Pagination
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

    // Filter
    public string ActionTypeFilter { get; set; } = "All";
    public string UserFilter { get; set; } = "All";

    // Dropdown options
    public List<string> ActionTypes { get; set; } = new();
    public List<string> Usernames { get; set; } = new();
}