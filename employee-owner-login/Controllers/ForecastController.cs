using Microsoft.AspNetCore.Mvc;

namespace PoultryOS.Controllers;

public class ForecastController : Controller
{
    public IActionResult Index() => View();
}