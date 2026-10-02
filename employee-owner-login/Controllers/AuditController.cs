using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize(Roles = "Owner")]
public class AuditController : Controller
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    // GET /Audit?actionType=All&user=All&page=1
    [HttpGet]
    public async Task<IActionResult> Index(string actionType = "All", string user = "All", int page = 1)
    {
        var model = await _audit.GetPageAsync(actionType, user, page, 50);
        return View(model);
    }
}