using IMS.Models;

namespace IMS.Services;

public interface INotificationService
{
    Task NotifyAdminAsync(string title, string message, string? linkUrl = null, string type = "General");
    Task NotifyCompanyAsync(int companyId, string title, string message, string? linkUrl = null, string type = "General", bool alsoNotifyAdmin = true);
    Task NotifyUserAsync(int userId, string title, string message, string? linkUrl = null, string type = "General");
    Task<int> GetUnreadCountAsync(int userId, string role, int? companyId);
    Task<List<Notification>> GetRecentNotificationsAsync(int userId, string role, int? companyId, int count = 10);
    Task<List<Notification>> GetAllNotificationsAsync(int userId, string role, int? companyId);
    Task MarkAsReadAsync(int notificationId);
    Task MarkAllAsReadAsync(int userId, string role, int? companyId);
}

