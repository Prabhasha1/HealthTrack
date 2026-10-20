using System.Text.Json;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>Registration, login/logout, profile, password and personal-data export.</summary>
    public class AccountController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IActivityLogger _activity;
        private readonly INotificationService _notifications;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
                                 ApplicationDbContext db, IActivityLogger activity, INotificationService notifications)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _activity = activity;
            _notifications = notifications;
        }

        // ------------------------------------------------------------ Login
        [AllowAnonymous, HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToDashboard();
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous, HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            // The same message is used for "unknown e-mail" and "wrong password"
            // so attackers cannot discover which accounts exist.
            const string invalid = "Invalid e-mail or password.";
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, invalid);
                return View(model);
            }
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the administrator.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.Now;
                await _userManager.UpdateAsync(user);
                await _activity.LogAsync(user.Id, "Login");

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl);

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(Roles.Admin)) return RedirectToAction("Dashboard", "Admin");
                if (roles.Contains(Roles.Trainer)) return RedirectToAction("Dashboard", "Trainer");
                return RedirectToAction("Dashboard", "Client");
            }

            if (result.IsLockedOut)
            {
                await _activity.LogAsync(user.Id, "Account locked", "Too many failed login attempts");
                ModelState.AddModelError(string.Empty, "Account locked after too many failed attempts. Try again in 15 minutes.");
            }
            else
            {
                await _activity.LogAsync(user.Id, "Login failed");
                ModelState.AddModelError(string.Empty, invalid);
            }
            return View(model);
        }

        // ------------------------------------------------------------ Register (clients only)
        [AllowAnonymous, HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToDashboard();
            return View(new RegisterViewModel());
        }

        [AllowAnonymous, HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.DateOfBirth.HasValue &&
                (model.DateOfBirth.Value > DateTime.Today.AddYears(-13) || model.DateOfBirth.Value < DateTime.Today.AddYears(-120)))
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "You must be at least 13 years old to register.");
            }
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                HeightCm = model.HeightCm,
                FitnessGoal = model.FitnessGoal,
                PrivacyConsentAt = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // Self-registration can only ever create a Client; trainers and admins are created by an admin.
            await _userManager.AddToRoleAsync(user, Roles.Client);
            await _activity.LogAsync(user.Id, "Registered");
            await _notifications.NotifyAsync(user.Id, "Welcome to HealthTrack!",
                "Start by recording your first health measurement so we can track your progress.",
                NotificationType.General, "/Measurements/Create");

            await _signInManager.SignInAsync(user, isPersistent: false);
            user.LastLoginAt = DateTime.Now;
            await _userManager.UpdateAsync(user);
            ShowSuccess("Your account has been created.");
            return RedirectToAction("Dashboard", "Client");
        }

        // ------------------------------------------------------------ Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _activity.LogAsync(CurrentUserId, "Logout");
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // ------------------------------------------------------------ Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _db.Users.Include(u => u.AssignedTrainer).FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            return View(new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                HeightCm = user.HeightCm,
                FitnessGoal = user.FitnessGoal,
                Specialization = user.Specialization,
                Bio = user.Bio,
                WorkoutRemindersEnabled = user.WorkoutRemindersEnabled,
                MealRemindersEnabled = user.MealRemindersEnabled,
                AppointmentRemindersEnabled = user.AppointmentRemindersEnabled,
                IsTrainer = IsTrainer,
                AssignedTrainerName = user.AssignedTrainer?.FullName
            });
        }

        [HttpPost]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            model.Email = user.Email ?? string.Empty;
            model.IsTrainer = IsTrainer;
            if (!ModelState.IsValid) return View(model);

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth;
            user.Gender = model.Gender;
            user.HeightCm = model.HeightCm;
            user.FitnessGoal = model.FitnessGoal;
            user.WorkoutRemindersEnabled = model.WorkoutRemindersEnabled;
            user.MealRemindersEnabled = model.MealRemindersEnabled;
            user.AppointmentRemindersEnabled = model.AppointmentRemindersEnabled;
            if (IsTrainer)
            {
                user.Specialization = model.Specialization;
                user.Bio = model.Bio;
            }

            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);   // updates the name shown in the top bar
            await _activity.LogAsync(user.Id, "Profile updated");
            ShowSuccess("Profile saved.");
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View(model);
            }
            await _signInManager.RefreshSignInAsync(user);
            await _activity.LogAsync(user.Id, "Password changed");
            ShowSuccess("Password changed.");
            return RedirectToAction(nameof(Profile));
        }

        /// <summary>
        /// Data-privacy right of access: a client downloads everything HealthTrack
        /// stores about them as a JSON file.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportMyData()
        {
            string id = CurrentUserId;
            var user = await _db.Users.FirstAsync(u => u.Id == id);
            var export = new
            {
                ExportedAt = DateTime.Now,
                Profile = new { user.FullName, user.Email, user.PhoneNumber, user.DateOfBirth, Gender = user.Gender?.ToString(),
                                user.HeightCm, user.FitnessGoal, user.CreatedAt, user.PrivacyConsentAt },
                Measurements = await _db.HealthMeasurements.Where(m => m.ClientId == id).OrderBy(m => m.Date)
                    .Select(m => new { m.Date, m.WeightKg, m.HeightCm, m.Bmi, m.BodyFatPercent, m.RestingHeartRate, m.SystolicBp, m.DiastolicBp, m.Notes }).ToListAsync(),
                Workouts = await _db.WorkoutLogs.Where(w => w.ClientId == id).OrderBy(w => w.Date)
                    .Select(w => new { w.Date, w.ExerciseName, w.DurationMinutes, w.CaloriesBurned, Intensity = w.Intensity.ToString(), w.Notes }).ToListAsync(),
                Meals = await _db.MealLogs.Where(m => m.ClientId == id).OrderBy(m => m.Date)
                    .Select(m => new { m.Date, MealType = m.MealType.ToString(), m.Description, m.Calories, m.ProteinGrams, m.CarbsGrams, m.FatGrams }).ToListAsync(),
                Appointments = await _db.Appointments.Where(a => a.ClientId == id).OrderBy(a => a.StartTime)
                    .Select(a => new { a.StartTime, a.DurationMinutes, Type = a.Type.ToString(), Status = a.Status.ToString(), a.ClientNotes, a.TrainerNotes }).ToListAsync()
            };

            await _activity.LogAsync(id, "Personal data exported");
            var json = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
            return File(json, "application/json", $"healthtrack-my-data-{DateTime.Today:yyyyMMdd}.json");
        }
    }
}
