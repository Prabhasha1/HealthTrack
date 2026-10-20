using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>A personalised workout routine that a trainer creates for one client.</summary>
    public class WorkoutPlan
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Goal { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public string TrainerId { get; set; } = string.Empty;
        public ApplicationUser? Trainer { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;
        public ApplicationUser? Client { get; set; }

        public ICollection<WorkoutExercise> Exercises { get; set; } = new List<WorkoutExercise>();
    }

    /// <summary>One exercise line inside a workout plan, scheduled on a weekday.</summary>
    public class WorkoutExercise
    {
        public int Id { get; set; }

        public int WorkoutPlanId { get; set; }
        public WorkoutPlan? WorkoutPlan { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DayOfWeek Day { get; set; }

        [Range(0, 50)]
        public int Sets { get; set; }

        [Range(0, 500)]
        public int Reps { get; set; }

        [Range(0, 600)]
        public int DurationMinutes { get; set; }

        public Intensity Intensity { get; set; } = Intensity.Moderate;

        [StringLength(250)]
        public string? Notes { get; set; }
    }
}
