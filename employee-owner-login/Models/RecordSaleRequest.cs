using System.ComponentModel.DataAnnotations;

namespace PoultryOS.Models;

public sealed class RecordSaleRequest
{
    [Required]
    public int? ProductId { get; set; }

    [Required]
    [Range(typeof(decimal), "0.0001", "999999999", ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public string? Notes { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }
}