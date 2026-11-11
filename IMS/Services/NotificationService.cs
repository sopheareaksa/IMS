using Microsoft.EntityFrameworkCore;
using IMS.Models;

namespace IMS.Services;

public class NotificationService : INotificationService
{
    private readonly InsuranceContext _context;

    public NotificationService(InsuranceContext context)
    {
        _context = context;
    }

    public async Task NotifyAdminAsync(string title, string message, string? linkUrl = null, string type = "General")
    {
        var notification = new Notification
        {
            targetRole = "Admin",
            title = title,
            message = message,
            linkUrl = linkUrl,
            type = type,
            createdAt = DateTime.UtcNow,
            isRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    public async Task NotifyCompanyAsync(int companyId, string title, string message, string? linkUrl = null, string type = "General", bool alsoNotifyAdmin = true)
    {
        // 1. Create company-level notification
        var compNotification = new Notification
        {
            companyId = companyId,
            targetRole = "Agent",
            title = title,
            message = message,
            linkUrl = linkUrl,
            type = type,
            createdAt = DateTime.UtcNow,
            isRead = false
        };
        _context.Notifications.Add(compNotification);

        // 2. Notify all agents of that company
        var agents = await _context.Agents
            .Where(a => a.companyId == companyId && a.status == "Active")
            .ToListAsync();

        foreach (var agent in agents)
        {
            _context.Notifications.Add(new Notification
            {
                userId = agent.userId,
                companyId = companyId,
                targetRole = "Agent",
                title = title,
                message = message,
                linkUrl = linkUrl,
                type = type,
                createdAt = DateTime.UtcNow,
                isRead = false
            });
        }

        // 3. Notify all staff of that company who have receiveAlerts enabled
        var staffMembers = await _context.StaffMembers
            .Where(s => s.companyId == companyId && s.status == "Active" && s.receiveAlerts)
            .ToListAsync();

        foreach (var staff in staffMembers)
        {
            _context.Notifications.Add(new Notification
            {
                userId = staff.userId,
                companyId = companyId,
                targetRole = "Staff",
                title = title,
                message = message,
                linkUrl = linkUrl,
                type = type,
                createdAt = DateTime.UtcNow,
                isRead = false
            });
        }

        // 4. Optionally also notify Admin as system owner
        if (alsoNotifyAdmin)
        {
            var adminLink = linkUrl?.StartsWith("/Agent/Quotations") == true ? "/Admin/Quotations" :
                            linkUrl?.StartsWith("/Agent/Claims") == true ? "/Admin/Claims" :
                            linkUrl?.StartsWith("/Agent/Policies") == true ? "/Admin/Policies" :
                            linkUrl;

            _context.Notifications.Add(new Notification
            {
                targetRole = "Admin",
                title = $"[Platform Alert] {title}",
                message = message,
                linkUrl = adminLink,
                type = type,
                createdAt = DateTime.UtcNow,
                isRead = false
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task NotifyUserAsync(int userId, string title, string message, string? linkUrl = null, string type = "General")
    {
        var notification = new Notification
        {
            userId = userId,
            title = title,
            message = message,
            linkUrl = linkUrl,
            type = type,
            createdAt = DateTime.UtcNow,
            isRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId, string role, int? companyId)
    {
        return await GetBaseQuery(userId, role, companyId)
            .Where(n => !n.isRead)
            .CountAsync();
    }

    public async Task<List<Notification>> GetRecentNotificationsAsync(int userId, string role, int? companyId, int count = 10)
    {
        return await GetBaseQuery(userId, role, companyId)
            .OrderByDescending(n => n.createdAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetAllNotificationsAsync(int userId, string role, int? companyId)
    {
        return await GetBaseQuery(userId, role, companyId)
            .OrderByDescending(n => n.createdAt)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.isRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId, string role, int? companyId)
    {
        var notifications = await GetBaseQuery(userId, role, companyId)
            .Where(n => !n.isRead)
            .ToListAsync();

        foreach (var n in notifications)
        {
            n.isRead = true;
        }

        await _context.SaveChangesAsync();
    }

    private IQueryable<Notification> GetBaseQuery(int userId, string role, int? companyId)
    {
        if (role == "Admin")
        {
            return _context.Notifications
                .Where(n => n.targetRole == "Admin" || n.userId == userId || (n.userId == null && n.companyId == null && n.targetRole == null));
        }

        if (role == "Agent")
        {
            return _context.Notifications
                .Where(n => n.userId == userId || (companyId.HasValue && n.companyId == companyId.Value && (n.targetRole == "Agent" || n.targetRole == null)));
        }

        if (role == "Staff")
        {
            return _context.Notifications
                .Where(n => n.userId == userId || (companyId.HasValue && n.companyId == companyId.Value && (n.targetRole == "Staff" || n.targetRole == null)));
        }

        // Customer or other
        return _context.Notifications.Where(n => n.userId == userId);
    }
}

