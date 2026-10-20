using System.Globalization;
using HealthTrack.Data;
using HealthTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>Builds the chart series used on the Progress page and the trainer's client view.</summary>
    public interface IProgressService
    {
        Task<ProgressViewModel> BuildAsync(string clientId, int days);
    }

    public class ProgressService : IProgressService
    {
        private readonly ApplicationDbContext _db;

        public ProgressService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ProgressViewModel> BuildAsync(string clientId, int days)
        {
            // Only allow a few sensible ranges so the charts stay readable.
            if (days != 7 && days != 30 && days != 90 && days != 180 && days != 365) days = 90;
            DateTime from = DateTime.Today.AddDays(-(days - 1));

            var client = await _db.Users.FirstOrDefaultAsync(u => u.Id == clientId)
                         ?? throw new KeyNotFoundException("Client not found.");

            // 1. Load the raw data for the period (one query per table).
            var measurements = await _db.HealthMeasurements
                .Where(m => m.ClientId == clientId && m.Date >= from)
                .OrderBy(m => m.Date)
                .ToListAsync();
            var workouts = await _db.WorkoutLogs
                .Where(w => w.ClientId == clientId && w.Date >= from)
                .ToListAsync();
            var meals = await _db.MealLogs
                .Where(m => m.ClientId == clientId && m.Date >= from)
                .ToListAsync();

            var vm = new ProgressViewModel
            {
                ClientId = clientId,
                ClientName = client.FullName,
                Days = days,
                TotalWorkouts = workouts.Count,
                TotalMinutes = workouts.Sum(w => w.DurationMinutes),
                TotalCaloriesBurned = workouts.Sum(w => w.CaloriesBurned)
            };

            // 2. Body metrics series.
            foreach (var m in measurements)
            {
                vm.MeasurementDates.Add(m.Date.ToString("dd MMM", CultureInfo.InvariantCulture));
                vm.Weights.Add(m.WeightKg);
                vm.Bmis.Add(m.Bmi);
                vm.BodyFat.Add(m.BodyFatPercent);
                vm.RestingHeartRates.Add(m.RestingHeartRate);
            }
            if (measurements.Count > 0)
            {
                vm.StartWeight = measurements.First().WeightKg;
                vm.CurrentWeight = measurements.Last().WeightKg;
                vm.CurrentBmi = measurements.Last().Bmi;
                vm.BmiCategory = HealthCalculator.BmiCategory(measurements.Last().Bmi);
            }

            // 3. Daily calories in vs. out (grouped in memory; at most 365 points).
            var burnedByDay = workouts.GroupBy(w => w.Date.Date).ToDictionary(g => g.Key, g => g.Sum(w => w.CaloriesBurned));
            var eatenByDay = meals.GroupBy(m => m.Date.Date).ToDictionary(g => g.Key, g => g.Sum(m => m.Calories));
            for (var day = from; day <= DateTime.Today; day = day.AddDays(1))
            {
                vm.EnergyDates.Add(day.ToString("dd MMM", CultureInfo.InvariantCulture));
                vm.CaloriesBurnedPerDay.Add(burnedByDay.GetValueOrDefault(day));
                vm.CaloriesEatenPerDay.Add(eatenByDay.GetValueOrDefault(day));
            }
            int daysWithMeals = eatenByDay.Count;
            vm.AverageDailyCaloriesIn = daysWithMeals == 0 ? 0 : Math.Round(eatenByDay.Values.Average(), 0);

            // 4. Weekly activity (weeks start on Monday).
            var weekStart = StartOfWeek(from);
            var byWeek = workouts.GroupBy(w => StartOfWeek(w.Date)).ToDictionary(g => g.Key, g => g.ToList());
            for (var week = weekStart; week <= DateTime.Today; week = week.AddDays(7))
            {
                vm.WeekLabels.Add("w/c " + week.ToString("dd MMM", CultureInfo.InvariantCulture));
                var list = byWeek.GetValueOrDefault(week);
                vm.WorkoutsPerWeek.Add(list?.Count ?? 0);
                vm.MinutesPerWeek.Add(list?.Sum(w => w.DurationMinutes) ?? 0);
            }

            return vm;
        }

        /// <summary>Returns the Monday on or before the given date.</summary>
        public static DateTime StartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }
    }
}
