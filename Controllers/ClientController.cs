using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>Client dashboard: health stats, today's workout, diet, appointments and feedback.</summary>
    [Authorize(Roles = Roles.Client)]
    public class ClientController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public ClientController(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<IActionResult> Dashboard()
        {
            string id = CurrentUserId;
            var today = DateTime.Today;
            var weekStart = today.AddDays(-6); // rolling 7-day window

            var client = await _db.Users.Include(u => u.AssignedTrainer).FirstAsync(u => u.Id == id);
            var measurements = await _db.HealthMeasurements.Where(m => m.ClientId == id && m.Date >= today.AddDays(-30))
                                        .OrderBy(m => m.Date).ToListAsync();
            var latest = await _db.HealthMeasurements.Where(m => m.ClientId == id)
                                  .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).FirstOrDefaultAsync();
            var weekWorkouts = await _db.WorkoutLogs.Where(w => w.ClientId == id && w.Date >= weekStart).ToListAsync();

            var workoutPlan = await _db.WorkoutPlans.Include(p => p.Exercises).Include(p => p.Trainer)
                .Where(p => p.ClientId == id && p.IsActive && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync();
            var dietPlan = await _db.DietPlans.Include(p => p.Meals).Include(p => p.Trainer)
                .Where(p => p.ClientId == id && p.IsActive && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync();

            var vm = new ClientDashboardViewModel
            {
                Client = client,
                LatestMeasurement = latest,
                WeightChangeKg = measurements.Count >= 2
                    ? Math.Round(measurements.Last().WeightKg - measurements.First().WeightKg, 1) : null,
                WorkoutsThisWeek = weekWorkouts.Count,
                MinutesThisWeek = weekWorkouts.Sum(w => w.DurationMinutes),
                CaloriesBurnedThisWeek = weekWorkouts.Sum(w => w.CaloriesBurned),
                CaloriesEatenToday = await _db.MealLogs.Where(m => m.ClientId == id && m.Date == today).SumAsync(m => m.Calories),
                DailyCalorieTarget = dietPlan?.DailyCalorieTarget,
                ActiveWorkoutPlan = workoutPlan,
                TodaysExercises = workoutPlan?.Exercises.Where(e => e.Day == today.DayOfWeek).ToList() ?? new(),
                ActiveDietPlan = dietPlan,
                UpcomingAppointments = await _db.Appointments.Include(a => a.Trainer)
                    .Where(a => a.ClientId == id && a.StartTime >= DateTime.Now
                             && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled))
                    .OrderBy(a => a.StartTime).Take(5).ToListAsync(),
                RecentFeedback = await _db.TrainerFeedbacks.Include(f => f.Trainer)
                    .Where(f => f.ClientId == id).OrderByDescending(f => f.CreatedAt).Take(3).ToListAsync(),
                Notifications = await _notifications.GetRecentAsync(id, 5),
                WeightDates = measurements.Select(m => m.Date.ToString("dd MMM")).ToList(),
                WeightValues = measurements.Select(m => m.WeightKg).ToList()
            };
            return View(vm);
        }

        /// <summary>All feedback the client has received.</summary>
        public async Task<IActionResult> Feedback()
        {
            var list = await _db.TrainerFeedbacks.Include(f => f.Trainer)
                .Where(f => f.ClientId == CurrentUserId)
                .OrderByDescending(f => f.CreatedAt).ToListAsync();
            return View(list);
        }
    }
}
