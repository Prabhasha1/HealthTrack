namespace HealthTrack.ViewModels
{
    /// <summary>All series needed to draw the progress charts for one client.</summary>
    public class ProgressViewModel
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public int Days { get; set; } = 90;

        public double? StartWeight { get; set; }
        public double? CurrentWeight { get; set; }
        public double? WeightChange => (StartWeight.HasValue && CurrentWeight.HasValue)
            ? Math.Round(CurrentWeight.Value - StartWeight.Value, 1) : null;
        public double? CurrentBmi { get; set; }
        public string BmiCategory { get; set; } = "Unknown";
        public int TotalWorkouts { get; set; }
        public int TotalMinutes { get; set; }
        public int TotalCaloriesBurned { get; set; }
        public double AverageDailyCaloriesIn { get; set; }

        // Body metrics (one point per measurement)
        public List<string> MeasurementDates { get; set; } = new();
        public List<double> Weights { get; set; } = new();
        public List<double> Bmis { get; set; } = new();
        public List<double?> BodyFat { get; set; } = new();
        public List<int?> RestingHeartRates { get; set; } = new();

        // Daily energy balance
        public List<string> EnergyDates { get; set; } = new();
        public List<int> CaloriesBurnedPerDay { get; set; } = new();
        public List<int> CaloriesEatenPerDay { get; set; } = new();

        // Weekly activity
        public List<string> WeekLabels { get; set; } = new();
        public List<int> WorkoutsPerWeek { get; set; } = new();
        public List<int> MinutesPerWeek { get; set; } = new();
    }
}
