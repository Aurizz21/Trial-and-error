using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class StockController : Controller
{
    private readonly IStockAlertsService _alerts;

    public StockController(IStockAlertsService alerts) => _alerts = alerts;

    // GET /Stock/Alerts?status=All|Critical|Warning
    [HttpGet]
    public async Task<IActionResult> Alerts(string status = "All")
    {
        var model = await _alerts.GetActiveAsync(status);
        return View(model);
    }
}