using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>Clients record their meals; the index shows a daily food diary against the plan target.</summary>
    [Authorize(Roles = Roles.Client)]
    public class MealLogsController : BaseController
    {
        private readonly ApplicationDbContext _db;

        public MealLogsController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var day = (date ?? DateTime.Today).Date;
            var meals = await _db.MealLogs.Where(m => m.ClientId == CurrentUserId && m.Date == day)
                                 .OrderBy(m => m.MealType).ToListAsync();
            var plan = await _db.DietPlans.Include(p => p.Meals)
                .Where(p => p.ClientId == CurrentUserId && p.IsActive && p.StartDate <= day && (p.EndDate == null || p.EndDate >= day))
                .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync();

            ViewBag.Date = day;
            ViewBag.Plan = plan;
            ViewBag.Target = plan?.DailyCalorieTarget;
            return View(meals);
        }

        [HttpGet]
        public async Task<IActionResult> Create(DateTime? date, MealType? type)
        {
            var log = new MealLog { Date = (date ?? DateTime.Today).Date, MealType = type ?? GuessMealType(DateTime.Now) };
            // Pre-fill from the diet plan so the client only has to confirm.
            var planned = await _db.DietMeals.Include(m => m.DietPlan)
                .Where(m => m.DietPlan!.ClientId == CurrentUserId && m.DietPlan!.IsActive && m.MealType == log.MealType)
                .FirstOrDefaultAsync();
            if (planned != null)
            {
                log.Description = planned.Description;
                log.Calories = planned.Calories;
            }
            return View("Edit", log);
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind("Date,MealType,Description,Calories,ProteinGrams,CarbsGrams,FatGrams")] MealLog log)
        {
            log.ClientId = CurrentUserId;
            ModelState.Remove(nameof(MealLog.ClientId));
            Validate(log);
            if (!ModelState.IsValid) return View("Edit", log);

            _db.MealLogs.Add(log);
            await _db.SaveChangesAsync();
            ShowSuccess($"{log.MealType} logged ({log.Calories} kcal).");
            return RedirectToAction(nameof(Index), new { date = log.Date.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var log = await _db.MealLogs.FirstOrDefaultAsync(m => m.Id == id && m.ClientId == CurrentUserId);
            return log == null ? NotFound() : View(log);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [Bind("Date,MealType,Description,Calories,ProteinGrams,CarbsGrams,FatGrams")] MealLog input)
        {
            var log = await _db.MealLogs.FirstOrDefaultAsync(m => m.Id == id && m.ClientId == CurrentUserId);
            if (log == null) return NotFound();
            ModelState.Remove(nameof(MealLog.ClientId));
            Validate(input);
            if (!ModelState.IsValid)
            {
                input.Id = id;
                return View(input);
            }

            log.Date = input.Date.Date;
            log.MealType = input.MealType;
            log.Description = input.Description.Trim();
            log.Calories = input.Calories;
            log.ProteinGrams = input.ProteinGrams;
            log.CarbsGrams = input.CarbsGrams;
            log.FatGrams = input.FatGrams;
            await _db.SaveChangesAsync();
            ShowSuccess("Meal updated.");
            return RedirectToAction(nameof(Index), new { date = log.Date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var log = await _db.MealLogs.FirstOrDefaultAsync(m => m.Id == id && m.ClientId == CurrentUserId);
            if (log == null) return NotFound();
            var date = log.Date;
            _db.MealLogs.Remove(log);
            await _db.SaveChangesAsync();
            ShowSuccess("Meal deleted.");
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
        }

        private void Validate(MealLog log)
        {
            if (log.Date.Date > DateTime.Today)
                ModelState.AddModelError(nameof(log.Date), "You cannot log a meal in the future.");
        }

        /// <summary>Suggests the meal type from the time of day.</summary>
        public static MealType GuessMealType(DateTime now) => now.Hour switch
        {
            < 10 => MealType.Breakfast,
            < 12 => MealType.MorningSnack,
            < 15 => MealType.Lunch,
            < 18 => MealType.AfternoonSnack,
            _ => MealType.Dinner
        };
    }
}
