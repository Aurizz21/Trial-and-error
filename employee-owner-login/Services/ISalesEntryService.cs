using PoultryOS.Models;

namespace PoultryOS.Services;

public interface ISalesEntryService
{
    // Loads everything the Entry page needs.
    Task<SalesEntryViewModel> GetEntryPageAsync();

    // Records a sale, decrements stock, writes an audit log, and regenerates alerts.
    Task<RecordSaleResult> RecordSaleAsync(
        int productId,
        decimal quantity,
        string username,
        string? notes,
        DateTime date);
}