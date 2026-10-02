using PoultryOS.Models;

namespace PoultryOS.Services;

public interface ISalesHistoryService
{
    // Returns one page of sales rows plus total count and dropdown options.
    Task<SalesHistoryPageViewModel> GetPageAsync(SalesHistoryFilter filter);
}