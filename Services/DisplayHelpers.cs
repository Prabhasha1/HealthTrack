using System.Text.RegularExpressions;
using HealthTrack.Models;

namespace HealthTrack.Services
{
    /// <summary>Bootstrap icon for each notification type.</summary>
    public static class NotificationIcon
    {
        public static string For(NotificationType type) => type switch
        {
            NotificationType.WorkoutReminder => "bi-activity text-success",
            NotificationType.MealReminder => "bi-egg-fried text-warning",
            NotificationType.AppointmentReminder => "bi-alarm text-primary",
            NotificationType.AppointmentUpdate => "bi-calendar-check text-primary",
            NotificationType.PlanAssigned => "bi-clipboard2-pulse text-info",
            NotificationType.Feedback => "bi-chat-quote text-secondary",
            _ => "bi-info-circle text-muted"
        };
    }

    /// <summary>Formatting helpers used by the Razor views.</summary>
    public static class Display
    {
        /// <summary>Turns "MorningSnack" into "Morning snack".</summary>
        public static string Words(Enum value)
        {
            string s = Regex.Replace(value.ToString(), "(?<!^)([A-Z])", " $1").ToLowerInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        public static string StatusBadge(AppointmentStatus status) => status switch
        {
            AppointmentStatus.Pending => "bg-warning text-dark",
            AppointmentStatus.Approved => "bg-success",
            AppointmentStatus.Rescheduled => "bg-info text-dark",
            AppointmentStatus.Completed => "bg-secondary",
            AppointmentStatus.Cancelled => "bg-light text-dark border",
            AppointmentStatus.Rejected => "bg-danger",
            _ => "bg-secondary"
        };

        /// <summary>Friendly "x days ago" text.</summary>
        public static string Ago(DateTime? when)
        {
            if (when == null) return "never";
            var span = DateTime.Now - when.Value;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} h ago";
            if (span.TotalDays < 2) return "yesterday";
            return $"{(int)span.TotalDays} days ago";
        }
    }
}
