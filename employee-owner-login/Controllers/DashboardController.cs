using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IInventoryService inventoryService;

    public DashboardController(IInventoryService inventoryService)
    {
        this.inventoryService = inventoryService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var username = User.Identity?.Name ?? "owner";
        var role = User.IsInRole("Owner") ? "Owner" : "Employee";
        var summary = inventoryService.GetSummary(username, role);
        return View(summary);
    }

    [HttpGet]
    public IActionResult Summary()
    {
        var username = User.Identity?.Name ?? "owner";
        var role = User.IsInRole("Owner") ? "Owner" : "Employee";
        var summary = inventoryService.GetSummary(username, role);
        return Json(summary);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult QuickSale(int productId, int quantity)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            var role = User.IsInRole("Owner") ? "Owner" : "Employee";
            var summary = inventoryService.RecordSale(productId, quantity, username, role);
            return Json(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
