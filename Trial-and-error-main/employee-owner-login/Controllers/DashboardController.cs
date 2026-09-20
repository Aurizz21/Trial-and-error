using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class DashboardController(DashboardService dashboard) : Controller
{
    private bool IsOwner => User.IsInRole("Owner");

    private string CurrentUser => User.Identity?.Name ?? "unknown";

    [HttpGet]
    public IActionResult Index() => View(dashboard.GetSnapshot(includeActivity: IsOwner));

    // Polled by the page every 30 seconds so several people see the same numbers.
    [HttpGet]
    public IActionResult Snapshot() => Json(dashboard.GetSnapshot(includeActivity: IsOwner));

    // Owner and staff can both record sales.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Sale([FromBody] StockChangeRequest? request) =>
        ToResponse(dashboard.RecordSale(request, CurrentUser, IsOwner));

    // Only the owner can log a restock.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Owner")]
    public IActionResult Restock([FromBody] StockChangeRequest? request) =>
        ToResponse(dashboard.RecordRestock(request, CurrentUser, includeActivity: true));

    private IActionResult ToResponse(StockChangeResult result) =>
        result.Ok ? Json(result) : StatusCode(StatusCodes.Status400BadRequest, result);
}
