using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>Clients record body measurements; BMI is calculated automatically.</summary>
    [Authorize(Roles = Roles.Client)]
    public class MeasurementsController : BaseController
    {
        private readonly ApplicationDbContext _db;

        public MeasurementsController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.HealthMeasurements.Where(m => m.ClientId == CurrentUserId)
                                .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToListAsync();
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var me = await _db.Users.FirstAsync(u => u.Id == CurrentUserId);
            var last = await _db.HealthMeasurements.Where(m => m.ClientId == CurrentUserId)
                                .OrderByDescending(m => m.Date).FirstOrDefaultAsync();
            return View("Edit", new HealthMeasurement
            {
                Date = DateTime.Today,
                HeightCm = me.HeightCm ?? last?.HeightCm ?? 170,
                WeightKg = last?.WeightKg ?? 0
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind("Date,WeightKg,HeightCm,BodyFatPercent,RestingHeartRate,SystolicBp,DiastolicBp,Notes")] HealthMeasurement m)
        {
            m.ClientId = CurrentUserId;
            ModelState.Remove(nameof(HealthMeasurement.ClientId));
            Validate(m);
            if (!ModelState.IsValid) return View("Edit", m);

            m.Bmi = HealthCalculator.CalculateBmi(m.WeightKg, m.HeightCm);
            _db.HealthMeasurements.Add(m);

            // Keep the profile height in step with the latest measurement.
            var me = await _db.Users.FirstAsync(u => u.Id == CurrentUserId);
            me.HeightCm = m.HeightCm;
            await _db.SaveChangesAsync();

            ShowSuccess($"Measurement saved. Your BMI is {m.Bmi} ({HealthCalculator.BmiCategory(m.Bmi)}).");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.HealthMeasurements.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == CurrentUserId);
            return m == null ? NotFound() : View(m);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [Bind("Date,WeightKg,HeightCm,BodyFatPercent,RestingHeartRate,SystolicBp,DiastolicBp,Notes")] HealthMeasurement input)
        {
            var m = await _db.HealthMeasurements.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == CurrentUserId);
            if (m == null) return NotFound();
            ModelState.Remove(nameof(HealthMeasurement.ClientId));
            Validate(input);
            if (!ModelState.IsValid)
            {
                input.Id = id;
                return View(input);
            }

            m.Date = input.Date.Date;
            m.WeightKg = input.WeightKg;
            m.HeightCm = input.HeightCm;
            m.BodyFatPercent = input.BodyFatPercent;
            m.RestingHeartRate = input.RestingHeartRate;
            m.SystolicBp = input.SystolicBp;
            m.DiastolicBp = input.DiastolicBp;
            m.Notes = input.Notes;
            m.Bmi = HealthCalculator.CalculateBmi(m.WeightKg, m.HeightCm);
            await _db.SaveChangesAsync();
            ShowSuccess("Measurement updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var m = await _db.HealthMeasurements.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == CurrentUserId);
            if (m == null) return NotFound();
            _db.HealthMeasurements.Remove(m);
            await _db.SaveChangesAsync();
            ShowSuccess("Measurement deleted.");
            return RedirectToAction(nameof(Index));
        }

        private void Validate(HealthMeasurement m)
        {
            if (m.Date.Date > DateTime.Today)
                ModelState.AddModelError(nameof(m.Date), "The date cannot be in the future.");
            if (m.SystolicBp.HasValue != m.DiastolicBp.HasValue)
                ModelState.AddModelError(nameof(m.SystolicBp), "Enter both systolic and diastolic blood pressure, or neither.");
            if (m.SystolicBp.HasValue && m.DiastolicBp.HasValue && m.DiastolicBp >= m.SystolicBp)
                ModelState.AddModelError(nameof(m.DiastolicBp), "Diastolic pressure must be lower than systolic.");
        }
    }
}
