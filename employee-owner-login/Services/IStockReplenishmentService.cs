using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IStockReplenishmentService
{
    // Records a restock event: adds stock, resolves any open alert, writes audit log.
    Task<RestockResult> RestockAsync(RestockRequest request, string username);
}