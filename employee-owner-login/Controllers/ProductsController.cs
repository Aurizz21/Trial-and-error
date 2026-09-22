using Microsoft.AspNetCore.Mvc;

namespace PoultryOS.Controllers;

public class ProductsController : Controller
{
    public IActionResult Index() => View();
}