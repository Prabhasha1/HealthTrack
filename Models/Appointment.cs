using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>A consultation booked by a client with a trainer or health professional.</summary>
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;
        public ApplicationUser? Client { get; set; }

        [Required]
        public string TrainerId { get; set; } = string.Empty;
        public ApplicationUser? Trainer { get; set; }

        public AppointmentType Type { get; set; }

        public DateTime StartTime { get; set; }

        [Range(15, 240)]
        public int DurationMinutes { get; set; } = 60;

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        /// <summary>Reason for the visit written by the client (encrypted at rest).</summary>
        [StringLength(500)]
        public string? ClientNotes { get; set; }

        /// <summary>Response written by the trainer (encrypted at rest).</summary>
        [StringLength(500)]
        public string? TrainerNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>True once the background service has sent the "upcoming appointment" reminder.</summary>
        public bool ReminderSent { get; set; }

        public DateTime EndTime => StartTime.AddMinutes(DurationMinutes);

        /// <summary>Appointments that still block the trainer's calendar.</summary>
        public bool IsOpen => Status == AppointmentStatus.Pending
                           || Status == AppointmentStatus.Approved
                           || Status == AppointmentStatus.Rescheduled;
    }
}
