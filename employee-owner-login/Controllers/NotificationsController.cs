using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Models;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize(Roles = "Owner,Employee")]
public class NotificationsController : Controller
{
    private readonly NotificationService notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        this.notificationService = notificationService;
    }

    [HttpGet]
    public JsonResult List()
    {
        var username = User.Identity?.Name ?? string.Empty;
        var role = RolePermissions.GetCurrentRole(User);
        var notifications = notificationService.GetNotificationsForUser(username, role);
        return Json(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public JsonResult MarkRead([FromBody] NotificationReadRequest request)
    {
        var username = User.Identity?.Name ?? string.Empty;
        var role = RolePermissions.GetCurrentRole(User);
        var success = notificationService.MarkRead(username, role, request?.NotificationId ?? string.Empty);
        return Json(new { success, unreadCount = notificationService.GetUnreadCount(username, role) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public JsonResult MarkAllRead()
    {
        var username = User.Identity?.Name ?? string.Empty;
        var role = RolePermissions.GetCurrentRole(User);
        var success = notificationService.MarkAllRead(username, role);
        return Json(new { success, unreadCount = notificationService.GetUnreadCount(username, role) });
    }

    public IActionResult Index() => View();
}