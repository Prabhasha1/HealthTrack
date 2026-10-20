using HealthTrack.Data;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>The user's notification inbox (reminders and updates).</summary>
    public class NotificationsController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public NotificationsController(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<IActionResult> Index(bool unreadOnly = false)
        {
            var query = _db.Notifications.Where(n => n.UserId == CurrentUserId);
            if (unreadOnly) query = query.Where(n => !n.IsRead);
            ViewBag.UnreadOnly = unreadOnly;
            return View(await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync());
        }

        /// <summary>Marks a notification read and follows its link (if it is a local URL).</summary>
        [HttpPost]
        public async Task<IActionResult> Open(int id)
        {
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);
            if (n == null) return NotFound();
            n.IsRead = true;
            await _db.SaveChangesAsync();
            if (!string.IsNullOrEmpty(n.Link) && Url.IsLocalUrl(n.Link)) return LocalRedirect(n.Link);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var unread = await _db.Notifications.Where(n => n.UserId == CurrentUserId && !n.IsRead).ToListAsync();
            unread.ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();
            ShowSuccess($"{unread.Count} notification(s) marked as read.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);
            if (n == null) return NotFound();
            _db.Notifications.Remove(n);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Polled by the navigation bar every minute to update the bell badge.</summary>
        [HttpGet]
        public async Task<IActionResult> UnreadCount() =>
            Json(new { count = await _notifications.GetUnreadCountAsync(CurrentUserId) });
    }
}
