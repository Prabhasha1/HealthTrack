using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>Written feedback from a trainer to a client.</summary>
    public class TrainerFeedback
    {
        public int Id { get; set; }

        [Required]
        public string TrainerId { get; set; } = string.Empty;
        public ApplicationUser? Trainer { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;
        public ApplicationUser? Client { get; set; }

        [Required, StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Range(1, 5)]
        public int? Rating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>An in-app notification or reminder shown under the bell icon.</summary>
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; }

        /// <summary>Optional relative URL the user is taken to when opening the notification.</summary>
        [StringLength(200)]
        public string? Link { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Used to stop the reminder service creating the same reminder twice,
        /// e.g. "meal:15:2026-05-10". Null for ordinary notifications.
        /// </summary>
        [StringLength(100)]
        public string? DedupKey { get; set; }
    }

    /// <summary>Audit trail of important actions, used by the admin dashboard.</summary>
    public class ActivityLog
    {
        public int Id { get; set; }

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Required, StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Details { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
