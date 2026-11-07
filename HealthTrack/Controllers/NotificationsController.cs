using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "Client,Trainer")]
    public class NotificationsController : Controller
    {
        private static readonly string[] Types = { "Workout", "Meal", "Appointment", "System" };
        private const int MaxUpcoming = 50;

        private readonly HealthTrackContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationsController(HealthTrackContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index() => View(await BuildAsync(new ReminderInput()));

        private async Task<NotificationsPageViewModel> BuildAsync(ReminderInput input)
        {
            var userId = _userManager.GetUserId(User)!;
            var now = DateTime.Now;

            NotificationItem Map(Notification n) => new()
            {
                Id = n.NotificationId, Message = n.Message, Type = n.Type,
                When = n.ScheduledFor, IsRead = n.IsRead
            };

            var past = await _db.Notifications
                .Where(n => n.UserId == userId && n.ScheduledFor <= now)
                .OrderByDescending(n => n.ScheduledFor)
                .Take(100).ToListAsync();
            var future = await _db.Notifications
                .Where(n => n.UserId == userId && n.ScheduledFor > now)
                .OrderBy(n => n.ScheduledFor)
                .Take(MaxUpcoming).ToListAsync();

            return new NotificationsPageViewModel
            {
                Unread = past.Where(n => !n.IsRead).Select(Map).ToList(),
                Earlier = past.Where(n => n.IsRead).Select(Map).ToList(),
                Upcoming = future.Select(Map).ToList(),
                Input = input
            };
        }

        // Only the owner can mark their own notifications
        [HttpPost]
        public async Task<IActionResult> MarkRead(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId);
            if (n != null && !n.IsRead)
            {
                n.IsRead = true;
                await _db.SaveChangesAsync();
            }
            return Back(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead(string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            TempData["Toast"] = "All notifications marked as read.";
            return Back(returnUrl);
        }

        // Remove my own notifications that I have already read
        [HttpPost]
        public async Task<IActionResult> ClearRead()
        {
            var userId = _userManager.GetUserId(User)!;
            await _db.Notifications.Where(n => n.UserId == userId && n.IsRead && n.ScheduledFor <= DateTime.Now)
                .ExecuteDeleteAsync();
            TempData["Toast"] = "Read notifications cleared.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- personal reminders ----------
        [HttpPost]
        public async Task<IActionResult> AddReminder([Bind(Prefix = "Input")] ReminderInput input)
        {
            var userId = _userManager.GetUserId(User)!;
            var now = DateTime.Now;

            input.Message = (input.Message ?? "").Trim();
            if (!Types.Contains(input.Type)) ModelState.AddModelError("Input.Type", "Choose a category from the list.");
            if (input.When is DateTime w)
            {
                if (w < now.AddMinutes(1)) ModelState.AddModelError("Input.When", "Pick a time in the future.");
                else if (w > now.AddDays(365)) ModelState.AddModelError("Input.When", "Reminders can be at most a year ahead.");
            }
            var count = await _db.Notifications.CountAsync(n => n.UserId == userId && n.ScheduledFor > now);
            if (count >= MaxUpcoming) ModelState.AddModelError("Input.Message", $"You can keep up to {MaxUpcoming} upcoming reminders. Delete one first.");

            if (!ModelState.IsValid) return View("Index", await BuildAsync(input));

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = input.Message,
                Type = input.Type,
                ScheduledFor = input.When!.Value,
                IsRead = false
            });
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Reminder set.";
            return RedirectToAction(nameof(Index));
        }

        // Only my own, and only reminders that have not fired yet
        [HttpPost]
        public async Task<IActionResult> DeleteReminder(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var n = await _db.Notifications.FirstOrDefaultAsync(x =>
                x.NotificationId == id && x.UserId == userId && x.ScheduledFor > DateTime.Now);
            if (n != null)
            {
                _db.Notifications.Remove(n);
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Reminder deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        private IActionResult Back(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Index));
    }
}
