using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>A workout the client actually performed.</summary>
    public class WorkoutLog
    {
        public int Id { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;
        public ApplicationUser? Client { get; set; }

        /// <summary>Optional link to the plan the workout belonged to (used for "popular routines").</summary>
        public int? WorkoutPlanId { get; set; }
        public WorkoutPlan? WorkoutPlan { get; set; }

        [Required, StringLength(100)]
        public string ExerciseName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Range(1, 600)]
        public int DurationMinutes { get; set; }

        [Range(0, 5000)]
        public int CaloriesBurned { get; set; }

        public Intensity Intensity { get; set; } = Intensity.Moderate;

        [StringLength(250)]
        public string? Notes { get; set; }
    }

    /// <summary>A meal the client actually ate.</summary>
    public class MealLog
    {
        public int Id { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;
        public ApplicationUser? Client { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        public MealType MealType { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Range(0, 5000)]
        public int Calories { get; set; }

        [Range(0, 500)]
        public double ProteinGrams { get; set; }

        [Range(0, 1000)]
        public double CarbsGrams { get; set; }

        [Range(0, 500)]
        public double FatGrams { get; set; }
    }

    /// <summary>
    /// A health measurement (weight, blood pressure, etc.). BMI is calculated and stored
    /// at save time so trends can be charted without recomputing.
    /// </summary>
    public class HealthMeasurement
    {
        public int Id { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;
        public ApplicationUser? Client { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Range(20, 400)]
        public double WeightKg { get; set; }

        [Range(50, 260)]
        public double HeightCm { get; set; }

        public double Bmi { get; set; }

        [Range(2, 70)]
        public double? BodyFatPercent { get; set; }

        [Range(30, 220)]
        public int? RestingHeartRate { get; set; }

        [Range(60, 250)]
        public int? SystolicBp { get; set; }

        [Range(30, 150)]
        public int? DiastolicBp { get; set; }

        /// <summary>Free-text notes; encrypted at rest because they may contain medical details.</summary>
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
