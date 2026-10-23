using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;

namespace PoultryOS.ViewComponents;

public class CriticalAlertBadgeViewComponent : ViewComponent
{
    private readonly AppDbContext _db;

    public CriticalAlertBadgeViewComponent(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Counts BOTH Critical and Warning alerts that are unresolved
        var count = await _db.StockAlerts
            .AsNoTracking()
            .CountAsync(alert => 
                alert.ResolvedAt == null 
                && (alert.Status == "Critical" || alert.Status == "Warning"));

        return View(count);
    }
}