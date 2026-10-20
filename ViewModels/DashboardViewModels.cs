using HealthTrack.Models;

namespace HealthTrack.ViewModels
{
    public class ClientDashboardViewModel
    {
        public ApplicationUser Client { get; set; } = null!;
        public HealthMeasurement? LatestMeasurement { get; set; }
        public double? WeightChangeKg { get; set; }

        public int WorkoutsThisWeek { get; set; }
        public int MinutesThisWeek { get; set; }
        public int CaloriesBurnedThisWeek { get; set; }
        public int CaloriesEatenToday { get; set; }
        public int? DailyCalorieTarget { get; set; }

        public WorkoutPlan? ActiveWorkoutPlan { get; set; }
        public List<WorkoutExercise> TodaysExercises { get; set; } = new();
        public DietPlan? ActiveDietPlan { get; set; }
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<TrainerFeedback> RecentFeedback { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        public List<string> WeightDates { get; set; } = new();
        public List<double> WeightValues { get; set; } = new();
    }

    public class TrainerClientSummary
    {
        public string ClientId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public double? CurrentWeight { get; set; }
        public double? CurrentBmi { get; set; }
        public double? WeightChange30Days { get; set; }
        public int WorkoutsLast7Days { get; set; }
        public DateTime? LastActivity { get; set; }
        public bool HasActiveWorkoutPlan { get; set; }
        public bool HasActiveDietPlan { get; set; }
    }

    public class TrainerDashboardViewModel
    {
        public ApplicationUser Trainer { get; set; } = null!;
        public List<TrainerClientSummary> Clients { get; set; } = new();
        public List<Appointment> PendingAppointments { get; set; } = new();
        public List<Appointment> TodaysAppointments { get; set; } = new();
        public int ActiveWorkoutPlans { get; set; }
        public int ActiveDietPlans { get; set; }
        public int ClientsNeedingAttention { get; set; }
        public List<string> ClientNames { get; set; } = new();
        public List<int> ClientWorkoutCounts { get; set; } = new();
    }

    public class ClientDetailViewModel
    {
        public ApplicationUser Client { get; set; } = null!;
        public ProgressViewModel Progress { get; set; } = new();
        public List<WorkoutPlan> WorkoutPlans { get; set; } = new();
        public List<DietPlan> DietPlans { get; set; } = new();
        public List<WorkoutLog> RecentWorkouts { get; set; } = new();
        public List<MealLog> RecentMeals { get; set; } = new();
        public List<TrainerFeedback> Feedback { get; set; } = new();
        public FeedbackInputModel NewFeedback { get; set; } = new();
    }

    public class FeedbackInputModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string ClientId { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 5)]
        public string Message { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(1, 5)]
        public int? Rating { get; set; }
    }
}
