using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PoultryOS.Controllers;

[Authorize(Roles = "Owner,Employee")]
public class ForecastController : Controller
{
    public IActionResult Index() => View();
}