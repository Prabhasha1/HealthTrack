using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.ViewModels
{
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required, StringLength(100, MinimumLength = 3)]
        [Display(Name = "Full name")]
        [RegularExpression(@"^[A-Za-z\u00C0-\u024F .'\-]+$", ErrorMessage = "Name may contain letters, spaces, dots, apostrophes and hyphens only.")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Phone, Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date), Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [Range(50, 260), Display(Name = "Height (cm)")]
        public double? HeightCm { get; set; }

        [StringLength(250), Display(Name = "Fitness goal")]
        public string? FitnessGoal { get; set; }

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters long.")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "I agree to the privacy notice")]
        [MustBeTrue(ErrorMessage = "You must accept the privacy notice to register.")]
        public bool AcceptPrivacy { get; set; }
    }

    /// <summary>Validation attribute for a checkbox that must be ticked (e.g. consent).</summary>
    public class MustBeTrueAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value) => value is bool b && b;
    }

    public class ProfileViewModel
    {
        [Required, StringLength(100, MinimumLength = 3), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Phone, Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date), Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [Range(50, 260), Display(Name = "Height (cm)")]
        public double? HeightCm { get; set; }

        [StringLength(250), Display(Name = "Fitness goal")]
        public string? FitnessGoal { get; set; }

        [StringLength(100)]
        public string? Specialization { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        [Display(Name = "Workout reminders")]
        public bool WorkoutRemindersEnabled { get; set; }

        [Display(Name = "Meal reminders")]
        public bool MealRemindersEnabled { get; set; }

        [Display(Name = "Appointment reminders")]
        public bool AppointmentRemindersEnabled { get; set; }

        public bool IsTrainer { get; set; }
        public string? AssignedTrainerName { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "New password")]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
