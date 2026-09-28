using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize(Roles = "Owner,Employee")]
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
        var role = User.IsInRole(RolePermissions.Owner) ? RolePermissions.Owner : RolePermissions.Employee;
        var summary = inventoryService.GetSummary(username, role);
        return View(summary);
    }

    [HttpGet]
    public IActionResult Summary()
    {
        var username = User.Identity?.Name ?? "owner";
        var role = User.IsInRole(RolePermissions.Owner) ? RolePermissions.Owner : RolePermissions.Employee;
        var summary = inventoryService.GetSummary(username, role);

        if (User.IsInRole(RolePermissions.Employee))
        {
            summary.RecentActivity = [];
            summary.ForecastAccuracy = 0;
            foreach (var alert in summary.ActiveAlerts)
            {
                alert.SuggestedReorderQuantity = 0m;
                alert.Message = $"{alert.ProductName} has {alert.CurrentStock:0.##} {alert.Units} remaining and {alert.DaysRemaining:0.0} days left.";
            }
        }

        return Json(summary);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult QuickSale(int productId, decimal quantity)
    {
        try
        {

            var username = User.Identity?.Name ?? "system";
            var role = User.IsInRole(RolePermissions.Owner) ? RolePermissions.Owner : RolePermissions.Employee;
            var summary = inventoryService.RecordSale(productId, quantity, username, role);
            return Json(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
