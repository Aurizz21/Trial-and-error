using Microsoft.AspNetCore.Mvc;

namespace PoultryOS.Controllers;

public class NotificationsController : Controller
{
    public IActionResult Index() => View();
}