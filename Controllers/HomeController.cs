using System.Diagnostics;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    [AllowAnonymous]
    public class HomeController : BaseController
    {
        /// <summary>Landing page for visitors; signed-in users go straight to their dashboard.</summary>
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToDashboard();
            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        /// <summary>Friendly page for 404, 403 etc. (wired up in Program.cs).</summary>
        [Route("Home/StatusCode")]
        public IActionResult StatusCodePage(int code)
        {
            ViewBag.Code = code;
            return View("StatusCode");
        }
    }
}
