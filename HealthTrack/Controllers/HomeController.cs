using System.Diagnostics;
using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Signed-in users go straight to their own dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
                if (User.IsInRole("Trainer")) return RedirectToAction("Dashboard", "Trainer");
                if (User.IsInRole("Client")) return RedirectToAction("Dashboard", "Client");
            }
            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
