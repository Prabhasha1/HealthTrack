using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>Creates and reads in-app notifications.</summary>
    public interface INotificationService
    {
        Task NotifyAsync(string userId, string title, string message,
                         NotificationType type = NotificationType.General,
                         string? link = null, string? dedupKey = null);
        Task<int> GetUnreadCountAsync(string userId);
        Task<List<Notification>> GetRecentAsync(string userId, int count);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;

        public NotificationService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Adds a notification. When a dedupKey is given and a notification with the same
        /// key already exists for the user, nothing is added (prevents duplicate reminders).
        /// </summary>
        public async Task NotifyAsync(string userId, string title, string message,
                                      NotificationType type = NotificationType.General,
                                      string? link = null, string? dedupKey = null)
        {
            if (string.IsNullOrEmpty(userId)) return;

            if (dedupKey != null &&
                await _db.Notifications.AnyAsync(n => n.UserId == userId && n.DedupKey == dedupKey))
            {
                return;
            }

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Link = link,
                DedupKey = dedupKey,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        public Task<int> GetUnreadCountAsync(string userId) =>
            _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public Task<List<Notification>> GetRecentAsync(string userId, int count) =>
            _db.Notifications.Where(n => n.UserId == userId)
                             .OrderByDescending(n => n.CreatedAt)
                             .Take(count)
                             .ToListAsync();
    }
}
