using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var username = User.Identity?.Name ?? "owner";
        var role = User.IsInRole("Owner") ? "Owner" : "Employee";
        var summary = await _dashboard.GetSummaryAsync(username, role);
        return View(summary);
    }

    [HttpGet]
    public async Task<IActionResult> Summary()
    {
        var username = User.Identity?.Name ?? "owner";
        var role = User.IsInRole("Owner") ? "Owner" : "Employee";
        var summary = await _dashboard.GetSummaryAsync(username, role);
        return Json(summary);
    }
}