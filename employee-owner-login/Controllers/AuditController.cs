using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;

namespace PoultryOS.Controllers;

[Authorize(Roles = RolePermissions.Owner)]
public class AuditController : Controller
{
    public IActionResult Index() => View();
}