using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HealthTrack.ViewModels
{
    /// <summary>Form used to create/edit a workout plan together with its exercise rows.</summary>
    public class WorkoutPlanForm : IValidatableObject
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Goal { get; set; }

        [Required(ErrorMessage = "Please choose a client.")]
        [Display(Name = "Client")]
        public string ClientId { get; set; } = string.Empty;

        [DataType(DataType.Date), Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date), Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public List<ExerciseInput> Exercises { get; set; } = new();

        public List<SelectListItem> ClientOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
                yield return new ValidationResult("End date must be on or after the start date.", new[] { nameof(EndDate) });
            if (Exercises.Count(e => !string.IsNullOrWhiteSpace(e.Name)) == 0)
                yield return new ValidationResult("Add at least one exercise.", new[] { nameof(Exercises) });
        }
    }

    public class ExerciseInput
    {
        [StringLength(100)]
        public string? Name { get; set; }
        public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
        [Range(0, 50)] public int Sets { get; set; }
        [Range(0, 500)] public int Reps { get; set; }
        [Range(0, 600)] public int DurationMinutes { get; set; }
        public Intensity Intensity { get; set; } = Intensity.Moderate;
        [StringLength(250)] public string? Notes { get; set; }
    }

    /// <summary>Form used to create/edit a diet plan together with its meals.</summary>
    public class DietPlanForm : IValidatableObject
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please choose a client.")]
        [Display(Name = "Client")]
        public string ClientId { get; set; } = string.Empty;

        [Range(800, 6000), Display(Name = "Daily calorie target (kcal)")]
        public int DailyCalorieTarget { get; set; } = 2000;

        [DataType(DataType.Date), Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date), Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public List<MealInput> Meals { get; set; } = new();

        public List<SelectListItem> ClientOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
                yield return new ValidationResult("End date must be on or after the start date.", new[] { nameof(EndDate) });
            if (Meals.Count(m => !string.IsNullOrWhiteSpace(m.Description)) == 0)
                yield return new ValidationResult("Add at least one meal.", new[] { nameof(Meals) });
        }
    }

    public class MealInput
    {
        public MealType MealType { get; set; }
        [DataType(DataType.Time)] public TimeSpan Time { get; set; } = new TimeSpan(8, 0, 0);
        [StringLength(200)] public string? Description { get; set; }
        [Range(0, 3000)] public int Calories { get; set; }
    }

    public class AppointmentForm : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose a trainer or professional.")]
        [Display(Name = "Trainer / professional")]
        public string TrainerId { get; set; } = string.Empty;

        [Display(Name = "Appointment type")]
        public AppointmentType Type { get; set; }

        [Required, Display(Name = "Date and time")]
        public DateTime StartTime { get; set; } = DateTime.Today.AddDays(1).AddHours(10);

        [Range(15, 240), Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; } = 60;

        [StringLength(500), Display(Name = "Reason / notes")]
        public string? ClientNotes { get; set; }

        public List<SelectListItem> TrainerOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (StartTime <= DateTime.Now)
                yield return new ValidationResult("The appointment must be in the future.", new[] { nameof(StartTime) });
            if (StartTime > DateTime.Now.AddMonths(6))
                yield return new ValidationResult("Appointments can be booked up to 6 months ahead.", new[] { nameof(StartTime) });
            var end = StartTime.AddMinutes(DurationMinutes);
            if (StartTime.Hour < 6 || end.TimeOfDay > new TimeSpan(21, 0, 0) || end.Date != StartTime.Date)
                yield return new ValidationResult("Appointments must be between 06:00 and 21:00.", new[] { nameof(StartTime) });
        }
    }

    public class RescheduleForm : IValidatableObject
    {
        public int Id { get; set; }

        [Required, Display(Name = "New date and time")]
        public DateTime StartTime { get; set; }

        [Range(15, 240), Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; } = 60;

        [StringLength(500), Display(Name = "Message to client")]
        public string? TrainerNotes { get; set; }

        public Appointment? Original { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (StartTime <= DateTime.Now)
                yield return new ValidationResult("The new time must be in the future.", new[] { nameof(StartTime) });
            var end = StartTime.AddMinutes(DurationMinutes);
            if (StartTime.Hour < 6 || end.TimeOfDay > new TimeSpan(21, 0, 0) || end.Date != StartTime.Date)
                yield return new ValidationResult("Appointments must be between 06:00 and 21:00.", new[] { nameof(StartTime) });
        }
    }
}
