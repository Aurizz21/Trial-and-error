using System.ComponentModel.DataAnnotations;

namespace PoultryOS.Models;

// What the browser sends when restocking.
public class RestockRequest
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [StringLength(500, ErrorMessage = "Notes must be 500 characters or fewer.")]
    public string? Notes { get; set; }
}

// Result returned to the caller.
public class RestockResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public decimal NewStock { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public bool AlertResolved { get; init; }
}