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
    /// <summary>Trainers/nutritionists create diet schedules; clients view theirs.</summary>
    public class DietPlansController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly ITrainerClientService _clients;
        private readonly INotificationService _notifications;
        private readonly IActivityLogger _activity;

        public DietPlansController(ApplicationDbContext db, ITrainerClientService clients,
                                   INotificationService notifications, IActivityLogger activity)
        {
            _db = db;
            _clients = clients;
            _notifications = notifications;
            _activity = activity;
        }

        public async Task<IActionResult> Index(string? clientId)
        {
            var query = _db.DietPlans.Include(p => p.Client).Include(p => p.Trainer).Include(p => p.Meals).AsQueryable();
            if (IsClient) query = query.Where(p => p.ClientId == CurrentUserId);
            else if (IsTrainer) query = query.Where(p => p.TrainerId == CurrentUserId);
            if (!string.IsNullOrEmpty(clientId)) query = query.Where(p => p.ClientId == clientId);

            return View(await query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var plan = await _db.DietPlans.Include(p => p.Client).Include(p => p.Trainer).Include(p => p.Meals)
                                .FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (!(IsAdmin || plan.ClientId == CurrentUserId || plan.TrainerId == CurrentUserId)) return Forbid();
            return View(plan);
        }

        [Authorize(Roles = Roles.Trainer), HttpGet]
        public async Task<IActionResult> Create(string? clientId)
        {
            var form = new DietPlanForm { ClientId = clientId ?? string.Empty };
            // Start with a sensible five-meal template the trainer can edit.
            form.Meals.Add(new MealInput { MealType = MealType.Breakfast, Time = new TimeSpan(7, 0, 0) });
            form.Meals.Add(new MealInput { MealType = MealType.Lunch, Time = new TimeSpan(12, 30, 0) });
            form.Meals.Add(new MealInput { MealType = MealType.Dinner, Time = new TimeSpan(19, 0, 0) });
            await LoadClientOptionsAsync(form);
            return View("Edit", form);
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Create(DietPlanForm form)
        {
            if (!await _clients.CanAccessClientAsync(CurrentUserId, form.ClientId))
                ModelState.AddModelError(nameof(form.ClientId), "You can only create plans for your own clients.");
            if (!ModelState.IsValid)
            {
                await LoadClientOptionsAsync(form);
                return View("Edit", form);
            }

            var plan = new DietPlan { TrainerId = CurrentUserId, CreatedAt = DateTime.Now };
            CopyFormToPlan(form, plan);
            _db.DietPlans.Add(plan);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(plan.ClientId, "New diet plan",
                $"Your diet plan \"{plan.Title}\" is ready.", NotificationType.PlanAssigned, $"/DietPlans/Details/{plan.Id}");
            await _activity.LogAsync(CurrentUserId, "Diet plan created", plan.Title);
            ShowSuccess("Diet plan created and the client has been notified.");
            return RedirectToAction(nameof(Details), new { id = plan.Id });
        }

        [Authorize(Roles = Roles.Trainer), HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var plan = await _db.DietPlans.Include(p => p.Meals).FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (plan.TrainerId != CurrentUserId) return Forbid();

            var form = new DietPlanForm
            {
                Id = plan.Id, Title = plan.Title, Description = plan.Description, ClientId = plan.ClientId,
                DailyCalorieTarget = plan.DailyCalorieTarget, StartDate = plan.StartDate, EndDate = plan.EndDate,
                IsActive = plan.IsActive,
                Meals = plan.Meals.OrderBy(m => m.Time).Select(m => new MealInput
                {
                    MealType = m.MealType, Time = m.Time, Description = m.Description, Calories = m.Calories
                }).ToList()
            };
            await LoadClientOptionsAsync(form);
            return View(form);
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Edit(int id, DietPlanForm form)
        {
            var plan = await _db.DietPlans.Include(p => p.Meals).FirstOrDefaultAsync(p => p.Id == id);
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

            _db.DietMeals.RemoveRange(plan.Meals);
            plan.Meals.Clear();
            CopyFormToPlan(form, plan);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(plan.ClientId, "Diet plan updated",
                $"\"{plan.Title}\" has been updated.", NotificationType.PlanAssigned, $"/DietPlans/Details/{plan.Id}");
            await _activity.LogAsync(CurrentUserId, "Diet plan updated", plan.Title);
            ShowSuccess("Diet plan updated.");
            return RedirectToAction(nameof(Details), new { id = plan.Id });
        }

        [Authorize(Roles = Roles.Trainer + "," + Roles.Admin), HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var plan = await _db.DietPlans.Include(p => p.Meals).FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            if (!IsAdmin && plan.TrainerId != CurrentUserId) return Forbid();

            _db.DietPlans.Remove(plan);
            await _db.SaveChangesAsync();
            await _activity.LogAsync(CurrentUserId, "Diet plan deleted", plan.Title);
            ShowSuccess("Diet plan deleted.");
            return RedirectToAction(nameof(Index));
        }

        private static void CopyFormToPlan(DietPlanForm form, DietPlan plan)
        {
            plan.Title = form.Title.Trim();
            plan.Description = form.Description;
            plan.ClientId = form.ClientId;
            plan.DailyCalorieTarget = form.DailyCalorieTarget;
            plan.StartDate = form.StartDate.Date;
            plan.EndDate = form.EndDate?.Date;
            plan.IsActive = form.IsActive;
            foreach (var m in form.Meals.Where(m => !string.IsNullOrWhiteSpace(m.Description)))
            {
                plan.Meals.Add(new DietMeal
                {
                    MealType = m.MealType, Time = m.Time, Description = m.Description!.Trim(), Calories = m.Calories
                });
            }
        }

        private async Task LoadClientOptionsAsync(DietPlanForm form)
        {
            var clients = await _clients.GetClientsAsync(CurrentUserId);
            form.ClientOptions = clients.Select(c => new SelectListItem(c.FullName, c.Id, c.Id == form.ClientId)).ToList();
        }
    }
}
