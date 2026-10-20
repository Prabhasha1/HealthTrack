using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>Generates workout, meal and appointment reminders.</summary>
    public interface IReminderService
    {
        /// <summary>Checks all schedules once and returns the number of reminders created.</summary>
        Task<int> GenerateRemindersAsync(DateTime now);
    }

    public class ReminderService : IReminderService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IConfiguration _config;

        public ReminderService(ApplicationDbContext db, INotificationService notifications, IConfiguration config)
        {
            _db = db;
            _notifications = notifications;
            _config = config;
        }

        public async Task<int> GenerateRemindersAsync(DateTime now)
        {
            int created = 0;
            created += await AppointmentRemindersAsync(now);
            created += await WorkoutRemindersAsync(now);
            created += await MealRemindersAsync(now);
            return created;
        }

        /// <summary>Approved appointments starting within the lead time get one reminder each.</summary>
        private async Task<int> AppointmentRemindersAsync(DateTime now)
        {
            int leadHours = _config.GetValue("Reminders:AppointmentLeadHours", 24);
            var limit = now.AddHours(leadHours);

            var due = await _db.Appointments
                .Include(a => a.Client)
                .Include(a => a.Trainer)
                .Where(a => !a.ReminderSent
                            && (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled)
                            && a.StartTime > now && a.StartTime <= limit)
                .ToListAsync();

            foreach (var a in due)
            {
                string when = a.StartTime.ToString("ddd dd MMM 'at' HH:mm");
                if (a.Client!.AppointmentRemindersEnabled)
                {
                    await _notifications.NotifyAsync(a.ClientId, "Upcoming appointment",
                        $"{Display.Words(a.Type)} with {a.Trainer!.FullName} on {when}.",
                        NotificationType.AppointmentReminder, $"/Appointments/Details/{a.Id}");
                }
                if (a.Trainer!.AppointmentRemindersEnabled)
                {
                    await _notifications.NotifyAsync(a.TrainerId, "Upcoming appointment",
                        $"{Display.Words(a.Type)} with {a.Client.FullName} on {when}.",
                        NotificationType.AppointmentReminder, $"/Appointments/Details/{a.Id}");
                }
                a.ReminderSent = true;
            }
            await _db.SaveChangesAsync();
            return due.Count;
        }

        /// <summary>
        /// From 06:00, a client whose active plan has exercises today and who has not yet
        /// logged a workout today gets one reminder for the day.
        /// </summary>
        private async Task<int> WorkoutRemindersAsync(DateTime now)
        {
            if (now.Hour < 6) return 0;
            var today = now.Date;
            var dayOfWeek = today.DayOfWeek;

            var plans = await _db.WorkoutPlans
                .Include(p => p.Client)
                .Include(p => p.Exercises)
                .Where(p => p.IsActive && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .ToListAsync();

            var loggedToday = (await _db.WorkoutLogs.Where(l => l.Date == today).Select(l => l.ClientId).ToListAsync()).ToHashSet();

            int count = 0;
            foreach (var plan in plans)
            {
                var todays = plan.Exercises.Where(e => e.Day == dayOfWeek).ToList();
                if (todays.Count == 0 || !plan.Client!.WorkoutRemindersEnabled || loggedToday.Contains(plan.ClientId))
                    continue;

                string list = string.Join(", ", todays.Select(e => e.Name));
                await _notifications.NotifyAsync(plan.ClientId, "Workout reminder",
                    $"Today's session from \"{plan.Title}\": {list}.",
                    NotificationType.WorkoutReminder, "/WorkoutLogs/Create",
                    dedupKey: $"workout:{plan.Id}:{today:yyyyMMdd}");
                count++;
            }
            return count;
        }

        /// <summary>A reminder is sent for each planned meal that is due within the lead time.</summary>
        private async Task<int> MealRemindersAsync(DateTime now)
        {
            int leadMinutes = _config.GetValue("Reminders:MealLeadMinutes", 60);
            var today = now.Date;
            var nowTime = now.TimeOfDay;
            var untilTime = nowTime.Add(TimeSpan.FromMinutes(leadMinutes));

            var plans = await _db.DietPlans
                .Include(p => p.Client)
                .Include(p => p.Meals)
                .Where(p => p.IsActive && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .ToListAsync();

            int count = 0;
            foreach (var plan in plans.Where(p => p.Client!.MealRemindersEnabled))
            {
                foreach (var meal in plan.Meals.Where(m => m.Time >= nowTime && m.Time <= untilTime).ToList())
                {
                    await _notifications.NotifyAsync(plan.ClientId, $"{Display.Words(meal.MealType)} at {meal.Time:hh\\:mm}",
                        $"{meal.Description} (~{meal.Calories} kcal). Remember to log it afterwards.",
                        NotificationType.MealReminder, "/MealLogs/Create",
                        dedupKey: $"meal:{meal.Id}:{today:yyyyMMdd}");
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Hosted service that runs the reminder check every few minutes for the life
    /// of the web application.
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;
        private readonly TimeSpan _interval;

        public ReminderBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration config,
                                         ILogger<ReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Reminders:IntervalMinutes", 5)));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Give the database initialiser a moment to finish on first start.
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // DbContext is scoped, so a new scope is created for every run.
                    using var scope = _scopeFactory.CreateScope();
                    var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    int created = await reminders.GenerateRemindersAsync(DateTime.Now);
                    if (created > 0) _logger.LogInformation("Reminder service created {Count} reminders", created);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reminder service failed; will retry next cycle");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
