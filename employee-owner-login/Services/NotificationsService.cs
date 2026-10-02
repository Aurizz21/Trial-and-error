using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;

namespace PoultryOS.Services;

public class NotificationsService : INotificationsService
{
    private readonly AppDbContext _db;

    public NotificationsService(AppDbContext db) => _db = db;

    public async Task<NotificationsPageViewModel> GetAsync(string typeFilter)
    {
        // Normalise the filter
        var filter = typeFilter?.Trim() ?? "All";
        if (filter != "Critical" && filter != "Warning" && filter != "Info" && filter != "Unread")
            filter = "All";

        // Base query: everything, newest first
        var query = _db.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .AsQueryable();

        // Counts BEFORE the filter -- KPIs show the totals
        var totalCount = await query.CountAsync();
        var unreadCount = await query.CountAsync(n => !n.IsRead);
        var criticalCount = await query.CountAsync(n => n.Type == "Critical");
        var warningCount = await query.CountAsync(n => n.Type == "Warning");

        // Apply the filter for the actual list
        query = filter switch
        {
            "Critical" => query.Where(n => n.Type == "Critical"),
            "Warning" => query.Where(n => n.Type == "Warning"),
            "Info" => query.Where(n => n.Type == "Info"),
            "Unread" => query.Where(n => !n.IsRead),
            _ => query
        };

        var rows = await query
            .Select(n => new NotificationRowViewModel
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                StockAlertId = n.StockAlertId
            })
            .ToListAsync();

        return new NotificationsPageViewModel
        {
            Notifications = rows,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            CriticalCount = criticalCount,
            WarningCount = warningCount,
            TypeFilter = filter
        };
    }
}