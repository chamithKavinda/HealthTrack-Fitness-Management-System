using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.ViewComponents
{
    public class NotificationsViewComponent : ViewComponent
    {
        private readonly HealthTrackContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationsViewComponent(HealthTrackContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <param name="mode">"rail" for the side panel, "bell" for the badge in the top bar</param>
        public async Task<IViewComponentResult> InvokeAsync(string mode = "rail")
        {
            var userId = _userManager.GetUserId(UserClaimsPrincipal);
            var model = new NotificationRailModel();
            if (userId == null) return View(mode == "bell" ? "Bell" : "Default", model);

            var now = DateTime.Now;
            // Reminders scheduled for the future are not shown until their time arrives
            var mine = _db.Notifications.Where(n => n.UserId == userId && n.ScheduledFor <= now);

            model.Unread = await mine.CountAsync(n => !n.IsRead);

            if (mode != "bell")
            {
                model.Items = await mine
                    .OrderBy(n => n.IsRead)
                    .ThenByDescending(n => n.ScheduledFor)
                    .Take(8)
                    .Select(n => new NotificationItem
                    {
                        Id = n.NotificationId, Message = n.Message, Type = n.Type,
                        When = n.ScheduledFor, IsRead = n.IsRead
                    })
                    .ToListAsync();
            }
            return View(mode == "bell" ? "Bell" : "Default", model);
        }
    }
}
