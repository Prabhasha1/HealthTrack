using HealthTrack.Data;
using HealthTrack.Models;

namespace HealthTrack.Services
{
    /// <summary>Writes entries to the audit log shown on the admin dashboard.</summary>
    public interface IActivityLogger
    {
        Task LogAsync(string? userId, string action, string? details = null);
    }

    public class ActivityLogger : IActivityLogger
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<ActivityLogger> _logger;

        public ActivityLogger(ApplicationDbContext db, IHttpContextAccessor http, ILogger<ActivityLogger> logger)
        {
            _db = db;
            _http = http;
            _logger = logger;
        }

        public async Task LogAsync(string? userId, string action, string? details = null)
        {
            try
            {
                _db.ActivityLogs.Add(new ActivityLog
                {
                    UserId = userId,
                    Action = action,
                    Details = details?.Length > 500 ? details.Substring(0, 500) : details,
                    IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Auditing must never break the user's action, so failures are only logged.
                _logger.LogError(ex, "Could not write activity log entry {Action}", action);
            }
        }
    }
}
