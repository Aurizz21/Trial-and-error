using PoultryOS.Models;

namespace PoultryOS.Services;

public sealed class NotificationService
{
    private readonly object syncRoot = new();
    private readonly List<NotificationItem> notifications = new();
    private readonly Dictionary<string, HashSet<string>> readByUser = new(StringComparer.OrdinalIgnoreCase);

    public NotificationService()
    {
        SeedNotifications();
    }

    public List<NotificationItem> GetNotificationsForUser(string username, string role)
    {
        lock (syncRoot)
        {
            var allowedModules = RolePermissions.GetModulesForRole(role).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return notifications
                .Where(item => IsVisibleToUser(item, username, role, allowedModules))
                .OrderByDescending(item => item.Timestamp)
                .Select(item => new NotificationItem
                {
                    Id = item.Id,
                    Type = item.Type,
                    Title = item.Title,
                    Message = item.Message,
                    Timestamp = item.Timestamp,
                    SourceModule = item.SourceModule,
                    TargetRoles = item.TargetRoles.ToList(),
                    TargetUser = item.TargetUser,
                    IsRead = IsReadForUser(username, item.Id)
                })
                .ToList();
        }
    }

    public int GetUnreadCount(string username, string role)
    {
        return GetNotificationsForUser(username, role).Count(item => !item.IsRead);
    }

    public bool MarkRead(string username, string role, string notificationId)
    {
        if (string.IsNullOrWhiteSpace(notificationId))
        {
            return false;
        }

        lock (syncRoot)
        {
            if (!GetNotificationsForUser(username, role).Any(item => item.Id == notificationId))
            {
                return false;
            }

            var readIds = GetReadSet(username);
            readIds.Add(notificationId);
            return true;
        }
    }

    public bool MarkAllRead(string username, string role)
    {
        lock (syncRoot)
        {
            var visibleIds = GetNotificationsForUser(username, role).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (visibleIds.Count == 0)
            {
                return false;
            }

            var readIds = GetReadSet(username);
            foreach (var id in visibleIds)
            {
                readIds.Add(id);
            }

            return true;
        }
    }

    private bool IsVisibleToUser(NotificationItem item, string username, string role, HashSet<string> allowedModules)
    {
        if (!string.IsNullOrWhiteSpace(item.TargetUser))
        {
            return item.TargetUser.Equals(username, StringComparison.OrdinalIgnoreCase);
        }

        if (item.TargetRoles.Count > 0)
        {
            return item.TargetRoles.Any(targetRole => targetRole.Equals(role, StringComparison.OrdinalIgnoreCase))
                && allowedModules.Contains(item.SourceModule);
        }

        return allowedModules.Contains(item.SourceModule);
    }

    private bool IsReadForUser(string username, string notificationId)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(notificationId))
        {
            return false;
        }

        return readByUser.TryGetValue(username, out var readIds) && readIds.Contains(notificationId);
    }

    private HashSet<string> GetReadSet(string username)
    {
        if (!readByUser.TryGetValue(username, out var readIds))
        {
            readIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readByUser[username] = readIds;
        }

        return readIds;
    }

    private void SeedNotifications()
    {
        notifications.Add(new NotificationItem
        {
            Id = "breast-critical",
            Type = "critical",
            Title = "Chicken Breast stock critical",
            Message = "8 kg remaining, reorder 12 kg.",
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            SourceModule = RolePermissions.ModuleStockAlerts,
            TargetRoles = new List<string> { RolePermissions.Owner }
        });

        notifications.Add(new NotificationItem
        {
            Id = "wings-warning",
            Type = "warning",
            Title = "Chicken Wings approaching reorder threshold",
            Message = "Current stock is 18 kg; review the 20 kg reorder threshold.",
            Timestamp = DateTime.UtcNow.AddMinutes(-42),
            SourceModule = RolePermissions.ModuleForecast,
            TargetRoles = new List<string> { RolePermissions.Employee }
        });

        notifications.Add(new NotificationItem
        {
            Id = "thigh-sale",
            Type = "info",
            Title = "Sale recorded: Chicken Thigh",
            Message = "10 kg Chicken Thigh sold by you.",
            Timestamp = DateTime.UtcNow.AddHours(-2),
            SourceModule = RolePermissions.ModuleSalesEntry,
            TargetRoles = new List<string> { RolePermissions.Employee },
            TargetUser = "employee"
        });

        notifications.Add(new NotificationItem
        {
            Id = "feet-threshold",
            Type = "info",
            Title = "Chicken Feet threshold updated",
            Message = "The reorder threshold is now 5 kg.",
            Timestamp = DateTime.UtcNow.AddHours(-5),
            SourceModule = RolePermissions.ModuleStockAlerts,
            TargetRoles = new List<string> { RolePermissions.Owner }
        });

        notifications.Add(new NotificationItem
        {
            Id = "whole-stock",
            Type = "warning",
            Title = "Whole Chicken stock is trending down",
            Message = "130 pcs remain after today's sales.",
            Timestamp = DateTime.UtcNow.AddHours(-26),
            SourceModule = RolePermissions.ModuleDashboard,
            TargetRoles = new List<string> { RolePermissions.Employee }
        });

        notifications.Add(new NotificationItem
        {
            Id = "thigh-forecast",
            Type = "info",
            Title = "Chicken Thigh forecast updated",
            Message = "Projected stock remains healthy for the next 7 days.",
            Timestamp = DateTime.UtcNow.AddDays(-3),
            SourceModule = RolePermissions.ModuleForecast,
            TargetRoles = new List<string> { RolePermissions.Owner, RolePermissions.Employee }
        });
    }
}