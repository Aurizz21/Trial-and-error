using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IInventoryService
{
    DashboardSummaryViewModel GetSummary(string username, string role);
    DashboardSummaryViewModel RecordSale(int productId, int quantity, string username, string role);
}
