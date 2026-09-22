using Microsoft.AspNetCore.Mvc;

namespace PoultryOS.Controllers;

public class SalesController : Controller
{
    public IActionResult Entry() => View();

    public IActionResult History() => View();
}