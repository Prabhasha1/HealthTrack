using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>Trainer dashboard, client progress tracking and feedback.</summary>
    [Authorize(Roles = Roles.Trainer)]
    public class TrainerController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly ITrainerClientService _clients;
        private readonly IProgressService _progress;
        private readonly INotificationService _notifications;
        private readonly IActivityLogger _activity;

        public TrainerController(ApplicationDbContext db, ITrainerClientService clients, IProgressService progress,
                                 INotificationService notifications, IActivityLogger activity)
        {
            _db = db;
            _clients = clients;
            _progress = progress;
            _notifications = notifications;
            _activity = activity;
        }

        public async Task<IActionResult> Dashboard()
        {
            string me = CurrentUserId;
            var today = DateTime.Today;
            var trainer = await _db.Users.FirstAsync(u => u.Id == me);
            var clients = await _clients.GetClientsAsync(me);
            var summaries = await BuildSummariesAsync(clients);

            var vm = new TrainerDashboardViewModel
            {
                Trainer = trainer,
                Clients = summaries,
                PendingAppointments = await _db.Appointments.Include(a => a.Client)
                    .Where(a => a.TrainerId == me && a.Status == AppointmentStatus.Pending && a.StartTime >= DateTime.Now)
                    .OrderBy(a => a.StartTime).ToListAsync(),
                TodaysAppointments = await _db.Appointments.Include(a => a.Client)
                    .Where(a => a.TrainerId == me && a.StartTime >= today && a.StartTime < today.AddDays(1)
                             && (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled))
                    .OrderBy(a => a.StartTime).ToListAsync(),
                ActiveWorkoutPlans = await _db.WorkoutPlans.CountAsync(p => p.TrainerId == me && p.IsActive),
                ActiveDietPlans = await _db.DietPlans.CountAsync(p => p.TrainerId == me && p.IsActive),
                // A client "needs attention" if they have not logged anything for 3+ days.
                ClientsNeedingAttention = summaries.Count(s => s.LastActivity == null || s.LastActivity < today.AddDays(-3)),
                ClientNames = summaries.Select(s => s.FullName.Split(' ')[0]).ToList(),
                ClientWorkoutCounts = summaries.Select(s => s.WorkoutsLast7Days).ToList()
            };
            return View(vm);
        }

        /// <summary>Calculates the progress summary line for each client in a few queries.</summary>
        private async Task<List<TrainerClientSummary>> BuildSummariesAsync(List<ApplicationUser> clients)
        {
            var ids = clients.Select(c => c.Id).ToList();
            var since30 = DateTime.Today.AddDays(-30);
            var since7 = DateTime.Today.AddDays(-6);

            var measurements = await _db.HealthMeasurements.Where(m => ids.Contains(m.ClientId)).OrderBy(m => m.Date).ToListAsync();
            var workouts = await _db.WorkoutLogs.Where(w => ids.Contains(w.ClientId) && w.Date >= since30)
                                    .Select(w => new { w.ClientId, w.Date }).ToListAsync();
            var meals = await _db.MealLogs.Where(m => ids.Contains(m.ClientId) && m.Date >= since30)
                                 .Select(m => new { m.ClientId, m.Date }).ToListAsync();
            var activeWorkoutClients = (await _db.WorkoutPlans.Where(p => ids.Contains(p.ClientId) && p.IsActive)
                                         .Select(p => p.ClientId).ToListAsync()).ToHashSet();
            var activeDietClients = (await _db.DietPlans.Where(p => ids.Contains(p.ClientId) && p.IsActive)
                                      .Select(p => p.ClientId).ToListAsync()).ToHashSet();

            var list = new List<TrainerClientSummary>();
            foreach (var c in clients)
            {
                var mine = measurements.Where(m => m.ClientId == c.Id).ToList();
                var last = mine.LastOrDefault();
                var monthAgo = mine.Where(m => m.Date >= since30).FirstOrDefault();
                var activityDates = workouts.Where(w => w.ClientId == c.Id).Select(w => w.Date)
                    .Concat(meals.Where(m => m.ClientId == c.Id).Select(m => m.Date))
                    .Concat(mine.Select(m => m.Date)).ToList();

                list.Add(new TrainerClientSummary
                {
                    ClientId = c.Id,
                    FullName = c.FullName,
                    Email = c.Email ?? string.Empty,
                    CurrentWeight = last?.WeightKg,
                    CurrentBmi = last?.Bmi,
                    WeightChange30Days = (last != null && monthAgo != null) ? Math.Round(last.WeightKg - monthAgo.WeightKg, 1) : null,
                    WorkoutsLast7Days = workouts.Count(w => w.ClientId == c.Id && w.Date >= since7),
                    LastActivity = activityDates.Count == 0 ? null : activityDates.Max(),
                    HasActiveWorkoutPlan = activeWorkoutClients.Contains(c.Id),
                    HasActiveDietPlan = activeDietClients.Contains(c.Id)
                });
            }
            return list;
        }

        /// <summary>Full view of one client: charts, plans, recent logs and feedback.</summary>
        public async Task<IActionResult> ClientDetail(string id, int days = 30)
        {
            if (!await _clients.CanAccessClientAsync(CurrentUserId, id)) return Forbid();
            var client = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (client == null) return NotFound();

            var vm = new ClientDetailViewModel
            {
                Client = client,
                Progress = await _progress.BuildAsync(id, days),
                WorkoutPlans = await _db.WorkoutPlans.Include(p => p.Trainer).Include(p => p.Exercises)
                                   .Where(p => p.ClientId == id).OrderByDescending(p => p.StartDate).ToListAsync(),
                DietPlans = await _db.DietPlans.Include(p => p.Trainer)
                                   .Where(p => p.ClientId == id).OrderByDescending(p => p.StartDate).ToListAsync(),
                RecentWorkouts = await _db.WorkoutLogs.Where(w => w.ClientId == id)
                                   .OrderByDescending(w => w.Date).Take(10).ToListAsync(),
                RecentMeals = await _db.MealLogs.Where(m => m.ClientId == id)
                                   .OrderByDescending(m => m.Date).ThenBy(m => m.MealType).Take(10).ToListAsync(),
                Feedback = await _db.TrainerFeedbacks.Include(f => f.Trainer)
                                   .Where(f => f.ClientId == id).OrderByDescending(f => f.CreatedAt).ToListAsync(),
                NewFeedback = new FeedbackInputModel { ClientId = id }
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> GiveFeedback([Bind(Prefix = "NewFeedback")] FeedbackInputModel input)
        {
            if (!await _clients.CanAccessClientAsync(CurrentUserId, input.ClientId)) return Forbid();
            if (!ModelState.IsValid)
            {
                ShowError("Feedback must be between 5 and 1000 characters.");
                return RedirectToAction(nameof(ClientDetail), new { id = input.ClientId });
            }

            _db.TrainerFeedbacks.Add(new TrainerFeedback
            {
                TrainerId = CurrentUserId,
                ClientId = input.ClientId,
                Message = input.Message.Trim(),
                Rating = input.Rating,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            var me = await _db.Users.FirstAsync(u => u.Id == CurrentUserId);
            await _notifications.NotifyAsync(input.ClientId, "New feedback", $"{me.FullName} left you feedback.",
                                             NotificationType.Feedback, "/Client/Feedback");
            await _activity.LogAsync(CurrentUserId, "Feedback given", $"Client {input.ClientId}");
            ShowSuccess("Feedback sent to the client.");
            return RedirectToAction(nameof(ClientDetail), new { id = input.ClientId });
        }
    }
}
