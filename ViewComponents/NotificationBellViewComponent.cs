using System.Security.Claims;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.ViewComponents
{
    /// <summary>Renders the bell icon with the unread count and the latest five notifications.</summary>
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly INotificationService _notifications;

        public NotificationBellViewComponent(INotificationService notifications)
        {
            _notifications = notifications;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            string? userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Content(string.Empty);
            ViewBag.Unread = await _notifications.GetUnreadCountAsync(userId);
            return View(await _notifications.GetRecentAsync(userId, 5));
        }
    }
}
