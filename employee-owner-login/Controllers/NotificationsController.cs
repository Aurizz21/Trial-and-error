using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryOS.Data;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationsService _notifications;
    private readonly AppDbContext _db;

    public NotificationsController(INotificationsService notifications, AppDbContext db)
    {
        _notifications = notifications;
        _db = db;
    }

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
        var unreadCount = await _db.Notifications
            .CountAsync(n => !n.IsRead && !n.Title.StartsWith("Resolved"));

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
                isRead = n.IsRead,
                stockAlertId = n.StockAlertId
            });

        return Ok(new
        {
            notifications = recent,
            unreadCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var unreadNotifications = await _db.Notifications
            .Where(notification => !notification.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
            notification.IsRead = true;

        await _db.SaveChangesAsync();

        return Ok(new { success = true, updatedCount = unreadNotifications.Count });
    }
}