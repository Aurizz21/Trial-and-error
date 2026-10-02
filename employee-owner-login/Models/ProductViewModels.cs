using System.ComponentModel.DataAnnotations;

namespace PoultryOS.Models;

// One row in the Products table. This is what the page sees -- NOT the database entity.
public class ProductRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderThreshold { get; set; }
    public decimal WeeklyConsumption { get; set; }   // total sold in the last 7 days
    public double? DaysRemaining { get; set; }       // null when there are no recent sales
    public string Status { get; set; } = "Healthy";  // Healthy / Warning / Critical
}

// Everything the Products page needs in one object.
public class ProductsPageViewModel
{
    public List<ProductRowViewModel> Products { get; set; } = new();
    public int TotalProducts { get; set; }
    public int ActiveAlerts { get; set; }
    public decimal WeeklySales { get; set; }
}

// What the browser sends when adding or editing a product.
public class ProductFormRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unit is required.")]
    public string Units { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Supplier must be 100 characters or fewer.")]
    public string? Supplier { get; set; }

    [Range(0.0, 1000000.0, ErrorMessage = "Stock must be 0 or more.")]
    public decimal CurrentStock { get; set; }

    [Range(0.0, 1000000.0, ErrorMessage = "Threshold must be 0 or more.")]
    public decimal ReorderThreshold { get; set; }
}

// What the service hands back to the controller: did it work, and what should we tell the user?
public record ServiceResult(bool Success, string Message)
{
    public static ServiceResult Ok(string message) => new(true, message);
    public static ServiceResult Fail(string message) => new(false, message);
}