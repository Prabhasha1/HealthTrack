namespace HealthTrack.ViewModels
{
    public class ReportFilter
    {
        public DateTime From { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime To { get; set; } = DateTime.Today;
        /// <summary>Admins may filter by trainer; trainers are always limited to their own clients.</summary>
        public string? TrainerId { get; set; }
    }

    public class ClientPerformanceRow
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string TrainerName { get; set; } = "-";
        public double? StartWeight { get; set; }
        public double? EndWeight { get; set; }
        public double? WeightChange => (StartWeight.HasValue && EndWeight.HasValue)
            ? Math.Round(EndWeight.Value - StartWeight.Value, 1) : null;
        public double? CurrentBmi { get; set; }
        public int Workouts { get; set; }
        public int TotalMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public double AvgDailyCaloriesIn { get; set; }
        public int AppointmentsCompleted { get; set; }
        /// <summary>Workout days logged as a percentage of the days the plan scheduled.</summary>
        public int? AdherencePercent { get; set; }
    }

    public class PopularRoutineRow
    {
        public string ExerciseName { get; set; } = string.Empty;
        public int TimesLogged { get; set; }
        public int UniqueClients { get; set; }
        public int TotalMinutes { get; set; }
        public int AvgCaloriesBurned { get; set; }
        public int TimesPrescribed { get; set; }
    }

    public class EngagementReport
    {
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
        public int NewRegistrations { get; set; }
        public int TotalLogins { get; set; }
        public int WorkoutLogs { get; set; }
        public int MealLogs { get; set; }
        public int Measurements { get; set; }
        public int AppointmentsBooked { get; set; }
        public double LogsPerActiveClient { get; set; }
        public List<string> Dates { get; set; } = new();
        public List<int> LoginsPerDay { get; set; } = new();
        public List<int> EntriesPerDay { get; set; } = new();
        public List<(string Name, int Entries, DateTime? LastLogin)> MostEngaged { get; set; } = new();
    }

    public class ReportsViewModel
    {
        public ReportFilter Filter { get; set; } = new();
        public string ReportType { get; set; } = "performance";
        public bool IsAdmin { get; set; }
        public List<ClientPerformanceRow> Performance { get; set; } = new();
        public List<PopularRoutineRow> PopularRoutines { get; set; } = new();
        public List<(string Title, int Count)> PopularPlans { get; set; } = new();
        public EngagementReport Engagement { get; set; } = new();
        public List<(string Id, string Name)> Trainers { get; set; } = new();
    }
}
