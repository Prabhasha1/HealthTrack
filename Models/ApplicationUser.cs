using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Models
{
    /// <summary>
    /// A person who can log in. Extends the ASP.NET Core Identity user with profile
    /// fields. The same class is used for Clients, Trainers and Administrators; the
    /// role decides what the person can do.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        /// <summary>Height in centimetres, used to calculate BMI.</summary>
        [Range(50, 260)]
        public double? HeightCm { get; set; }

        [StringLength(250)]
        public string? FitnessGoal { get; set; }

        // ---- Trainer / professional fields ----
        public ProfessionalType? ProfessionalType { get; set; }

        [StringLength(100)]
        public string? Specialization { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        // ---- Client fields ----
        /// <summary>The trainer currently responsible for this client (null if none).</summary>
        public string? AssignedTrainerId { get; set; }
        public ApplicationUser? AssignedTrainer { get; set; }

        // ---- Notification preferences ----
        public bool WorkoutRemindersEnabled { get; set; } = true;
        public bool MealRemindersEnabled { get; set; } = true;
        public bool AppointmentRemindersEnabled { get; set; } = true;

        // ---- Account housekeeping ----
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }

        /// <summary>Date and time the user accepted the privacy notice at registration.</summary>
        public DateTime? PrivacyConsentAt { get; set; }

        /// <summary>Age in whole years, or null when the date of birth is unknown.</summary>
        public int? Age
        {
            get
            {
                if (DateOfBirth == null) return null;
                var today = DateTime.Today;
                int age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }
    }
}
