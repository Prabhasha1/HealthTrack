using System.Globalization;
using System.Text;
using HealthTrack.Models;
using HealthTrack.Services;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Client performance, popular routines and engagement reports, viewable on screen,
    /// printable, and exportable to CSV (opens in Excel).
    /// </summary>
    [Authorize(Roles = Roles.Trainer + "," + Roles.Admin)]
    public class ReportsController : BaseController
    {
        private readonly IReportService _reports;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activity;

        public ReportsController(IReportService reports, UserManager<ApplicationUser> userManager, IActivityLogger activity)
        {
            _reports = reports;
            _userManager = userManager;
            _activity = activity;
        }

        public async Task<IActionResult> Index(string type = "performance", DateTime? from = null, DateTime? to = null, string? trainerId = null)
        {
            var filter = BuildFilter(from, to, trainerId);
            var vm = new ReportsViewModel { Filter = filter, ReportType = type, IsAdmin = IsAdmin };

            switch (type)
            {
                case "routines":
                    vm.PopularRoutines = await _reports.PopularRoutinesAsync(filter);
                    vm.PopularPlans = await _reports.PopularPlansAsync(filter);
                    break;
                case "engagement":
                    vm.Engagement = await _reports.EngagementAsync(filter);
                    break;
                default:
                    vm.ReportType = "performance";
                    vm.Performance = await _reports.ClientPerformanceAsync(filter);
                    break;
            }

            if (IsAdmin)
            {
                var trainers = await _userManager.GetUsersInRoleAsync(Roles.Trainer);
                vm.Trainers = trainers.OrderBy(t => t.FullName).Select(t => (t.Id, t.FullName)).ToList();
            }
            return View(vm);
        }

        /// <summary>Downloads the selected report as a CSV file.</summary>
        public async Task<IActionResult> Export(string type = "performance", DateTime? from = null, DateTime? to = null, string? trainerId = null)
        {
            var filter = BuildFilter(from, to, trainerId);
            var csv = new StringBuilder();

            switch (type)
            {
                case "routines":
                    csv.AppendLine("Exercise,Times logged,Unique clients,Total minutes,Avg kcal burned,Times prescribed");
                    foreach (var r in await _reports.PopularRoutinesAsync(filter))
                        csv.AppendLine(string.Join(",", Csv(r.ExerciseName), r.TimesLogged, r.UniqueClients, r.TotalMinutes, r.AvgCaloriesBurned, r.TimesPrescribed));
                    break;

                case "engagement":
                    var e = await _reports.EngagementAsync(filter);
                    csv.AppendLine("Metric,Value");
                    csv.AppendLine($"Total clients,{e.TotalClients}");
                    csv.AppendLine($"Active clients,{e.ActiveClients}");
                    csv.AppendLine($"New registrations,{e.NewRegistrations}");
                    csv.AppendLine($"Logins,{e.TotalLogins}");
                    csv.AppendLine($"Workout logs,{e.WorkoutLogs}");
                    csv.AppendLine($"Meal logs,{e.MealLogs}");
                    csv.AppendLine($"Measurements,{e.Measurements}");
                    csv.AppendLine($"Appointments booked,{e.AppointmentsBooked}");
                    csv.AppendLine();
                    csv.AppendLine("Date,Logins,Entries");
                    for (int i = 0; i < e.Dates.Count; i++)
                        csv.AppendLine($"{Csv(e.Dates[i])},{e.LoginsPerDay[i]},{e.EntriesPerDay[i]}");
                    break;

                default:
                    type = "performance";
                    csv.AppendLine("Client,Trainer,Start weight,End weight,Change kg,BMI,Workouts,Minutes,Kcal burned,Avg kcal in/day,Appointments completed,Adherence %");
                    foreach (var r in await _reports.ClientPerformanceAsync(filter))
                        csv.AppendLine(string.Join(",", Csv(r.ClientName), Csv(r.TrainerName), Num(r.StartWeight), Num(r.EndWeight),
                            Num(r.WeightChange), Num(r.CurrentBmi), r.Workouts, r.TotalMinutes, r.CaloriesBurned,
                            r.AvgDailyCaloriesIn.ToString(CultureInfo.InvariantCulture), r.AppointmentsCompleted, r.AdherencePercent?.ToString() ?? ""));
                    break;
            }

            await _activity.LogAsync(CurrentUserId, "Report exported", type);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"healthtrack-{type}-{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.csv");
        }

        /// <summary>Trainers are always limited to their own clients; admins may choose any trainer or all.</summary>
        private ReportFilter BuildFilter(DateTime? from, DateTime? to, string? trainerId) => new()
        {
            From = (from ?? DateTime.Today.AddDays(-30)).Date,
            To = (to ?? DateTime.Today).Date,
            TrainerId = IsAdmin ? (string.IsNullOrEmpty(trainerId) ? null : trainerId) : CurrentUserId
        };

        /// <summary>Quotes a CSV field and neutralises spreadsheet formula injection.</summary>
        private static string Csv(string value)
        {
            if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Num(double? value) => value?.ToString("0.0", CultureInfo.InvariantCulture) ?? "";
    }
}
