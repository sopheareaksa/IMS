using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IMS.Services;

namespace IMS.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        var (userId, role, companyId) = GetUserInfo();
        var notifications = await _notificationService.GetAllNotificationsAsync(userId, role, companyId);
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent()
    {
        // The bell polls this endpoint; an expired or missing session should return an
        // empty list rather than a login page the JSON parser cannot read.
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return Json(new { unreadCount = 0, notifications = Array.Empty<object>() });
        }

        var (userId, role, companyId) = GetUserInfo();
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId, role, companyId);
        var recent = await _notificationService.GetRecentNotificationsAsync(userId, role, companyId, 6);

        return Json(new
        {
            unreadCount,
            notifications = recent.Select(n => new
            {
                n.notificationId,
                n.title,
                n.message,
                n.linkUrl,
                n.isRead,
                n.type,
                timeAgo = GetTimeAgo(n.createdAt)
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var (userId, role, companyId) = GetUserInfo();
        await _notificationService.MarkAllAsReadAsync(userId, role, companyId);
        TempData["SuccessMessage"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }

    private (int userId, string role, int? companyId) GetUserInfo()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);

        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";

        var compIdStr = User.FindFirstValue("CompanyId");
        int? compId = int.TryParse(compIdStr, out int cId) ? cId : null;

        return (userId, role, compId);
    }

    private string GetTimeAgo(DateTime dt)
    {
        var span = DateTime.UtcNow - dt;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return dt.ToString("MMM dd, yyyy");
    }
}

