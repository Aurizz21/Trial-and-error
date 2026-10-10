using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

// Owner-only, enforced on the SERVER.
[Authorize(Roles = "Owner")]
public class ProductsController : Controller
{
    private readonly IProductService _products;
    private readonly IStockReplenishmentService _replenishment;

    public ProductsController(IProductService products, IStockReplenishmentService replenishment)
    {
        _products = products;
        _replenishment = replenishment;
    }

    private string CurrentUser => User.Identity?.Name ?? "unknown";

    // GET /Products/Index -- shows the page
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rows = await _products.GetAllAsync();
        var model = new ProductsPageViewModel
        {
            Products = rows,
            TotalProducts = rows.Count,
            ActiveAlerts = rows.Count(r => r.Status != "Healthy"),
            WeeklySales = rows.Sum(r => r.WeeklyConsumption)
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] ProductFormRequest request)
    {
        if (!ModelState.IsValid) return Invalid();
        return ToJson(await _products.CreateAsync(request, CurrentUser));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([FromForm] int id, [FromForm] ProductFormRequest request)
    {
        if (!ModelState.IsValid) return Invalid();
        return ToJson(await _products.UpdateAsync(id, request, CurrentUser));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int id)
        => ToJson(await _products.DeleteAsync(id, CurrentUser));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateThresholds([FromForm] Dictionary<int, decimal> thresholds)
        => ToJson(await _products.UpdateThresholdsAsync(thresholds, CurrentUser));
    
        [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restock([FromForm] RestockRequest request)
    {
        if (!ModelState.IsValid)
        {
            var message = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Please check the form and try again.";
            return BadRequest(new { success = false, message });
        }

        var result = await _replenishment.RestockAsync(request, CurrentUser);

        return result.Success
            ? Ok(new
            {
                success = true,
                message = result.Message,
                newStock = result.NewStock,
                alertResolved = result.AlertResolved
            })
            : BadRequest(new { success = false, message = result.Message });
    }

    // ---------- helpers ----------

    private IActionResult ToJson(ServiceResult result)
        => result.Success
            ? Ok(new { success = true, message = result.Message })
            : BadRequest(new { success = false, message = result.Message });

    private IActionResult Invalid()
    {
        var message = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Please check the form and try again.";
        return BadRequest(new { success = false, message });
    }
}