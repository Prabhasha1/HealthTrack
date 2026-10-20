using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>Clients record the workouts they performed.</summary>
    [Authorize(Roles = Roles.Client)]
    public class WorkoutLogsController : BaseController
    {
        private readonly ApplicationDbContext _db;

        public WorkoutLogsController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var start = (from ?? DateTime.Today.AddDays(-30)).Date;
            var end = (to ?? DateTime.Today).Date;
            var logs = await _db.WorkoutLogs.Include(l => l.WorkoutPlan)
                .Where(l => l.ClientId == CurrentUserId && l.Date >= start && l.Date <= end)
                .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id)
                .ToListAsync();
            ViewBag.From = start;
            ViewBag.To = end;
            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> Create(string? exercise, int? minutes, Intensity? intensity)
        {
            await LoadPlanOptionsAsync();
            return View("Edit", new WorkoutLog
            {
                Date = DateTime.Today,
                ExerciseName = exercise ?? string.Empty,
                DurationMinutes = minutes ?? 30,
                Intensity = intensity ?? Intensity.Moderate
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind("ExerciseName,Date,DurationMinutes,CaloriesBurned,Intensity,Notes,WorkoutPlanId")] WorkoutLog log)
        {
            log.ClientId = CurrentUserId;
            ModelState.Remove(nameof(WorkoutLog.ClientId));
            await ValidateAsync(log);
            if (!ModelState.IsValid)
            {
                await LoadPlanOptionsAsync();
                return View("Edit", log);
            }

            await FillCaloriesAsync(log);
            _db.WorkoutLogs.Add(log);
            await _db.SaveChangesAsync();
            ShowSuccess($"Workout logged: {log.ExerciseName}, {log.DurationMinutes} min, {log.CaloriesBurned} kcal.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var log = await _db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == CurrentUserId);
            if (log == null) return NotFound();
            await LoadPlanOptionsAsync();
            return View(log);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [Bind("ExerciseName,Date,DurationMinutes,CaloriesBurned,Intensity,Notes,WorkoutPlanId")] WorkoutLog input)
        {
            // Load the record by id AND owner so one client can never edit another's data.
            var log = await _db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == CurrentUserId);
            if (log == null) return NotFound();
            ModelState.Remove(nameof(WorkoutLog.ClientId));
            input.ClientId = CurrentUserId;
            await ValidateAsync(input);
            if (!ModelState.IsValid)
            {
                input.Id = id;
                await LoadPlanOptionsAsync();
                return View(input);
            }

            log.ExerciseName = input.ExerciseName.Trim();
            log.Date = input.Date.Date;
            log.DurationMinutes = input.DurationMinutes;
            log.Intensity = input.Intensity;
            log.CaloriesBurned = input.CaloriesBurned;
            log.Notes = input.Notes;
            log.WorkoutPlanId = input.WorkoutPlanId;
            await FillCaloriesAsync(log);
            await _db.SaveChangesAsync();
            ShowSuccess("Workout updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var log = await _db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == CurrentUserId);
            if (log == null) return NotFound();
            _db.WorkoutLogs.Remove(log);
            await _db.SaveChangesAsync();
            ShowSuccess("Workout deleted.");
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateAsync(WorkoutLog log)
        {
            if (log.Date.Date > DateTime.Today)
                ModelState.AddModelError(nameof(log.Date), "You cannot log a workout in the future.");
            if (log.WorkoutPlanId.HasValue &&
                !await _db.WorkoutPlans.AnyAsync(p => p.Id == log.WorkoutPlanId && p.ClientId == CurrentUserId))
                ModelState.AddModelError(nameof(log.WorkoutPlanId), "Invalid plan.");
        }

        /// <summary>If the client left calories blank, estimate them from duration, intensity and latest weight.</summary>
        private async Task FillCaloriesAsync(WorkoutLog log)
        {
            if (log.CaloriesBurned > 0) return;
            double weight = await _db.HealthMeasurements.Where(m => m.ClientId == CurrentUserId)
                                     .OrderByDescending(m => m.Date).Select(m => m.WeightKg).FirstOrDefaultAsync();
            log.CaloriesBurned = HealthCalculator.EstimateCaloriesBurned(log.DurationMinutes, log.Intensity, weight);
        }

        private async Task LoadPlanOptionsAsync()
        {
            var plans = await _db.WorkoutPlans.Include(p => p.Exercises)
                .Where(p => p.ClientId == CurrentUserId && p.IsActive).ToListAsync();
            ViewBag.Plans = plans.Select(p => new SelectListItem(p.Title, p.Id.ToString())).ToList();
            // Today's planned exercises are offered as one-click shortcuts.
            ViewBag.TodaysExercises = plans.SelectMany(p => p.Exercises.Where(e => e.Day == DateTime.Today.DayOfWeek)
                                                                       .Select(e => (Plan: p, Exercise: e))).ToList();
        }
    }
}
