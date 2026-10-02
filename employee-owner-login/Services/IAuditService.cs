using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IAuditService
{
    // Returns one page of audit rows, plus filter options and totals.
    Task<AuditPageViewModel> GetPageAsync(
        string actionTypeFilter,
        string userFilter,
        int page,
        int pageSize);
}