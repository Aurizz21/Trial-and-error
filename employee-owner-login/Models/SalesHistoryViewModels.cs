namespace PoultryOS.Models;

// One row in the Sales History table.
public class SalesHistoryRowViewModel
{
    public int Id { get; set; }
    public DateTime SoldAt { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string EnteredBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

// What the History page needs for the shell (filters, dropdown options, etc.)
public class SalesHistoryPageViewModel
{
    public List<SalesHistoryRowViewModel> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

    // For the filter dropdowns
    public List<ProductOption> Products { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
}

public class ProductOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UserOption
{
    public string Username { get; set; } = string.Empty;
}

// Filters sent from the browser when applying the toolbar.
public class SalesHistoryFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? ProductId { get; set; }
    public string? EnteredBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}