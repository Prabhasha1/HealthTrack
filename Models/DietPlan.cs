using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>A personalised diet schedule that a trainer/nutritionist creates for one client.</summary>
    public class DietPlan
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(800, 6000)]
        public int DailyCalorieTarget { get; set; } = 2000;

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

        public ICollection<DietMeal> Meals { get; set; } = new List<DietMeal>();
    }

    /// <summary>A planned meal in a diet plan (repeats every day the plan is active).</summary>
    public class DietMeal
    {
        public int Id { get; set; }

        public int DietPlanId { get; set; }
        public DietPlan? DietPlan { get; set; }

        public MealType MealType { get; set; }

        /// <summary>Planned time of the meal, e.g. 07:30. Used for meal reminders.</summary>
        [DataType(DataType.Time)]
        public TimeSpan Time { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Range(0, 3000)]
        public int Calories { get; set; }
    }
}
