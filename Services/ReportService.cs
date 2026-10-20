using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>Report generation for trainers and administrators.</summary>
    public interface IReportService
    {
        Task<List<ClientPerformanceRow>> ClientPerformanceAsync(ReportFilter filter);
        Task<List<PopularRoutineRow>> PopularRoutinesAsync(ReportFilter filter);
        Task<List<(string Title, int Count)>> PopularPlansAsync(ReportFilter filter);
        Task<EngagementReport> EngagementAsync(ReportFilter filter);
    }

    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public ReportService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        /// <summary>Clients in scope: all clients, or only those assigned to the given trainer.</summary>
        private async Task<List<ApplicationUser>> ClientsInScopeAsync(string? trainerId)
        {
            var clients = await _users.GetUsersInRoleAsync(Roles.Client);
            return clients.Where(c => trainerId == null || c.AssignedTrainerId == trainerId)
                          .OrderBy(c => c.FullName)
                          .ToList();
        }

        private static (DateTime From, DateTime ToExclusive) Range(ReportFilter f)
        {
            var from = f.From.Date;
            var to = f.To.Date;
            if (to < from) (from, to) = (to, from);
            return (from, to.AddDays(1));
        }

        public async Task<List<ClientPerformanceRow>> ClientPerformanceAsync(ReportFilter filter)
        {
            var (from, toEx) = Range(filter);
            var clients = await ClientsInScopeAsync(filter.TrainerId);
            var ids = clients.Select(c => c.Id).ToList();

            var trainerNames = (await _users.GetUsersInRoleAsync(Roles.Trainer)).ToDictionary(t => t.Id, t => t.FullName);

            var workouts = await _db.WorkoutLogs.Where(w => ids.Contains(w.ClientId) && w.Date >= from && w.Date < toEx).ToListAsync();
            var meals = await _db.MealLogs.Where(m => ids.Contains(m.ClientId) && m.Date >= from && m.Date < toEx).ToListAsync();
            var measurements = await _db.HealthMeasurements.Where(m => ids.Contains(m.ClientId) && m.Date >= from && m.Date < toEx)
                                        .OrderBy(m => m.Date).ToListAsync();
            var completed = await _db.Appointments.Where(a => ids.Contains(a.ClientId) && a.Status == AppointmentStatus.Completed
                                                             && a.StartTime >= from && a.StartTime < toEx).ToListAsync();
            var plans = await _db.WorkoutPlans.Include(p => p.Exercises)
                                 .Where(p => ids.Contains(p.ClientId) && p.IsActive).ToListAsync();

            var rows = new List<ClientPerformanceRow>();
            foreach (var c in clients)
            {
                var w = workouts.Where(x => x.ClientId == c.Id).ToList();
                var m = measurements.Where(x => x.ClientId == c.Id).ToList();
                var mealDays = meals.Where(x => x.ClientId == c.Id).GroupBy(x => x.Date.Date).ToList();

                rows.Add(new ClientPerformanceRow
                {
                    ClientId = c.Id,
                    ClientName = c.FullName,
                    TrainerName = c.AssignedTrainerId != null && trainerNames.TryGetValue(c.AssignedTrainerId, out var tn) ? tn : "-",
                    StartWeight = m.FirstOrDefault()?.WeightKg,
                    EndWeight = m.LastOrDefault()?.WeightKg,
                    CurrentBmi = m.LastOrDefault()?.Bmi,
                    Workouts = w.Count,
                    TotalMinutes = w.Sum(x => x.DurationMinutes),
                    CaloriesBurned = w.Sum(x => x.CaloriesBurned),
                    AvgDailyCaloriesIn = mealDays.Count == 0 ? 0 : Math.Round(mealDays.Average(g => g.Sum(x => x.Calories)), 0),
                    AppointmentsCompleted = completed.Count(a => a.ClientId == c.Id),
                    AdherencePercent = CalculateAdherence(plans.Where(p => p.ClientId == c.Id), w, from, toEx)
                });
            }
            return rows;
        }

        /// <summary>
        /// Adherence = days with at least one logged workout / days the active plan(s)
        /// scheduled training, within the report period. Null when nothing was scheduled.
        /// </summary>
        public static int? CalculateAdherence(IEnumerable<WorkoutPlan> plans, List<WorkoutLog> logs, DateTime from, DateTime toExclusive)
        {
            var scheduledDates = new HashSet<DateTime>();
            var end = toExclusive > DateTime.Today.AddDays(1) ? DateTime.Today.AddDays(1) : toExclusive;
            foreach (var plan in plans)
            {
                var days = plan.Exercises.Select(e => e.Day).ToHashSet();
                for (var d = from; d < end; d = d.AddDays(1))
                {
                    bool inPlan = d >= plan.StartDate.Date && (plan.EndDate == null || d <= plan.EndDate.Value.Date);
                    if (inPlan && days.Contains(d.DayOfWeek)) scheduledDates.Add(d);
                }
            }
            if (scheduledDates.Count == 0) return null;
            int done = logs.Select(l => l.Date.Date).Distinct().Count(d => scheduledDates.Contains(d));
            return (int)Math.Round(100.0 * done / scheduledDates.Count);
        }

        public async Task<List<PopularRoutineRow>> PopularRoutinesAsync(ReportFilter filter)
        {
            var (from, toEx) = Range(filter);
            var ids = (await ClientsInScopeAsync(filter.TrainerId)).Select(c => c.Id).ToList();

            var logs = await _db.WorkoutLogs.Where(w => ids.Contains(w.ClientId) && w.Date >= from && w.Date < toEx).ToListAsync();
            var prescribed = await _db.WorkoutExercises
                .Where(e => ids.Contains(e.WorkoutPlan!.ClientId))
                .Select(e => e.Name)
                .ToListAsync();
            var prescribedCounts = prescribed.GroupBy(n => n.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count());

            return logs.GroupBy(l => l.ExerciseName.Trim().ToLowerInvariant())
                .Select(g => new PopularRoutineRow
                {
                    ExerciseName = g.First().ExerciseName.Trim(),
                    TimesLogged = g.Count(),
                    UniqueClients = g.Select(x => x.ClientId).Distinct().Count(),
                    TotalMinutes = g.Sum(x => x.DurationMinutes),
                    AvgCaloriesBurned = (int)Math.Round(g.Average(x => x.CaloriesBurned)),
                    TimesPrescribed = prescribedCounts.GetValueOrDefault(g.Key)
                })
                .OrderByDescending(r => r.TimesLogged)
                .ThenBy(r => r.ExerciseName)
                .ToList();
        }

        public async Task<List<(string Title, int Count)>> PopularPlansAsync(ReportFilter filter)
        {
            var (from, toEx) = Range(filter);
            var ids = (await ClientsInScopeAsync(filter.TrainerId)).Select(c => c.Id).ToList();
            var logs = await _db.WorkoutLogs.Include(l => l.WorkoutPlan)
                .Where(w => ids.Contains(w.ClientId) && w.Date >= from && w.Date < toEx && w.WorkoutPlanId != null)
                .ToListAsync();
            return logs.GroupBy(l => l.WorkoutPlan!.Title)
                       .Select(g => (g.Key, g.Count()))
                       .OrderByDescending(x => x.Item2)
                       .Take(10)
                       .ToList();
        }

        public async Task<EngagementReport> EngagementAsync(ReportFilter filter)
        {
            var (from, toEx) = Range(filter);
            var clients = await ClientsInScopeAsync(filter.TrainerId);
            var ids = clients.Select(c => c.Id).ToList();

            var workouts = await _db.WorkoutLogs.Where(w => ids.Contains(w.ClientId) && w.Date >= from && w.Date < toEx)
                                    .Select(w => new { w.ClientId, w.Date }).ToListAsync();
            var meals = await _db.MealLogs.Where(m => ids.Contains(m.ClientId) && m.Date >= from && m.Date < toEx)
                                 .Select(m => new { m.ClientId, m.Date }).ToListAsync();
            var measures = await _db.HealthMeasurements.Where(m => ids.Contains(m.ClientId) && m.Date >= from && m.Date < toEx)
                                    .Select(m => new { m.ClientId, m.Date }).ToListAsync();
            var logins = await _db.ActivityLogs.Where(a => a.Action == "Login" && a.UserId != null && ids.Contains(a.UserId)
                                                       && a.Timestamp >= from && a.Timestamp < toEx)
                                  .Select(a => new { a.UserId, a.Timestamp }).ToListAsync();
            int booked = await _db.Appointments.CountAsync(a => ids.Contains(a.ClientId) && a.CreatedAt >= from && a.CreatedAt < toEx);

            // Every entry the client made, regardless of type.
            var entries = workouts.Select(x => (x.ClientId, x.Date))
                .Concat(meals.Select(x => (x.ClientId, x.Date)))
                .Concat(measures.Select(x => (x.ClientId, x.Date)))
                .ToList();

            var activeIds = entries.Select(e => e.ClientId).Concat(logins.Select(l => l.UserId!)).Distinct().ToHashSet();

            var report = new EngagementReport
            {
                TotalClients = clients.Count,
                ActiveClients = activeIds.Count,
                NewRegistrations = clients.Count(c => c.CreatedAt >= from && c.CreatedAt < toEx),
                TotalLogins = logins.Count,
                WorkoutLogs = workouts.Count,
                MealLogs = meals.Count,
                Measurements = measures.Count,
                AppointmentsBooked = booked,
                LogsPerActiveClient = activeIds.Count == 0 ? 0 : Math.Round((double)entries.Count / activeIds.Count, 1)
            };

            var entriesByDay = entries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Count());
            var loginsByDay = logins.GroupBy(l => l.Timestamp.Date).ToDictionary(g => g.Key, g => g.Count());
            for (var d = from; d < toEx; d = d.AddDays(1))
            {
                report.Dates.Add(d.ToString("dd MMM"));
                report.EntriesPerDay.Add(entriesByDay.GetValueOrDefault(d));
                report.LoginsPerDay.Add(loginsByDay.GetValueOrDefault(d));
            }

            report.MostEngaged = clients
                .Select(c => (c.FullName, entries.Count(e => e.ClientId == c.Id), c.LastLoginAt))
                .OrderByDescending(x => x.Item2)
                .Take(10)
                .ToList();
            return report;
        }
    }
}
