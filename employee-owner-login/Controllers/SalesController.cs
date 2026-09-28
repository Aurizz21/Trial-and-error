using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize(Roles = "Owner,Employee")]
public class SalesController : Controller
{
    private readonly IInventoryService inventoryService;

    public SalesController(IInventoryService inventoryService)
    {
        this.inventoryService = inventoryService;
    }

    [HttpGet]
    public IActionResult Entry()
    {
        var username = User.Identity?.Name ?? string.Empty;
        var allEntries = inventoryService.GetTodaysEntries();

        ViewData["CriticalAlertCount"] = inventoryService.GetLowStockProducts().Count(alert => alert.Status == "Critical");
        return View(new SalesEntryViewModel
        {
            Products = inventoryService.GetProducts(),
            TodaysSummary = inventoryService.GetTodaysSalesSummary(),
            TodaysEntries = User.IsInRole("Owner")
                ? allEntries
                : allEntries.Where(entry => string.Equals(entry.EnteredBy, username, StringComparison.OrdinalIgnoreCase)).ToList(),
            LowStockProducts = inventoryService.GetLowStockProducts()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Owner,Employee")]
    public IActionResult RecordSale(RecordSaleRequest request)
    {
        if (!ModelState.IsValid)
        {
            var message = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault() ?? "Please enter valid sale details.";
            return BadRequest(new { success = false, message });
        }

        var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized(new { success = false, message = "An authenticated username is required." });
        }

        var result = inventoryService.RecordSale(
            request.ProductId!.Value,
            request.Quantity,
            username,
            request.Notes,
            request.Date!.Value);

        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = true,
            updatedProduct = result.UpdatedProduct,
            newEntry = result.NewEntry,
            todaysSummary = inventoryService.GetTodaysSalesSummary(),
            lowStockProducts = inventoryService.GetLowStockProducts()
        });
    }

    [HttpGet]
    [Authorize(Roles = RolePermissions.Owner)]
    public IActionResult History() => View();
}