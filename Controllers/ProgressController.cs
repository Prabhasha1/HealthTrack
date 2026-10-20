using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>Progress charts: weight, BMI, body fat, calories in/out and weekly activity.</summary>
    public class ProgressController : BaseController
    {
        private readonly IProgressService _progress;
        private readonly ITrainerClientService _clients;

        public ProgressController(IProgressService progress, ITrainerClientService clients)
        {
            _progress = progress;
            _clients = clients;
        }

        /// <param name="clientId">Trainers/admins pass a client id; clients always see their own data.</param>
        /// <param name="days">Period to show: 7, 30, 90, 180 or 365 days.</param>
        public async Task<IActionResult> Index(string? clientId, int days = 90)
        {
            string target;
            if (IsClient) target = CurrentUserId;
            else if (string.IsNullOrEmpty(clientId)) return RedirectToDashboard();
            else if (IsTrainer && !await _clients.CanAccessClientAsync(CurrentUserId, clientId)) return Forbid();
            else target = clientId;

            try
            {
                var vm = await _progress.BuildAsync(target, days);
                return View(vm);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
