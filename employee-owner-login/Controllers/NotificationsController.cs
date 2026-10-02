using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationsService _notifications;

    public NotificationsController(INotificationsService notifications) => _notifications = notifications;

    // GET /Notifications?type=All|Critical|Warning|Info|Unread
    [HttpGet]
    public async Task<IActionResult> Index(string type = "All")
    {
        var model = await _notifications.GetAsync(type);
        return View(model);
    }

    // JSON endpoint the bell dropdown uses. Returns the latest 5 notifications.
    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var data = await _notifications.GetAsync("All");

        var recent = data.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new
            {
                id = n.Id,
                type = n.Type.ToLowerInvariant(),
                title = n.Title,
                message = n.Message,
                timestamp = n.CreatedAt.ToString("o"),
                isRead = n.IsRead
            });

        return Ok(new
        {
            notifications = recent,
            unreadCount = data.UnreadCount
        });
    }
}