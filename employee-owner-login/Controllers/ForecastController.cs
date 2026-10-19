using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class ForecastController : Controller
{
    private readonly IForecastService _forecast;

    public ForecastController(IForecastService forecast) => _forecast = forecast;

    // GET /Forecast
    [HttpGet]
    public async Task<IActionResult> Index(bool useAi = true)
    {
        var model = await _forecast.GetForecastAsync(useAi);
        return View(model);
    }
}