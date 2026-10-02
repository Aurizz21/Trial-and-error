using PoultryOS.Models;

namespace PoultryOS.Services;

public interface INotificationsService
{
    // Returns notifications, optionally filtered by type ("All", "Critical", "Warning", "Info", "Unread").
    Task<NotificationsPageViewModel> GetAsync(string typeFilter);
}