using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class SalesController : Controller
{
            private readonly ISalesEntryService _salesEntry;
    private readonly ISalesHistoryService _salesHistory;

    public SalesController(ISalesEntryService salesEntry, ISalesHistoryService salesHistory)
    {
        _salesEntry = salesEntry;
        _salesHistory = salesHistory;
    }

        [HttpGet]
    public async Task<IActionResult> Entry()
    {
        var model = await _salesEntry.GetEntryPageAsync();
        ViewData["CriticalAlertCount"] = model.LowStockProducts.Count(a => a.Status == "Critical");
        return View(model);
    }

        [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordSale(RecordSaleRequest request)
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

        var result = await _salesEntry.RecordSaleAsync(
            request.ProductId!.Value,
            request.Quantity,
            username,
            request.Notes,
            request.Date!.Value);

        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        // Reload the page state for the JS to update the UI
        var page = await _salesEntry.GetEntryPageAsync();

        return Ok(new
        {
            success = true,
            message = result.Message,
            updatedProduct = result.UpdatedProduct,
            newEntry = result.NewEntry,
            todaysSummary = page.TodaysSummary,
            lowStockProducts = page.LowStockProducts
        });
    }

        [HttpGet]
    public IActionResult History() => View();

    // JSON endpoint the History page calls via fetch() when applying filters or paging.
    [HttpGet]
    public async Task<IActionResult> HistoryData(
        DateTime? from,
        DateTime? to,
        int? productId,
        string? enteredBy,
        int page = 1,
        int pageSize = 50)
    {
        var filter = new SalesHistoryFilter
        {
            From = from,
            To = to,
            ProductId = productId,
            EnteredBy = enteredBy,
            Page = page,
            PageSize = pageSize
        };

        var data = await _salesHistory.GetPageAsync(filter);

        return Ok(new
        {
            success = true,
            rows = data.Rows.Select(r => new
            {
                id = r.Id,
                soldAt = r.SoldAt.ToString("yyyy-MM-dd"),
                time = r.SoldAt.ToString("HH:mm"),
                productName = r.ProductName,
                units = r.Units,
                quantity = r.Quantity,
                enteredBy = r.EnteredBy,
                notes = r.Notes
            }),
            totalCount = data.TotalCount,
            page = data.Page,
            pageSize = data.PageSize,
            totalPages = data.TotalPages,
            products = data.Products.Select(p => new { id = p.Id, name = p.Name }),
            users = data.Users.Select(u => u.Username)
        });
    }
}