using Microsoft.AspNetCore.Mvc;

namespace PoultryOS.Controllers;

public class StockController : Controller
{
    public IActionResult Alerts() => View();
}