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
    /// <summary>
    /// Appointment booking workflow:
    /// Client requests (Pending) -> Trainer approves / rejects / reschedules ->
    /// either side may cancel -> Trainer marks Completed after the session.
    /// </summary>
    public class AppointmentsController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;
        private readonly IActivityLogger _activity;

        public AppointmentsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
                                      INotificationService notifications, IActivityLogger activity)
        {
            _db = db;
            _userManager = userManager;
            _notifications = notifications;
            _activity = activity;
        }

        public async Task<IActionResult> Index(string view = "upcoming")
        {
            var query = _db.Appointments.Include(a => a.Client).Include(a => a.Trainer).AsQueryable();
            if (IsClient) query = query.Where(a => a.ClientId == CurrentUserId);
            else if (IsTrainer) query = query.Where(a => a.TrainerId == CurrentUserId);

            var now = DateTime.Now;
            query = view switch
            {
                "pending" => query.Where(a => a.Status == AppointmentStatus.Pending),
                "past" => query.Where(a => a.StartTime < now || a.Status == AppointmentStatus.Completed
                                           || a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.Rejected),
                "all" => query,
                _ => query.Where(a => a.StartTime >= now && (a.Status == AppointmentStatus.Pending
                                      || a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled))
            };

            var list = view == "upcoming" || view == "pending"
                ? await query.OrderBy(a => a.StartTime).ToListAsync()
                : await query.OrderByDescending(a => a.StartTime).ToListAsync();
            ViewBag.View = view;
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var a = await LoadAsync(id);
            if (a == null) return NotFound();
            if (!CanView(a)) return Forbid();
            return View(a);
        }

        // ------------------------------------------------------------ Client books
        [Authorize(Roles = Roles.Client), HttpGet]
        public async Task<IActionResult> Create(string? trainerId)
        {
            var me = await _db.Users.FirstAsync(u => u.Id == CurrentUserId);
            var form = new AppointmentForm { TrainerId = trainerId ?? me.AssignedTrainerId ?? string.Empty };
            await LoadTrainerOptionsAsync(form);
            return View(form);
        }

        [Authorize(Roles = Roles.Client), HttpPost]
        public async Task<IActionResult> Create(AppointmentForm form)
        {
            var trainer = await _userManager.FindByIdAsync(form.TrainerId ?? string.Empty);
            if (trainer == null || !trainer.IsActive || !await _userManager.IsInRoleAsync(trainer, Roles.Trainer))
                ModelState.AddModelError(nameof(form.TrainerId), "Please choose a valid trainer or professional.");

            if (ModelState.IsValid)
            {
                var end = form.StartTime.AddMinutes(form.DurationMinutes);
                if (await HasClashAsync(a => a.TrainerId == form.TrainerId, form.StartTime, end, null))
                    ModelState.AddModelError(nameof(form.StartTime), "The professional already has an appointment at that time. Please choose another slot.");
                else if (await HasClashAsync(a => a.ClientId == CurrentUserId, form.StartTime, end, null))
                    ModelState.AddModelError(nameof(form.StartTime), "You already have an appointment at that time.");
            }

            if (!ModelState.IsValid)
            {
                await LoadTrainerOptionsAsync(form);
                return View(form);
            }

            var appointment = new Appointment
            {
                ClientId = CurrentUserId,
                TrainerId = form.TrainerId!,
                Type = form.Type,
                StartTime = form.StartTime,
                DurationMinutes = form.DurationMinutes,
                ClientNotes = form.ClientNotes,
                Status = AppointmentStatus.Pending,
                CreatedAt = DateTime.Now
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();

            var me = await _db.Users.FirstAsync(u => u.Id == CurrentUserId);
            await _notifications.NotifyAsync(appointment.TrainerId, "New appointment request",
                $"{me.FullName} requested a {Display.Words(appointment.Type)} on {appointment.StartTime:ddd dd MMM HH:mm}.",
                NotificationType.AppointmentUpdate, $"/Appointments/Details/{appointment.Id}");
            await _activity.LogAsync(CurrentUserId, "Appointment requested", $"#{appointment.Id}");
            ShowSuccess("Appointment requested. You will be notified when it is approved.");
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------ Trainer actions
        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Approve(int id, string? trainerNotes)
        {
            var a = await LoadAsync(id);
            if (a == null) return NotFound();
            if (a.TrainerId != CurrentUserId) return Forbid();
            if (a.Status != AppointmentStatus.Pending)
                return Fail(id, "Only pending appointments can be approved.");
            if (a.StartTime <= DateTime.Now)
                return Fail(id, "This appointment time has already passed.");
            if (await HasClashAsync(x => x.TrainerId == a.TrainerId && x.Status != AppointmentStatus.Pending, a.StartTime, a.EndTime, a.Id))
                return Fail(id, "You already have a confirmed appointment that overlaps. Reschedule this one instead.");

            a.Status = AppointmentStatus.Approved;
            if (!string.IsNullOrWhiteSpace(trainerNotes)) a.TrainerNotes = trainerNotes.Trim();
            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await NotifyClientAsync(a, "Appointment approved", $"Your {Display.Words(a.Type)} on {a.StartTime:ddd dd MMM HH:mm} was approved.");
            ShowSuccess("Appointment approved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Reject(int id, string? trainerNotes)
        {
            var a = await LoadAsync(id);
            if (a == null) return NotFound();
            if (a.TrainerId != CurrentUserId) return Forbid();
            if (a.Status != AppointmentStatus.Pending)
                return Fail(id, "Only pending appointments can be rejected.");

            a.Status = AppointmentStatus.Rejected;
            a.TrainerNotes = string.IsNullOrWhiteSpace(trainerNotes) ? a.TrainerNotes : trainerNotes.Trim();
            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await NotifyClientAsync(a, "Appointment declined",
                $"Your request for {a.StartTime:ddd dd MMM HH:mm} was declined. {a.TrainerNotes}");
            ShowSuccess("Appointment rejected.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = Roles.Trainer), HttpGet]
        public async Task<IActionResult> Reschedule(int id)
        {
            var a = await LoadAsync(id);
            if (a == null) return NotFound();
            if (a.TrainerId != CurrentUserId) return Forbid();
            if (!a.IsOpen) return Fail(id, "Closed appointments cannot be changed.");
            return View(new RescheduleForm { Id = a.Id, StartTime = a.StartTime, DurationMinutes = a.DurationMinutes, TrainerNotes = a.TrainerNotes, Original = a });
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Reschedule(RescheduleForm form)
        {
            var a = await LoadAsync(form.Id);
            if (a == null) return NotFound();
            if (a.TrainerId != CurrentUserId) return Forbid();
            if (!a.IsOpen) return Fail(form.Id, "Closed appointments cannot be changed.");

            var end = form.StartTime.AddMinutes(form.DurationMinutes);
            if (ModelState.IsValid)
            {
                if (await HasClashAsync(x => x.TrainerId == a.TrainerId, form.StartTime, end, a.Id))
                    ModelState.AddModelError(nameof(form.StartTime), "You already have an appointment at that time.");
                else if (await HasClashAsync(x => x.ClientId == a.ClientId, form.StartTime, end, a.Id))
                    ModelState.AddModelError(nameof(form.StartTime), "The client already has an appointment at that time.");
            }
            if (!ModelState.IsValid)
            {
                form.Original = a;
                return View(form);
            }

            var oldTime = a.StartTime;
            a.StartTime = form.StartTime;
            a.DurationMinutes = form.DurationMinutes;
            a.TrainerNotes = form.TrainerNotes;
            a.Status = AppointmentStatus.Rescheduled;   // confirmed at the new time
            a.ReminderSent = false;                     // a new reminder is needed for the new time
            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await NotifyClientAsync(a, "Appointment rescheduled",
                $"Your {Display.Words(a.Type)} moved from {oldTime:ddd dd MMM HH:mm} to {a.StartTime:ddd dd MMM HH:mm}.");
            ShowSuccess("Appointment rescheduled and the client has been notified.");
            return RedirectToAction(nameof(Details), new { id = a.Id });
        }

        [Authorize(Roles = Roles.Trainer), HttpPost]
        public async Task<IActionResult> Complete(int id, string? trainerNotes)
        {
            var a = await LoadAsync(id);
            if (a == null) return NotFound();
            if (a.TrainerId != CurrentUserId) return Forbid();
            if (a.Status != AppointmentStatus.Approved && a.Status != AppointmentStatus.Rescheduled)
                return Fail(id, "Only confirmed appointments can be completed.");
            if (a.StartTime > DateTime.Now)
                return Fail(id, "An appointment can only be completed after it has started.");

            a.Status = AppointmentStatus.Completed;
            if (!string.IsNullOrWhiteSpace(trainerNotes)) a.TrainerNotes = trainerNotes.Trim();
            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _activity.LogAsync(CurrentUserId, "Appointment completed", $"#{a.Id}");
            ShowSuccess("Appointment marked as completed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // ------------------------------------------------------------ Either side cancels
        [Authorize(Roles = Roles.Client + "," + Roles.Trainer), HttpPost]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var a = await LoadAsync(id);
            if (a == null) return NotFound();
            bool isClientSide = a.ClientId == CurrentUserId;
            if (!isClientSide && a.TrainerId != CurrentUserId) return Forbid();
            if (!a.IsOpen) return Fail(id, "This appointment is already closed.");

            a.Status = AppointmentStatus.Cancelled;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                if (isClientSide) a.ClientNotes = $"{a.ClientNotes} [Cancelled: {reason.Trim()}]".Trim();
                else a.TrainerNotes = reason.Trim();
            }
            a.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            string who = isClientSide ? a.Client!.FullName : a.Trainer!.FullName;
            string otherId = isClientSide ? a.TrainerId : a.ClientId;
            await _notifications.NotifyAsync(otherId, "Appointment cancelled",
                $"{who} cancelled the {Display.Words(a.Type)} on {a.StartTime:ddd dd MMM HH:mm}.",
                NotificationType.AppointmentUpdate, $"/Appointments/Details/{a.Id}");
            await _activity.LogAsync(CurrentUserId, "Appointment cancelled", $"#{a.Id}");
            ShowSuccess("Appointment cancelled.");
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------ Helpers
        private Task<Appointment?> LoadAsync(int id) =>
            _db.Appointments.Include(a => a.Client).Include(a => a.Trainer).FirstOrDefaultAsync(a => a.Id == id);

        private bool CanView(Appointment a) => IsAdmin || a.ClientId == CurrentUserId || a.TrainerId == CurrentUserId;

        private IActionResult Fail(int id, string message)
        {
            ShowError(message);
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// True if any open appointment matching the filter overlaps [start, end).
        /// Candidates for the day are loaded and the overlap test is done in memory.
        /// </summary>
        private async Task<bool> HasClashAsync(System.Linq.Expressions.Expression<Func<Appointment, bool>> who,
                                              DateTime start, DateTime end, int? ignoreId)
        {
            var dayStart = start.Date;
            var dayEnd = dayStart.AddDays(1);
            var sameDay = await _db.Appointments.Where(who)
                .Where(a => a.StartTime >= dayStart && a.StartTime < dayEnd
                            && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled))
                .ToListAsync();
            return sameDay.Any(a => a.Id != ignoreId && a.StartTime < end && start < a.EndTime);
        }

        private Task NotifyClientAsync(Appointment a, string title, string message) =>
            _notifications.NotifyAsync(a.ClientId, title, message, NotificationType.AppointmentUpdate, $"/Appointments/Details/{a.Id}");

        private async Task LoadTrainerOptionsAsync(AppointmentForm form)
        {
            var trainers = await _userManager.GetUsersInRoleAsync(Roles.Trainer);
            form.TrainerOptions = trainers.Where(t => t.IsActive).OrderBy(t => t.FullName)
                .Select(t => new SelectListItem($"{t.FullName} - {t.Specialization ?? t.ProfessionalType?.ToString()}", t.Id, t.Id == form.TrainerId))
                .ToList();
        }
    }
}
