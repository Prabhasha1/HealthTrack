using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using HealthTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Trainers create and maintain personalised workout plans; clients can view theirs;
    /// administrators can view all.
    /// </summary>
    public class WorkoutPlansController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly ITrainerClientService _clients;
        private readonly INotificationService _notifications;
        private readonly IActivityLogger _activity;

        public WorkoutPlansController(ApplicationDbContext db, ITrainerClientService clients,
                                      INotificationService notifications, IActivityLogger activity)
        {
            _db = db;
            _clients = clients;
            _notifications = notifications;
            _activity = activity;
        }

        public async Task<IActionResult> Index(string? clientId)
        {
            var query = _db.WorkoutPlans.Include(p => p.Client).Include(p => p.Trainer).Include(p => p.Exercises).AsQueryable();
            if (IsClient) query = query.Where(p => p.ClientId == CurrentUserId);
            else if (IsTrainer) query = query.Where(p => p.TrainerId == CurrentUserId);
            if (!string.IsNullOrEmpty(clientId)) query = query.Where(p => p.ClientId == clientId);

            return View(await query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var plan = await _db.WorkoutPlans.Include(p => p.Client).Include(p => p.Trainer).Include(p => p.Exercises)
                                .FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (!CanView(plan)) return Forbid();
            return View(plan);
        }

        [Authorize(Roles = Roles.Trainer), HttpGet]
        public async Task<IActionResult> Create(string? clientId)
        {
            var form = new WorkoutPlanForm { ClientId = clientId ?? string.Empty };
            form.Exercises.Add(new ExerciseInput());
            await LoadClientOptionsAsync(form);
            return View("Edit", form);
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Create(WorkoutPlanForm form)
        {
            if (!await _clients.CanAccessClientAsync(CurrentUserId, form.ClientId))
                ModelState.AddModelError(nameof(form.ClientId), "You can only create plans for your own clients.");
            if (!ModelState.IsValid)
            {
                await LoadClientOptionsAsync(form);
                return View("Edit", form);
            }

            var plan = new WorkoutPlan { TrainerId = CurrentUserId, CreatedAt = DateTime.Now };
            CopyFormToPlan(form, plan);
            _db.WorkoutPlans.Add(plan);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(plan.ClientId, "New workout plan",
                $"Your trainer created the workout plan \"{plan.Title}\".", NotificationType.PlanAssigned, $"/WorkoutPlans/Details/{plan.Id}");
            await _activity.LogAsync(CurrentUserId, "Workout plan created", plan.Title);
            ShowSuccess("Workout plan created and the client has been notified.");
            return RedirectToAction(nameof(Details), new { id = plan.Id });
        }

        [Authorize(Roles = Roles.Trainer), HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var plan = await _db.WorkoutPlans.Include(p => p.Exercises).FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (plan.TrainerId != CurrentUserId) return Forbid();

            var form = new WorkoutPlanForm
            {
                Id = plan.Id, Title = plan.Title, Description = plan.Description, Goal = plan.Goal,
                ClientId = plan.ClientId, StartDate = plan.StartDate, EndDate = plan.EndDate, IsActive = plan.IsActive,
                Exercises = plan.Exercises.OrderBy(e => DayIndex(e.Day)).Select(e => new ExerciseInput
                {
                    Name = e.Name, Day = e.Day, Sets = e.Sets, Reps = e.Reps,
                    DurationMinutes = e.DurationMinutes, Intensity = e.Intensity, Notes = e.Notes
                }).ToList()
            };
            await LoadClientOptionsAsync(form);
            return View(form);
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Edit(int id, WorkoutPlanForm form)
        {
            var plan = await _db.WorkoutPlans.Include(p => p.Exercises).FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (plan.TrainerId != CurrentUserId) return Forbid();
            if (!await _clients.CanAccessClientAsync(CurrentUserId, form.ClientId))
                ModelState.AddModelError(nameof(form.ClientId), "You can only assign plans to your own clients.");
            if (!ModelState.IsValid)
            {
                form.Id = id;
                await LoadClientOptionsAsync(form);
                return View(form);
            }

            // Replace the exercise rows with the edited list.
            _db.WorkoutExercises.RemoveRange(plan.Exercises);
            plan.Exercises.Clear();
            CopyFormToPlan(form, plan);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(plan.ClientId, "Workout plan updated",
                $"\"{plan.Title}\" has been updated by your trainer.", NotificationType.PlanAssigned, $"/WorkoutPlans/Details/{plan.Id}");
            await _activity.LogAsync(CurrentUserId, "Workout plan updated", plan.Title);
            ShowSuccess("Workout plan updated.");
            return RedirectToAction(nameof(Details), new { id = plan.Id });
        }

        [Authorize(Roles = Roles.Trainer + "," + Roles.Admin), HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var plan = await _db.WorkoutPlans.Include(p => p.Exercises).FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (!IsAdmin && plan.TrainerId != CurrentUserId) return Forbid();

            _db.WorkoutPlans.Remove(plan);   // exercises are removed by cascade delete
            await _db.SaveChangesAsync();
            await _activity.LogAsync(CurrentUserId, "Workout plan deleted", plan.Title);
            ShowSuccess("Workout plan deleted.");
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Copies the posted form onto the entity, ignoring blank exercise rows.</summary>
        private static void CopyFormToPlan(WorkoutPlanForm form, WorkoutPlan plan)
        {
            plan.Title = form.Title.Trim();
            plan.Description = form.Description;
            plan.Goal = form.Goal;
            plan.ClientId = form.ClientId;
            plan.StartDate = form.StartDate.Date;
            plan.EndDate = form.EndDate?.Date;
            plan.IsActive = form.IsActive;
            foreach (var e in form.Exercises.Where(e => !string.IsNullOrWhiteSpace(e.Name)))
            {
                plan.Exercises.Add(new WorkoutExercise
                {
                    Name = e.Name!.Trim(), Day = e.Day, Sets = e.Sets, Reps = e.Reps,
                    DurationMinutes = e.DurationMinutes, Intensity = e.Intensity, Notes = e.Notes
                });
            }
        }

        private bool CanView(WorkoutPlan plan) =>
            IsAdmin || plan.ClientId == CurrentUserId || plan.TrainerId == CurrentUserId;

        private async Task LoadClientOptionsAsync(WorkoutPlanForm form)
        {
            var clients = await _clients.GetClientsAsync(CurrentUserId);
            form.ClientOptions = clients.Select(c => new SelectListItem(c.FullName, c.Id, c.Id == form.ClientId)).ToList();
        }

        /// <summary>Orders weekdays Monday..Sunday instead of Sunday..Saturday.</summary>
        public static int DayIndex(DayOfWeek day) => ((int)day + 6) % 7;
    }
}
