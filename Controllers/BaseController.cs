using System.Security.Claims;
using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>Small helpers shared by all HealthTrack controllers.</summary>
    public abstract class BaseController : Controller
    {
        /// <summary>Id of the signed-in user (from the authentication cookie).</summary>
        protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        protected bool IsAdmin => User.IsInRole(Roles.Admin);
        protected bool IsTrainer => User.IsInRole(Roles.Trainer);
        protected bool IsClient => User.IsInRole(Roles.Client);

        /// <summary>Green message shown at the top of the next page.</summary>
        protected void ShowSuccess(string message) => TempData["Success"] = message;

        /// <summary>Red message shown at the top of the next page.</summary>
        protected void ShowError(string message) => TempData["Error"] = message;

        /// <summary>Sends the user to the dashboard that matches their role.</summary>
        protected IActionResult RedirectToDashboard()
        {
            if (IsAdmin) return RedirectToAction("Dashboard", "Admin");
            if (IsTrainer) return RedirectToAction("Dashboard", "Trainer");
            return RedirectToAction("Dashboard", "Client");
        }
    }
}
