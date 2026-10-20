using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>System overview, user management, trainer assignment and audit log.</summary>
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activity;
        private readonly INotificationService _notifications;
        private readonly IReminderService _reminders;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IActivityLogger activity,
                               INotificationService notifications, IReminderService reminders)
        {
            _db = db;
            _userManager = userManager;
            _activity = activity;
            _notifications = notifications;
            _reminders = reminders;
        }

        // ------------------------------------------------------------ Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var clients = await _userManager.GetUsersInRoleAsync(Roles.Client);
            var trainers = await _userManager.GetUsersInRoleAsync(Roles.Trainer);
            var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
            var from = today.AddDays(-13);

            var logins = await _db.ActivityLogs.Where(a => a.Action == "Login" && a.Timestamp >= from)
                                  .Select(a => new { a.UserId, a.Timestamp }).ToListAsync();
            var workoutDates = await _db.WorkoutLogs.Where(w => w.Date >= from).Select(w => w.Date).ToListAsync();
            var mealDates = await _db.MealLogs.Where(m => m.Date >= from).Select(m => m.Date).ToListAsync();
            var measureDates = await _db.HealthMeasurements.Where(m => m.Date >= from).Select(m => m.Date).ToListAsync();
            var allEntryDates = workoutDates.Concat(mealDates).Concat(measureDates).ToList();

            var vm = new AdminDashboardViewModel
            {
                TotalClients = clients.Count,
                TotalTrainers = trainers.Count,
                TotalAdmins = admins.Count,
                ActiveUsersLast7Days = logins.Where(l => l.Timestamp >= today.AddDays(-6)).Select(l => l.UserId).Distinct().Count(),
                NewUsersLast30Days = clients.Count(c => c.CreatedAt >= today.AddDays(-30)),
                UnassignedClients = clients.Count(c => c.AssignedTrainerId == null),
                LogsToday = allEntryDates.Count(d => d.Date == today),
                PendingAppointments = await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending),
                RecentActivity = await _db.ActivityLogs.Include(a => a.User)
                                     .OrderByDescending(a => a.Timestamp).Take(10).ToListAsync()
            };

            for (var d = from; d <= today; d = d.AddDays(1))
            {
                vm.ActivityDays.Add(d.ToString("dd MMM"));
                vm.LoginsPerDay.Add(logins.Count(l => l.Timestamp.Date == d));
                vm.LogEntriesPerDay.Add(allEntryDates.Count(x => x.Date == d));
            }

            // Trainer workload table
            var workoutPlans = await _db.WorkoutPlans.Where(p => p.IsActive).Select(p => p.TrainerId).ToListAsync();
            var dietPlans = await _db.DietPlans.Where(p => p.IsActive).Select(p => p.TrainerId).ToListAsync();
            var upcoming = await _db.Appointments
                .Where(a => a.StartTime >= DateTime.Now && (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Rescheduled))
                .Select(a => a.TrainerId).ToListAsync();
            vm.TrainerAssignments = trainers.OrderBy(t => t.FullName).Select(t => new TrainerLoadItem
            {
                TrainerId = t.Id,
                TrainerName = t.FullName,
                Specialization = t.Specialization,
                ClientCount = clients.Count(c => c.AssignedTrainerId == t.Id),
                ActivePlans = workoutPlans.Count(x => x == t.Id) + dietPlans.Count(x => x == t.Id),
                UpcomingAppointments = upcoming.Count(x => x == t.Id)
            }).ToList();

            var statuses = await _db.Appointments.Select(a => a.Status).ToListAsync();
            vm.AppointmentsByStatus = Enum.GetValues<AppointmentStatus>()
                .ToDictionary(s => s.ToString(), s => statuses.Count(x => x == s));

            return View(vm);
        }

        // ------------------------------------------------------------ Users
        public async Task<IActionResult> Users(string? search, string? role)
        {
            var query = _db.Users.Include(u => u.AssignedTrainer).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(u => u.FullName.Contains(s) || (u.Email != null && u.Email.Contains(s)));
            }
            var users = await query.OrderBy(u => u.FullName).ToListAsync();

            var items = new List<UserListItem>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                string userRole = roles.FirstOrDefault() ?? "-";
                if (!string.IsNullOrEmpty(role) && userRole != role) continue;
                items.Add(new UserListItem
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    Role = userRole,
                    IsActive = u.IsActive,
                    IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.Now,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                    AssignedTrainerName = u.AssignedTrainer?.FullName,
                    Specialization = u.Specialization
                });
            }
            return View(new UserListViewModel { Users = items, Search = search, RoleFilter = role });
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser(string? role)
        {
            await LoadTrainerOptionsAsync();
            return View("EditUser", new UserEditViewModel { Role = role ?? Roles.Trainer });
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(UserEditViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "A password is required for a new account.");
            if (!Roles.All.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Unknown role.");
            if (!ModelState.IsValid)
            {
                await LoadTrainerOptionsAsync();
                return View("EditUser", model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = true,
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber,
                ProfessionalType = model.Role == Roles.Trainer ? model.ProfessionalType : null,
                Specialization = model.Role == Roles.Trainer ? model.Specialization : null,
                AssignedTrainerId = model.Role == Roles.Client ? NullIfEmpty(model.AssignedTrainerId) : null,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, model.Password!);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                await LoadTrainerOptionsAsync();
                return View("EditUser", model);
            }
            await _userManager.AddToRoleAsync(user, model.Role);
            await _activity.LogAsync(CurrentUserId, "User created", $"{user.Email} as {model.Role}");
            ShowSuccess($"{model.Role} account created for {user.FullName}.");
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            await LoadTrainerOptionsAsync();
            return View(new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Role = roles.FirstOrDefault() ?? Roles.Client,
                ProfessionalType = user.ProfessionalType,
                Specialization = user.Specialization,
                AssignedTrainerId = user.AssignedTrainerId,
                IsActive = user.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(UserEditViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id ?? string.Empty);
            if (user == null) return NotFound();
            if (!Roles.All.Contains(model.Role)) ModelState.AddModelError(nameof(model.Role), "Unknown role.");

            // An administrator must not lock themselves out of the system.
            bool editingSelf = user.Id == CurrentUserId;
            if (editingSelf && (model.Role != Roles.Admin || !model.IsActive))
                ModelState.AddModelError(string.Empty, "You cannot remove your own administrator role or deactivate yourself.");

            if (!ModelState.IsValid)
            {
                await LoadTrainerOptionsAsync();
                return View(model);
            }

            bool wasActive = user.IsActive;
            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber;
            user.IsActive = model.IsActive;
            user.ProfessionalType = model.Role == Roles.Trainer ? model.ProfessionalType : null;
            user.Specialization = model.Role == Roles.Trainer ? model.Specialization : null;
            user.AssignedTrainerId = model.Role == Roles.Client ? NullIfEmpty(model.AssignedTrainerId) : null;

            if (!string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.SetEmailAsync(user, model.Email.Trim());
                await _userManager.SetUserNameAsync(user, model.Email.Trim());
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                await LoadTrainerOptionsAsync();
                return View(model);
            }

            // Change role if needed
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            // Optional password reset by the admin
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var reset = await _userManager.ResetPasswordAsync(user, token, model.Password);
                if (!reset.Succeeded)
                {
                    foreach (var e in reset.Errors) ModelState.AddModelError(nameof(model.Password), e.Description);
                    await LoadTrainerOptionsAsync();
                    return View(model);
                }
            }

            // Changing the security stamp signs a deactivated user out within a minute.
            if (wasActive && !user.IsActive) await _userManager.UpdateSecurityStampAsync(user);

            await _activity.LogAsync(CurrentUserId, "User updated", user.Email);
            ShowSuccess("User updated.");
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            if (user.Id == CurrentUserId)
            {
                ShowError("You cannot deactivate your own account.");
                return RedirectToAction(nameof(Users));
            }
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            if (!user.IsActive) await _userManager.UpdateSecurityStampAsync(user);
            await _activity.LogAsync(CurrentUserId, user.IsActive ? "User activated" : "User deactivated", user.Email);
            ShowSuccess($"{user.FullName} is now {(user.IsActive ? "active" : "deactivated")}.");
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _activity.LogAsync(CurrentUserId, "User unlocked", user.Email);
            ShowSuccess($"{user.FullName} has been unlocked.");
            return RedirectToAction(nameof(Users));
        }

        // ------------------------------------------------------------ Trainer assignment
        [HttpGet]
        public async Task<IActionResult> AssignTrainer(string id)
        {
            var client = await _userManager.FindByIdAsync(id);
            if (client == null || !await _userManager.IsInRoleAsync(client, Roles.Client)) return NotFound();
            await LoadTrainerOptionsAsync();
            return View(new AssignTrainerViewModel { ClientId = client.Id, ClientName = client.FullName, TrainerId = client.AssignedTrainerId });
        }

        [HttpPost]
        public async Task<IActionResult> AssignTrainer(AssignTrainerViewModel model)
        {
            var client = await _userManager.FindByIdAsync(model.ClientId);
            if (client == null) return NotFound();

            string? trainerId = NullIfEmpty(model.TrainerId);
            if (trainerId != null)
            {
                var trainer = await _userManager.FindByIdAsync(trainerId);
                if (trainer == null || !await _userManager.IsInRoleAsync(trainer, Roles.Trainer))
                {
                    ShowError("Please choose a valid trainer.");
                    return RedirectToAction(nameof(AssignTrainer), new { id = model.ClientId });
                }
                await _notifications.NotifyAsync(trainer.Id, "New client assigned",
                    $"{client.FullName} has been assigned to you.", NotificationType.General, $"/Trainer/ClientDetail/{client.Id}");
                await _notifications.NotifyAsync(client.Id, "Trainer assigned",
                    $"{trainer.FullName} is now your trainer.", NotificationType.General, "/Client/Dashboard");
            }

            client.AssignedTrainerId = trainerId;
            await _userManager.UpdateAsync(client);
            await _activity.LogAsync(CurrentUserId, "Trainer assigned", $"{client.Email} -> {trainerId ?? "none"}");
            ShowSuccess("Trainer assignment saved.");
            return RedirectToAction(nameof(Users), new { role = Roles.Client });
        }

        // ------------------------------------------------------------ Audit log
        public async Task<IActionResult> ActivityLog(string? filter, int page = 1)
        {
            const int pageSize = 25;
            var query = _db.ActivityLogs.Include(a => a.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter)) query = query.Where(a => a.Action == filter);

            int total = await query.CountAsync();
            page = Math.Max(1, page);
            var logs = await query.OrderByDescending(a => a.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            ViewBag.Filter = filter;
            ViewBag.Actions = await _db.ActivityLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
            return View(logs);
        }

        /// <summary>Runs the reminder check immediately (useful for demonstrating notifications).</summary>
        [HttpPost]
        public async Task<IActionResult> RunReminders()
        {
            int created = await _reminders.GenerateRemindersAsync(DateTime.Now);
            ShowSuccess($"Reminder check complete: {created} reminder(s) created.");
            return RedirectToAction(nameof(Dashboard));
        }

        private async Task LoadTrainerOptionsAsync()
        {
            var trainers = await _userManager.GetUsersInRoleAsync(Roles.Trainer);
            ViewBag.Trainers = trainers.Where(t => t.IsActive).OrderBy(t => t.FullName)
                .Select(t => new SelectListItem($"{t.FullName} ({t.Specialization ?? t.ProfessionalType?.ToString()})", t.Id))
                .ToList();
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
