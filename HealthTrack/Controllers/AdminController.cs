using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private const int PageSize = 15;
        private static readonly string[] ManagedRoles = { "Client", "Trainer" };

        private readonly HealthTrackContext _db;
        private readonly UserManager<IdentityUser> _users;
        private readonly ILogger<AdminController> _log;

        public AdminController(HealthTrackContext db, UserManager<IdentityUser> users, ILogger<AdminController> log)
        {
            _db = db;
            _users = users;
            _log = log;
        }

        public async Task<IActionResult> Dashboard()
        {
            var vm = new AdminDashboardViewModel
            {
                TotalClients = await _db.ClientProfiles.CountAsync(),
                TotalTrainers = await _db.TrainerProfiles.CountAsync(),
                TotalAppointments = await _db.Appointments.CountAsync(),
                WorkoutsLogged = await _db.WorkoutLogs.CountAsync(),
                MealsLogged = await _db.MealLogs.CountAsync(),
                MetricsLogged = await _db.HealthMetrics.CountAsync(),
                UnassignedClients = await _db.ClientProfiles.CountAsync(c =>
                    !_db.TrainerAssignments.Any(a => a.ClientId == c.ClientId && a.IsActive))
            };

            vm.RecentSignups = await _db.ClientProfiles
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new PersonRow { FullName = c.FullName, Phone = c.Phone, Joined = c.CreatedAt })
                .Take(5)
                .ToListAsync();

            return View(vm);
        }

        // =====================================================================
        // User management
        // =====================================================================
        public async Task<IActionResult> Users(string? q, string? role, string? status, int page = 1)
        {
            var all = await LoadUserRowsAsync();
            var vm = new AdminUsersViewModel { Q = q?.Trim(), Role = role, Status = status, LockedCount = all.Count(u => u.IsLocked) };

            IEnumerable<AdminUserRow> rows = all;
            if (!string.IsNullOrWhiteSpace(vm.Q))
                rows = rows.Where(u => u.Name.Contains(vm.Q, StringComparison.OrdinalIgnoreCase) ||
                                       u.Email.Contains(vm.Q, StringComparison.OrdinalIgnoreCase));
            if (role is "Client" or "Trainer" or "Admin") rows = rows.Where(u => u.Role == role);
            else vm.Role = null;
            if (status == "locked") rows = rows.Where(u => u.IsLocked);
            else if (status == "active") rows = rows.Where(u => !u.IsLocked);
            else vm.Status = null;

            var list = rows.OrderBy(u => u.Role == "Admin" ? 0 : u.Role == "Trainer" ? 1 : 2).ThenBy(u => u.Name).ToList();
            vm.Total = list.Count;
            vm.Pages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
            vm.Page = Math.Clamp(page, 1, vm.Pages);
            vm.Rows = list.Skip((vm.Page - 1) * PageSize).Take(PageSize).ToList();
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> LockUser(string id, string? returnUrl)
        {
            var (user, role) = await FindManagedAsync(id);
            if (user == null) return BackToUsers(returnUrl, "That account cannot be changed here.");
            if (user.Id == _users.GetUserId(User)) return BackToUsers(returnUrl, "You cannot lock your own account.");

            await _users.SetLockoutEnabledAsync(user, true);
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _users.UpdateSecurityStampAsync(user);          // ends any signed-in session
            _log.LogInformation("Admin {Admin} locked {Role} account {User}", User.Identity?.Name, role, user.Email);
            return BackToUsers(returnUrl, $"{user.Email} is now locked.");
        }

        [HttpPost]
        public async Task<IActionResult> UnlockUser(string id, string? returnUrl)
        {
            var (user, role) = await FindManagedAsync(id);
            if (user == null) return BackToUsers(returnUrl, "That account cannot be changed here.");

            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);
            _log.LogInformation("Admin {Admin} unlocked {Role} account {User}", User.Identity?.Name, role, user.Email);
            return BackToUsers(returnUrl, $"{user.Email} is unlocked.");
        }

        public async Task<IActionResult> ResetPassword(string id)
        {
            var (user, role) = await FindManagedAsync(id);
            if (user == null) return BackToUsers(null, "That account cannot be changed here.");
            if (user.Id == _users.GetUserId(User)) return BackToUsers(null, "Change your own password from your account instead.");
            return View(await ResetVmAsync(user, role!, new ResetPasswordInput { UserId = user.Id }));
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword([Bind(Prefix = "Input")] ResetPasswordInput input)
        {
            var (user, role) = await FindManagedAsync(input.UserId);
            if (user == null) return BackToUsers(null, "That account cannot be changed here.");
            if (user.Id == _users.GetUserId(User)) return BackToUsers(null, "Change your own password from your account instead.");

            if (ModelState.IsValid)
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var result = await _users.ResetPasswordAsync(user, token, input.NewPassword);
                if (result.Succeeded)
                {
                    await _users.ResetAccessFailedCountAsync(user);
                    _log.LogInformation("Admin {Admin} reset the password of {Role} account {User}", User.Identity?.Name, role, user.Email);
                    TempData["Toast"] = $"Password reset for {user.Email}. Tell them to sign in and keep it private.";
                    return RedirectToAction(nameof(Users));
                }
                foreach (var e in result.Errors) ModelState.AddModelError("Input.NewPassword", e.Description);
            }

            input.NewPassword = ""; input.ConfirmPassword = "";
            return View(await ResetVmAsync(user, role!, input));
        }

        private async Task<ResetPasswordViewModel> ResetVmAsync(IdentityUser user, string role, ResetPasswordInput input)
        {
            var names = await NameLookupAsync();
            return new ResetPasswordViewModel
            {
                Name = names.TryGetValue(user.Id, out var n) ? n.Name : user.Email ?? "",
                Email = user.Email ?? "", Role = role, Input = input
            };
        }

        // Only Client and Trainer accounts can be managed here; admins are never touched
        private async Task<(IdentityUser? user, string? role)> FindManagedAsync(string? id)
        {
            if (string.IsNullOrEmpty(id)) return (null, null);
            var user = await _users.FindByIdAsync(id);
            if (user == null) return (null, null);
            var roles = await _users.GetRolesAsync(user);
            var role = ManagedRoles.FirstOrDefault(r => roles.Contains(r));
            return role == null || roles.Contains("Admin") ? (null, null) : (user, role);
        }

        private IActionResult BackToUsers(string? returnUrl, string toast)
        {
            TempData["Toast"] = toast;
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Users));
        }

        private async Task<Dictionary<string, (string Name, DateTime? Joined)>> NameLookupAsync()
        {
            var clients = await _db.ClientProfiles.Select(c => new { c.UserId, c.FullName, J = (DateTime?)c.CreatedAt }).ToListAsync();
            var trainers = await _db.TrainerProfiles.Select(t => new { t.UserId, t.FullName, J = (DateTime?)t.CreatedAt }).ToListAsync();
            var d = new Dictionary<string, (string, DateTime?)>();
            foreach (var c in clients) d[c.UserId] = (c.FullName, c.J);
            foreach (var t in trainers) d[t.UserId] = (t.FullName, t.J);
            return d;
        }

        private async Task<List<AdminUserRow>> LoadUserRowsAsync()
        {
            var names = await NameLookupAsync();
            var me = _users.GetUserId(User);
            var now = DateTimeOffset.UtcNow;
            var rows = new List<AdminUserRow>();
            foreach (var role in new[] { "Admin", "Trainer", "Client" })
            {
                foreach (var u in await _users.GetUsersInRoleAsync(role))
                {
                    names.TryGetValue(u.Id, out var info);
                    rows.Add(new AdminUserRow
                    {
                        Id = u.Id,
                        Name = string.IsNullOrEmpty(info.Name) ? (role == "Admin" ? "Administrator" : u.Email ?? "") : info.Name,
                        Email = u.Email ?? u.UserName ?? "",
                        Role = role,
                        IsLocked = u.LockoutEnd != null && u.LockoutEnd > now,
                        Joined = info.Joined,
                        IsSelf = u.Id == me
                    });
                }
            }
            return rows;
        }

        // =====================================================================
        // Reports
        // =====================================================================
        public async Task<IActionResult> Reports()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var vm = new AdminReportsViewModel
            {
                Clients = await _db.ClientProfiles.CountAsync(),
                Trainers = await _db.TrainerProfiles.CountAsync(),
                Appointments = await _db.Appointments.CountAsync(),
                CompletedAppointments = await _db.Appointments.CountAsync(a => a.Status == "Completed"),
                ActiveAssignments = await _db.TrainerAssignments.CountAsync(a => a.IsActive)
            };
            var chart = vm.Chart;

            // New sign-ups in eight 7-day blocks (oldest first)
            var from56 = DateTime.Today.AddDays(-55);
            var clientDates = await _db.ClientProfiles.Where(c => c.CreatedAt >= from56)
                .Select(c => (DateTime?)c.CreatedAt).ToListAsync();
            var trainerDates = await _db.TrainerProfiles.Where(t => t.CreatedAt >= from56)
                .Select(t => (DateTime?)t.CreatedAt).ToListAsync();
            for (var i = 7; i >= 0; i--)
            {
                var end = DateTime.Today.AddDays(-7 * i).AddDays(1);      // exclusive
                var start = end.AddDays(-7);
                chart.GrowthLabels.Add(end.AddDays(-1).ToString("dd MMM"));
                chart.NewClients.Add(clientDates.Count(d => d >= start && d < end));
                chart.NewTrainers.Add(trainerDates.Count(d => d >= start && d < end));
            }

            // Daily activity for the last 14 days
            var from14 = today.AddDays(-13);
            var wl = await _db.WorkoutLogs.Where(l => l.LogDate >= from14).Select(l => l.LogDate).ToListAsync();
            var ml = await _db.MealLogs.Where(l => l.LogDate >= from14).Select(l => l.LogDate).ToListAsync();
            var hm = await _db.HealthMetrics.Where(l => l.RecordedDate >= from14).Select(l => l.RecordedDate).ToListAsync();
            for (var i = 13; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                chart.ActLabels.Add(day.ToString("dd MMM"));
                chart.ActWorkouts.Add(wl.Count(d => d == day));
                chart.ActMeals.Add(ml.Count(d => d == day));
                chart.ActMetrics.Add(hm.Count(d => d == day));
            }

            // Appointment statuses
            var statuses = await _db.Appointments.GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync();
            foreach (var s in new[] { "Pending", "Approved", "Completed", "Cancelled" })
            {
                chart.StatusLabels.Add(s);
                chart.StatusCounts.Add(statuses.FirstOrDefault(x => x.Status == s)?.N ?? 0);
            }

            // Trainer workload table
            var trainers = await _db.TrainerProfiles.OrderBy(t => t.FullName)
                .Select(t => new { t.TrainerId, t.FullName, t.Specialization }).ToListAsync();
            var active = await _db.TrainerAssignments.Where(a => a.IsActive)
                .GroupBy(a => a.TrainerId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
            var wp = await _db.WorkoutPlans.GroupBy(p => p.TrainerId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
            var dp = await _db.DietPlans.GroupBy(p => p.TrainerId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
            var ap = await _db.Appointments.GroupBy(a => new { a.TrainerId, a.Status })
                .Select(g => new { g.Key.TrainerId, g.Key.Status, N = g.Count() }).ToListAsync();

            vm.Workload = trainers.Select(t =>
            {
                var total = ap.Where(a => a.TrainerId == t.TrainerId).Sum(a => a.N);
                var done = ap.Where(a => a.TrainerId == t.TrainerId && a.Status == "Completed").Sum(a => a.N);
                return new TrainerWorkloadRow
                {
                    Name = t.FullName,
                    Specialization = t.Specialization,
                    ActiveClients = active.FirstOrDefault(x => x.Id == t.TrainerId)?.N ?? 0,
                    Plans = (wp.FirstOrDefault(x => x.Id == t.TrainerId)?.N ?? 0) + (dp.FirstOrDefault(x => x.Id == t.TrainerId)?.N ?? 0),
                    Appointments = total,
                    Completed = done,
                    CompletionPct = total > 0 ? (int)Math.Round(done * 100.0 / total) : null
                };
            }).ToList();

            return View(vm);
        }

        public async Task<IActionResult> ExportAppointments()
        {
            var rows = await (from a in _db.Appointments
                              join c in _db.ClientProfiles on a.ClientId equals c.ClientId
                              join t in _db.TrainerProfiles on a.TrainerId equals t.TrainerId
                              orderby a.AppointmentDateTime descending
                              select new { a.AppointmentId, Client = c.FullName, Trainer = t.FullName,
                                           a.AppointmentDateTime, a.Status, a.Reason, a.TrainerRemarks }).ToListAsync();

            var bytes = CsvWriter.Build(
                new[] { "Appointment", "Client", "Trainer", "Date and time", "Status", "Reason", "Trainer remarks" },
                rows.Select(r => new object?[] { r.AppointmentId, r.Client, r.Trainer, r.AppointmentDateTime, r.Status, r.Reason, r.TrainerRemarks }));
            return File(bytes, "text/csv", $"appointments-{DateTime.Today:yyyyMMdd}.csv");
        }

        // Names and roles only. Medical notes and contact details are never exported.
        public async Task<IActionResult> ExportUsers()
        {
            var clients = await _db.ClientProfiles.OrderBy(c => c.FullName)
                .Select(c => new { c.FullName, Joined = (DateTime?)c.CreatedAt }).ToListAsync();
            var trainers = await _db.TrainerProfiles.OrderBy(t => t.FullName)
                .Select(t => new { t.FullName, t.Specialization, Joined = (DateTime?)t.CreatedAt }).ToListAsync();

            var rows = clients.Select(c => new object?[] { "Client", c.FullName, null, c.Joined })
                .Concat(trainers.Select(t => new object?[] { "Trainer", t.FullName, t.Specialization, t.Joined }));
            var bytes = CsvWriter.Build(new[] { "Role", "Name", "Specialization", "Joined" }, rows);
            return File(bytes, "text/csv", $"users-{DateTime.Today:yyyyMMdd}.csv");
        }

        // =====================================================================
        // Assign clients to trainers
        // =====================================================================
        public async Task<IActionResult> Assignments(bool unassignedOnly = false)
        {
            var trainers = await _db.TrainerProfiles
                .OrderBy(t => t.FullName)
                .Select(t => new TrainerOption
                {
                    Id = t.TrainerId,
                    Name = t.FullName,
                    Specialization = t.Specialization,
                    ClientCount = _db.TrainerAssignments.Count(a => a.TrainerId == t.TrainerId && a.IsActive)
                })
                .ToListAsync();

            var raw = await _db.ClientProfiles
                .OrderBy(c => c.FullName)
                .Select(c => new
                {
                    c.ClientId,
                    c.FullName,
                    c.Phone,
                    Active = _db.TrainerAssignments
                        .Where(a => a.ClientId == c.ClientId && a.IsActive)
                        .Select(a => new { a.TrainerId, a.AssignedDate })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var names = trainers.ToDictionary(t => t.Id, t => t.Name);
            var rows = raw.Select(r => new AssignmentRow
            {
                ClientId = r.ClientId,
                ClientName = r.FullName,
                Phone = r.Phone,
                TrainerId = r.Active?.TrainerId,
                TrainerName = r.Active != null && names.TryGetValue(r.Active.TrainerId, out var n) ? n : null,
                Since = r.Active?.AssignedDate
            }).ToList();

            var vm = new AssignmentsPageViewModel
            {
                Trainers = trainers,
                UnassignedCount = rows.Count(r => r.TrainerId == null),
                UnassignedOnly = unassignedOnly,
                Clients = unassignedOnly ? rows.Where(r => r.TrainerId == null).ToList() : rows
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Assign(int clientId, int trainerId, bool unassignedOnly = false)
        {
            var client = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.ClientId == clientId);
            var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(t => t.TrainerId == trainerId);
            var clientName = client?.FullName;
            var trainerName = trainer?.FullName;

            if (client == null || trainer == null)
            {
                TempData["Toast"] = "Choose a client and a trainer from the list.";
                return RedirectToAction(nameof(Assignments), new { unassignedOnly });
            }

            var rows = await _db.TrainerAssignments.Where(a => a.ClientId == clientId).ToListAsync();

            // Is this a real change? (re-assigning the same active trainer does nothing)
            var changed = !rows.Any(a => a.IsActive && a.TrainerId == trainerId);

            // A client has one active trainer at a time
            foreach (var other in rows.Where(a => a.IsActive && a.TrainerId != trainerId))
                other.IsActive = false;

            var existing = rows.FirstOrDefault(a => a.TrainerId == trainerId);
            if (existing == null)
            {
                _db.TrainerAssignments.Add(new TrainerAssignment
                {
                    ClientId = clientId,
                    TrainerId = trainerId,
                    AssignedDate = DateOnly.FromDateTime(DateTime.Today),
                    IsActive = true
                });
            }
            else if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.AssignedDate = DateOnly.FromDateTime(DateTime.Today);
            }

            // Tell both people, unless nothing changed
            if (changed)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = client.UserId, Type = "System", ScheduledFor = DateTime.Now, IsRead = false,
                    Message = $"{trainerName} is now your trainer."
                });
                _db.Notifications.Add(new Notification
                {
                    UserId = trainer.UserId, Type = "System", ScheduledFor = DateTime.Now, IsRead = false,
                    Message = $"{clientName} has been assigned to you."
                });
            }

            await _db.SaveChangesAsync();
            TempData["Toast"] = $"{clientName} is now assigned to {trainerName}.";
            return RedirectToAction(nameof(Assignments), new { unassignedOnly });
        }

        [HttpPost]
        public async Task<IActionResult> Unassign(int clientId, bool unassignedOnly = false)
        {
            var active = await _db.TrainerAssignments
                .Where(a => a.ClientId == clientId && a.IsActive).ToListAsync();
            foreach (var a in active) a.IsActive = false;

            if (active.Count > 0)
            {
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Trainer removed from this client.";
            }
            return RedirectToAction(nameof(Assignments), new { unassignedOnly });
        }
    }
}
