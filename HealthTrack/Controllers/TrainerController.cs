using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class TrainerController : Controller
    {
        private readonly HealthTrackContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public TrainerController(HealthTrackContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private async Task<TrainerProfile?> GetMeAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            return await _db.TrainerProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
        }

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Account");

        // Only clients with an active assignment to this trainer are visible or editable
        private IQueryable<int> MyClientIds(int trainerId) =>
            _db.TrainerAssignments.Where(a => a.TrainerId == trainerId && a.IsActive).Select(a => a.ClientId);

        private async Task<List<ClientOption>> MyClientOptionsAsync(int trainerId)
        {
            var ids = MyClientIds(trainerId);
            return await _db.ClientProfiles
                .Where(c => ids.Contains(c.ClientId))
                .OrderBy(c => c.FullName)
                .Select(c => new ClientOption { Id = c.ClientId, Name = c.FullName })
                .ToListAsync();
        }

        private void Notify(string userId, string message, string type)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message.Length > 250 ? message[..250] : message,
                Type = type,
                ScheduledFor = DateTime.Now,
                IsRead = false
            });
        }

        // =====================================================================
        // Dashboard
        // =====================================================================
        public async Task<IActionResult> Dashboard()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var vm = new TrainerDashboardViewModel
            {
                FullName = me.FullName,
                Specialization = me.Specialization
            };

            var myClientIds = MyClientIds(me.TrainerId);

            vm.Clients = await _db.ClientProfiles
                .Where(c => myClientIds.Contains(c.ClientId))
                .OrderBy(c => c.FullName)
                .Select(c => new PersonRow { FullName = c.FullName, Phone = c.Phone })
                .ToListAsync();
            vm.ActiveClients = vm.Clients.Count;

            vm.PlansCreated =
                await _db.WorkoutPlans.CountAsync(p => p.TrainerId == me.TrainerId) +
                await _db.DietPlans.CountAsync(p => p.TrainerId == me.TrainerId);

            vm.PendingAppointments = await _db.Appointments
                .CountAsync(a => a.TrainerId == me.TrainerId && a.Status == "Pending");

            vm.Pending = await (from a in _db.Appointments
                                join c in _db.ClientProfiles on a.ClientId equals c.ClientId
                                where a.TrainerId == me.TrainerId && a.Status == "Pending"
                                orderby a.AppointmentDateTime
                                select new AppointmentRow
                                {
                                    When = a.AppointmentDateTime,
                                    Person = c.FullName,
                                    Status = a.Status,
                                    Reason = a.Reason
                                }).Take(5).ToListAsync();

            return View(vm);
        }

        // =====================================================================
        // My clients
        // =====================================================================
        public async Task<IActionResult> Clients()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var ids = MyClientIds(me.TrainerId);
            var clients = await _db.ClientProfiles
                .Where(c => ids.Contains(c.ClientId))
                .OrderBy(c => c.FullName)
                .Select(c => new { c.ClientId, c.FullName, c.Phone })
                .ToListAsync();

            var clientIds = clients.Select(c => c.ClientId).ToList();
            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));

            var metrics = await _db.HealthMetrics
                .Where(m => clientIds.Contains(m.ClientId))
                .Select(m => new { m.ClientId, m.RecordedDate, m.MetricId, m.WeightKg, m.Bmi })
                .ToListAsync();
            var logs = await _db.WorkoutLogs
                .Where(l => clientIds.Contains(l.ClientId))
                .Select(l => new { l.ClientId, l.LogDate })
                .ToListAsync();
            var wPlans = await _db.WorkoutPlans.Where(p => p.TrainerId == me.TrainerId && clientIds.Contains(p.ClientId))
                .Select(p => p.ClientId).ToListAsync();
            var dPlans = await _db.DietPlans.Where(p => p.TrainerId == me.TrainerId && clientIds.Contains(p.ClientId))
                .Select(p => p.ClientId).ToListAsync();

            var rows = clients.Select(c =>
            {
                var latest = metrics.Where(m => m.ClientId == c.ClientId)
                    .OrderByDescending(m => m.RecordedDate).ThenByDescending(m => m.MetricId).FirstOrDefault();
                var mine = logs.Where(l => l.ClientId == c.ClientId).ToList();
                return new TrainerClientRow
                {
                    ClientId = c.ClientId,
                    Name = c.FullName,
                    Phone = c.Phone,
                    LatestWeight = latest?.WeightKg,
                    Bmi = latest?.Bmi,
                    LastWorkout = mine.Count > 0 ? mine.Max(l => l.LogDate) : null,
                    WeekSessions = mine.Count(l => l.LogDate >= from),
                    PlanCount = wPlans.Count(x => x == c.ClientId) + dPlans.Count(x => x == c.ClientId)
                };
            }).ToList();

            return View(rows);
        }

        // =====================================================================
        // Appointments
        // =====================================================================
        public async Task<IActionResult> Appointments()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var raw = await (from a in _db.Appointments
                             join c in _db.ClientProfiles on a.ClientId equals c.ClientId
                             where a.TrainerId == me.TrainerId
                             select new TrainerApptRow
                             {
                                 Id = a.AppointmentId, When = a.AppointmentDateTime, ClientName = c.FullName,
                                 Status = a.Status, Reason = a.Reason, Remarks = a.TrainerRemarks
                             }).ToListAsync();

            var now = DateTime.Now;
            var vm = new TrainerAppointmentsViewModel
            {
                Pending = raw.Where(r => r.Status == "Pending" && r.When > now).OrderBy(r => r.When).ToList(),
                Upcoming = raw.Where(r => r.Status == "Approved" && r.When > now).OrderBy(r => r.When).ToList(),
                ToComplete = raw.Where(r => r.Status == "Approved" && r.When <= now).OrderBy(r => r.When).ToList(),
                History = raw.Where(r => r.Status == "Completed" || r.Status == "Cancelled" || (r.Status == "Pending" && r.When <= now))
                             .OrderByDescending(r => r.When).Take(30).ToList()
            };
            return View(vm);
        }

        // Loads an appointment only if it belongs to this trainer, with both people's details
        private async Task<(Appointment A, ClientProfile C)?> OwnApptAsync(TrainerProfile me, int id)
        {
            var a = await _db.Appointments.FirstOrDefaultAsync(x => x.AppointmentId == id && x.TrainerId == me.TrainerId);
            if (a == null) return null;
            var c = await _db.ClientProfiles.FirstAsync(x => x.ClientId == a.ClientId);
            return (a, c);
        }

        private static string? CleanRemarks(string? s, out bool tooLong)
        {
            s = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            tooLong = s != null && s.Length > 250;
            return s;
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAppointment(int id, string? remarks)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var own = await OwnApptAsync(me, id);
            var text = CleanRemarks(remarks, out var tooLong);

            if (own == null || own.Value.A.Status != "Pending" || own.Value.A.AppointmentDateTime <= DateTime.Now || tooLong)
            {
                TempData["Error"] = tooLong ? "Remarks can be up to 250 characters." : "That request can't be approved.";
                return RedirectToAction(nameof(Appointments));
            }

            var (a, c) = own.Value;
            a.Status = "Approved";
            if (text != null) a.TrainerRemarks = text;
            AppointmentRules.Notify(_db, c.UserId, $"{me.FullName} approved your appointment on {a.AppointmentDateTime:dd MMM} at {a.AppointmentDateTime:h:mm tt}.");
            AppointmentRules.AddReminders(_db, c.UserId, me.UserId, c.FullName, me.FullName, a.AppointmentDateTime);
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Appointment approved.";
            return RedirectToAction(nameof(Appointments));
        }

        // Decline a pending request, or cancel an approved one. A reason is required.
        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int id, string? remarks)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var own = await OwnApptAsync(me, id);
            var text = CleanRemarks(remarks, out var tooLong);

            if (text == null || tooLong)
            {
                TempData["Error"] = "Please give the client a short reason (up to 250 characters).";
                return RedirectToAction(nameof(Appointments));
            }
            if (own == null || (own.Value.A.Status != "Pending" && own.Value.A.Status != "Approved"))
            {
                TempData["Error"] = "That appointment can't be cancelled.";
                return RedirectToAction(nameof(Appointments));
            }

            var (a, c) = own.Value;
            var wasPending = a.Status == "Pending";
            a.Status = "Cancelled";
            a.TrainerRemarks = text;
            await AppointmentRules.ClearRemindersAsync(_db, c.UserId, me.UserId, c.FullName, me.FullName, a.AppointmentDateTime);
            AppointmentRules.Notify(_db, c.UserId,
                $"{me.FullName} {(wasPending ? "declined" : "cancelled")} your appointment on {a.AppointmentDateTime:dd MMM} at {a.AppointmentDateTime:h:mm tt}. {text}");
            await _db.SaveChangesAsync();

            TempData["Toast"] = wasPending ? "Request declined." : "Appointment cancelled.";
            return RedirectToAction(nameof(Appointments));
        }

        [HttpPost]
        public async Task<IActionResult> RescheduleAppointment(int id, DateTime date, string time, string? remarks)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var own = await OwnApptAsync(me, id);
            var text = CleanRemarks(remarks, out var tooLong);

            if (own == null || (own.Value.A.Status != "Pending" && own.Value.A.Status != "Approved") || tooLong)
            {
                TempData["Error"] = tooLong ? "Remarks can be up to 250 characters." : "That appointment can't be changed.";
                return RedirectToAction(nameof(Appointments));
            }

            var (a, c) = own.Value;
            var err = AppointmentRules.ValidateSlot(date, time, out var start)
                      ?? await AppointmentRules.ConflictAsync(_db, me.TrainerId, c.ClientId, start, a.AppointmentId);
            if (err != null)
            {
                TempData["Error"] = err;
                return RedirectToAction(nameof(Appointments));
            }

            var old = a.AppointmentDateTime;
            await AppointmentRules.ClearRemindersAsync(_db, c.UserId, me.UserId, c.FullName, me.FullName, old);
            a.AppointmentDateTime = start;
            a.Status = "Approved";
            if (text != null) a.TrainerRemarks = text;
            AppointmentRules.Notify(_db, c.UserId,
                $"{me.FullName} moved your appointment from {old:dd MMM} {old:h:mm tt} to {start:dd MMM} at {start:h:mm tt}.");
            AppointmentRules.AddReminders(_db, c.UserId, me.UserId, c.FullName, me.FullName, start);
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Appointment updated and the client has been told.";
            return RedirectToAction(nameof(Appointments));
        }

        [HttpPost]
        public async Task<IActionResult> CompleteAppointment(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var own = await OwnApptAsync(me, id);

            if (own == null || own.Value.A.Status != "Approved" || own.Value.A.AppointmentDateTime > DateTime.Now)
            {
                TempData["Error"] = "Only an approved appointment whose time has passed can be completed.";
                return RedirectToAction(nameof(Appointments));
            }

            var (a, c) = own.Value;
            a.Status = "Completed";
            AppointmentRules.Notify(_db, c.UserId, $"Your appointment on {a.AppointmentDateTime:dd MMM} with {me.FullName} was marked completed.");
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Marked as completed.";
            return RedirectToAction(nameof(Appointments));
        }

        // =====================================================================
        // Feedback to clients
        // =====================================================================
        public async Task<IActionResult> Feedback(int? clientId)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var vm = await BuildFeedbackAsync(me, new FeedbackInput());
            if (clientId != null && vm.Clients.Any(c => c.Id == clientId)) vm.Input.ClientId = clientId.Value;
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SendFeedback([Bind(Prefix = "Input")] FeedbackInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            input.Message = (input.Message ?? "").Trim();

            // The client must be one of my own active clients
            var client = await _db.ClientProfiles
                .Where(c => c.ClientId == input.ClientId && MyClientIds(me.TrainerId).Contains(c.ClientId))
                .FirstOrDefaultAsync();
            if (client == null) ModelState.AddModelError("Input.ClientId", "Choose one of your clients.");

            if (!ModelState.IsValid || client == null)
                return View("Feedback", await BuildFeedbackAsync(me, input));

            _db.Feedbacks.Add(new HealthTrack.Models.Feedback
            {
                TrainerId = me.TrainerId,
                ClientId = client.ClientId,
                Message = input.Message,
                CreatedAt = DateTime.Now
            });
            var preview = input.Message.Length > 120 ? input.Message[..120] + "..." : input.Message;
            Notify(client.UserId, $"Feedback from {me.FullName}: {preview}", "System");
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Feedback sent to {client.FullName}.";
            return RedirectToAction(nameof(Feedback));
        }

        // A trainer can only remove feedback they wrote themselves
        [HttpPost]
        public async Task<IActionResult> DeleteFeedback(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var f = await _db.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id && x.TrainerId == me.TrainerId);
            if (f != null)
            {
                _db.Feedbacks.Remove(f);
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Feedback deleted.";
            }
            return RedirectToAction(nameof(Feedback));
        }

        private async Task<TrainerFeedbackViewModel> BuildFeedbackAsync(TrainerProfile me, FeedbackInput input)
        {
            var rows = await (from f in _db.Feedbacks
                              join c in _db.ClientProfiles on f.ClientId equals c.ClientId
                              where f.TrainerId == me.TrainerId
                              orderby f.CreatedAt descending
                              select new { f.FeedbackId, Client = c.FullName, f.Message, At = (DateTime?)f.CreatedAt })
                             .Take(100).ToListAsync();
            return new TrainerFeedbackViewModel
            {
                Clients = await MyClientOptionsAsync(me.TrainerId),
                Input = input,
                Items = rows.Select(r => new FeedbackRow
                {
                    Id = r.FeedbackId, ClientName = r.Client, TrainerName = me.FullName,
                    Message = r.Message, When = r.At ?? DateTime.Now
                }).ToList()
            };
        }

        // =====================================================================
        // Reports
        // =====================================================================
        public async Task<IActionResult> Reports(int? clientId)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var rows = await BuildClientReportAsync(me);
            var vm = new TrainerReportsViewModel { Rows = rows };

            // Only a client from this trainer's own list can be opened
            var chosen = clientId == null ? null : rows.FirstOrDefault(r => r.ClientId == clientId);
            if (chosen != null) vm.Detail = await BuildClientDetailAsync(me, chosen);

            return View(vm);
        }

        public async Task<IActionResult> ExportClients()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var rows = await BuildClientReportAsync(me);
            var bytes = CsvWriter.Build(
                new[] { "Client", "Start weight (kg)", "Latest weight (kg)", "Change (kg)", "BMI", "BMI category",
                        "Sessions (28 days)", "Calories burned (28 days)", "Avg calories eaten per logged day",
                        "Plan adherence this week (%)", "Last active" },
                rows.Select(r => new object?[] { r.Name, r.StartWeight, r.LatestWeight, r.Change, r.Bmi, r.Category,
                                                 r.Sessions28, r.Burned28, r.AvgEaten, r.AdherencePct, r.LastActive }));
            return File(bytes, "text/csv", $"client-report-{DateTime.Today:yyyyMMdd}.csv");
        }

        private async Task<List<TrainerClientReportRow>> BuildClientReportAsync(TrainerProfile me)
        {
            var ids = MyClientIds(me.TrainerId);
            var clients = await _db.ClientProfiles
                .Where(c => ids.Contains(c.ClientId))
                .OrderBy(c => c.FullName)
                .Select(c => new { c.ClientId, c.FullName })
                .ToListAsync();
            var cids = clients.Select(c => c.ClientId).ToList();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var from28 = today.AddDays(-27);
            var from7 = today.AddDays(-6);

            var metrics = await _db.HealthMetrics.Where(m => cids.Contains(m.ClientId))
                .Select(m => new { m.ClientId, m.RecordedDate, m.MetricId, m.WeightKg, m.Bmi }).ToListAsync();
            var logs = await _db.WorkoutLogs.Where(l => cids.Contains(l.ClientId) && l.LogDate >= from28)
                .Select(l => new { l.ClientId, l.LogDate, l.CaloriesBurned, l.ItemId }).ToListAsync();
            var meals = await _db.MealLogs.Where(l => cids.Contains(l.ClientId) && l.LogDate >= from28)
                .Select(l => new { l.ClientId, l.LogDate, l.Calories }).ToListAsync();

            // Adherence uses this trainer's currently active workout plans
            var plans = await _db.WorkoutPlans
                .Where(p => p.TrainerId == me.TrainerId && cids.Contains(p.ClientId) &&
                            p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .Select(p => new { p.PlanId, p.ClientId }).ToListAsync();
            var planIds = plans.Select(p => p.PlanId).ToList();
            var items = await _db.WorkoutPlanItems.Where(i => planIds.Contains(i.PlanId))
                .Select(i => new { i.ItemId, i.PlanId }).ToListAsync();

            var result = new List<TrainerClientReportRow>();
            foreach (var c in clients)
            {
                var ms = metrics.Where(m => m.ClientId == c.ClientId)
                    .OrderBy(m => m.RecordedDate).ThenBy(m => m.MetricId).ToList();
                var myLogs = logs.Where(l => l.ClientId == c.ClientId).ToList();
                var myMeals = meals.Where(l => l.ClientId == c.ClientId).ToList();

                var myItemIds = items.Where(i => plans.Any(p => p.PlanId == i.PlanId && p.ClientId == c.ClientId))
                    .Select(i => i.ItemId).ToList();
                int? adherence = null;
                if (myItemIds.Count > 0)
                {
                    var done = myLogs.Where(l => l.LogDate >= from7 && l.ItemId != null && myItemIds.Contains(l.ItemId.Value))
                        .Select(l => l.ItemId!.Value).Distinct().Count();
                    adherence = (int)Math.Min(100, Math.Round(done * 100.0 / myItemIds.Count));
                }

                var eatenDays = myMeals.GroupBy(m => m.LogDate).Select(g => g.Sum(x => x.Calories ?? 0)).Where(k => k > 0).ToList();
                var first = ms.FirstOrDefault();
                var last = ms.LastOrDefault();

                var dates = new List<DateOnly>();
                if (ms.Count > 0) dates.Add(last!.RecordedDate);
                dates.AddRange(myLogs.Select(l => l.LogDate));
                dates.AddRange(myMeals.Select(l => l.LogDate));

                result.Add(new TrainerClientReportRow
                {
                    ClientId = c.ClientId,
                    Name = c.FullName,
                    StartWeight = first?.WeightKg,
                    LatestWeight = last?.WeightKg,
                    Change = (first != null && last != null && ms.Count > 1) ? last.WeightKg - first.WeightKg : null,
                    Bmi = last?.Bmi is decimal b ? Math.Round(b, 1) : null,
                    Category = last?.Bmi is decimal bb ? HealthMath.Category(bb) : "",
                    Sessions28 = myLogs.Count,
                    Burned28 = myLogs.Sum(l => l.CaloriesBurned ?? 0),
                    AvgEaten = eatenDays.Count > 0 ? (int)Math.Round(eatenDays.Average()) : null,
                    AdherencePct = adherence,
                    LastActive = dates.Count > 0 ? dates.Max() : null
                });
            }
            return result;
        }

        private async Task<TrainerClientDetail> BuildClientDetailAsync(TrainerProfile me, TrainerClientReportRow row)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var chart = new ReportChartData();

            var recent = await _db.HealthMetrics.Where(m => m.ClientId == row.ClientId)
                .OrderByDescending(m => m.RecordedDate).ThenByDescending(m => m.MetricId).Take(60)
                .Select(m => new { m.RecordedDate, m.WeightKg, m.Bmi }).ToListAsync();
            recent.Reverse();
            foreach (var m in recent)
            {
                chart.WeightLabels.Add(m.RecordedDate.ToString("dd MMM"));
                chart.Weights.Add(m.WeightKg);
                chart.Bmis.Add(m.Bmi);
            }

            var from28 = today.AddDays(-27);
            var logs = await _db.WorkoutLogs.Where(l => l.ClientId == row.ClientId && l.LogDate >= from28)
                .Select(l => new { l.LogDate, l.CaloriesBurned }).ToListAsync();
            var meals = await _db.MealLogs.Where(l => l.ClientId == row.ClientId && l.LogDate >= from28)
                .Select(l => new { l.LogDate, l.Calories }).ToListAsync();

            for (var i = 13; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                chart.DayLabels.Add(day.ToString("dd MMM"));
                chart.Burned.Add(logs.Where(l => l.LogDate == day).Sum(l => l.CaloriesBurned ?? 0));
                chart.Eaten.Add(meals.Where(l => l.LogDate == day).Sum(l => l.Calories ?? 0));
            }
            for (var w = 0; w < 4; w++)
            {
                var start = from28.AddDays(w * 7);
                var end = start.AddDays(6);
                chart.WeekLabels.Add(start.ToString("dd MMM"));
                chart.WeekSessions.Add(logs.Count(l => l.LogDate >= start && l.LogDate <= end));
            }

            var now = DateTime.Now;
            var appts = await _db.Appointments
                .Where(a => a.TrainerId == me.TrainerId && a.ClientId == row.ClientId)
                .Select(a => new { a.Status, a.AppointmentDateTime }).ToListAsync();

            return new TrainerClientDetail
            {
                Row = row,
                Chart = chart,
                Completed = appts.Count(a => a.Status == "Completed"),
                Upcoming = appts.Count(a => a.Status == "Approved" && a.AppointmentDateTime > now),
                Pending = appts.Count(a => a.Status == "Pending" && a.AppointmentDateTime > now),
                Cancelled = appts.Count(a => a.Status == "Cancelled")
            };
        }

        // =====================================================================
        // Plan lists
        // =====================================================================
        public async Task<IActionResult> Plans()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var vm = new TrainerPlansViewModel();

            var w = await (from p in _db.WorkoutPlans
                           join c in _db.ClientProfiles on p.ClientId equals c.ClientId
                           where p.TrainerId == me.TrainerId
                           orderby p.StartDate descending
                           select new { p.PlanId, p.Title, c.FullName, p.StartDate, p.EndDate }).ToListAsync();
            var wCounts = await _db.WorkoutPlanItems
                .Where(i => _db.WorkoutPlans.Any(p => p.PlanId == i.PlanId && p.TrainerId == me.TrainerId))
                .GroupBy(i => i.PlanId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            vm.Workout = w.Select(p => new PlanRow
            {
                Id = p.PlanId, Title = p.Title, ClientName = p.FullName, Start = p.StartDate, End = p.EndDate,
                ItemCount = wCounts.FirstOrDefault(x => x.Key == p.PlanId)?.N ?? 0
            }).ToList();

            var d = await (from p in _db.DietPlans
                           join c in _db.ClientProfiles on p.ClientId equals c.ClientId
                           where p.TrainerId == me.TrainerId
                           orderby p.StartDate descending
                           select new { p.DietPlanId, p.Title, c.FullName, p.StartDate, p.EndDate }).ToListAsync();
            var dCounts = await _db.DietPlanItems
                .Where(i => _db.DietPlans.Any(p => p.DietPlanId == i.DietPlanId && p.TrainerId == me.TrainerId))
                .GroupBy(i => i.DietPlanId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            vm.Diet = d.Select(p => new PlanRow
            {
                Id = p.DietPlanId, Title = p.Title, ClientName = p.FullName, Start = p.StartDate, End = p.EndDate,
                ItemCount = dCounts.FirstOrDefault(x => x.Key == p.DietPlanId)?.N ?? 0
            }).ToList();

            return View(vm);
        }

        // =====================================================================
        // Create a plan (kind = workout or diet)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> NewPlan(string kind = "workout", int? clientId = null)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var vm = new NewPlanViewModel
            {
                Kind = kind == "diet" ? "diet" : "workout",
                Clients = await MyClientOptionsAsync(me.TrainerId)
            };
            if (clientId != null && vm.Clients.Any(c => c.Id == clientId)) vm.Input.ClientId = clientId;
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> NewPlan(string kind, [Bind(Prefix = "Input")] NewPlanInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            kind = kind == "diet" ? "diet" : "workout";
            var clients = await MyClientOptionsAsync(me.TrainerId);

            if (input.ClientId != null && !clients.Any(c => c.Id == input.ClientId))
                ModelState.AddModelError("Input.ClientId", "You can only create plans for your own clients.");
            if (input.EndDate != null && input.EndDate.Value.Date < input.StartDate.Date)
                ModelState.AddModelError("Input.EndDate", "The end date can't be before the start date.");

            if (!ModelState.IsValid)
                return View(new NewPlanViewModel { Kind = kind, Input = input, Clients = clients });

            var client = await _db.ClientProfiles.FirstAsync(c => c.ClientId == input.ClientId!.Value);
            var start = DateOnly.FromDateTime(input.StartDate);
            DateOnly? end = input.EndDate is DateTime e ? DateOnly.FromDateTime(e) : null;

            if (kind == "workout")
            {
                var plan = new WorkoutPlan
                {
                    TrainerId = me.TrainerId,
                    ClientId = client.ClientId,
                    Title = input.Title.Trim(),
                    Goal = string.IsNullOrWhiteSpace(input.Goal) ? null : input.Goal.Trim(),
                    StartDate = start,
                    EndDate = end
                };
                _db.WorkoutPlans.Add(plan);
                Notify(client.UserId, $"{me.FullName} created a workout plan for you: {plan.Title}.", "Workout");
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Plan created. Now add the exercises.";
                return RedirectToAction(nameof(WorkoutPlanDetails), new { id = plan.PlanId });
            }
            else
            {
                var plan = new DietPlan
                {
                    TrainerId = me.TrainerId,
                    ClientId = client.ClientId,
                    Title = input.Title.Trim(),
                    StartDate = start,
                    EndDate = end
                };
                _db.DietPlans.Add(plan);
                Notify(client.UserId, $"{me.FullName} created a diet plan for you: {plan.Title}.", "Meal");
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Plan created. Now add the meals.";
                return RedirectToAction(nameof(DietPlanDetails), new { id = plan.DietPlanId });
            }
        }

        // =====================================================================
        // Workout plan details
        // =====================================================================
        public async Task<IActionResult> WorkoutPlanDetails(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var vm = await BuildWorkoutDetailsAsync(me, id, new WorkoutItemInput());
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddWorkoutItem(int id, [Bind(Prefix = "Input")] WorkoutItemInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var plan = await _db.WorkoutPlans.FirstOrDefaultAsync(p => p.PlanId == id && p.TrainerId == me.TrainerId);
            if (plan == null) return NotFound();

            if (!PlanDays.All.Contains(input.DayOfWeek))
                ModelState.AddModelError("Input.DayOfWeek", "Choose a day of the week.");
            if (input.Sets == null && input.Reps == null && input.DurationMin == null)
                ModelState.AddModelError("Input.DurationMin", "Add sets and reps, or a duration.");

            if (!ModelState.IsValid)
                return View("WorkoutPlanDetails", await BuildWorkoutDetailsAsync(me, id, input));

            _db.WorkoutPlanItems.Add(new WorkoutPlanItem
            {
                PlanId = id,
                ExerciseName = input.ExerciseName.Trim(),
                DayOfWeek = input.DayOfWeek,
                Sets = input.Sets,
                Reps = input.Reps,
                DurationMin = input.DurationMin
            });
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Exercise added.";
            return RedirectToAction(nameof(WorkoutPlanDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWorkoutItem(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var item = await _db.WorkoutPlanItems.FirstOrDefaultAsync(i => i.ItemId == id);
            if (item == null) return RedirectToAction(nameof(Plans));
            var owns = await _db.WorkoutPlans.AnyAsync(p => p.PlanId == item.PlanId && p.TrainerId == me.TrainerId);
            if (!owns) return Denied();

            // Keep the client's past logs, just detach them from the removed exercise
            await _db.WorkoutLogs.Where(l => l.ItemId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ItemId, (int?)null));
            _db.WorkoutPlanItems.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Exercise removed.";
            return RedirectToAction(nameof(WorkoutPlanDetails), new { id = item.PlanId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWorkoutPlan(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var plan = await _db.WorkoutPlans.FirstOrDefaultAsync(p => p.PlanId == id && p.TrainerId == me.TrainerId);
            if (plan != null)
            {
                var itemIds = await _db.WorkoutPlanItems.Where(i => i.PlanId == id).Select(i => i.ItemId).ToListAsync();
                await _db.WorkoutLogs.Where(l => l.ItemId != null && itemIds.Contains(l.ItemId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.ItemId, (int?)null));
                _db.WorkoutPlans.Remove(plan);   // items are removed by the database cascade
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Workout plan deleted.";
            }
            return RedirectToAction(nameof(Plans));
        }

        private async Task<WorkoutPlanDetailsViewModel?> BuildWorkoutDetailsAsync(TrainerProfile me, int id, WorkoutItemInput input)
        {
            var p = await (from pl in _db.WorkoutPlans
                           join c in _db.ClientProfiles on pl.ClientId equals c.ClientId
                           where pl.PlanId == id && pl.TrainerId == me.TrainerId
                           select new { pl.PlanId, pl.Title, pl.Goal, pl.StartDate, pl.EndDate, c.FullName })
                          .FirstOrDefaultAsync();
            if (p == null) return null;

            var items = await _db.WorkoutPlanItems.Where(i => i.PlanId == id).OrderBy(i => i.ItemId).ToListAsync();
            return new WorkoutPlanDetailsViewModel
            {
                Id = p.PlanId, Title = p.Title, Goal = p.Goal, ClientName = p.FullName, Start = p.StartDate, End = p.EndDate,
                Input = input,
                Days = PlanDays.All
                    .Select(d => new PlanDay<WorkoutItemRow>
                    {
                        Day = d,
                        Items = items.Where(i => i.DayOfWeek == d).Select(i => new WorkoutItemRow
                        {
                            Id = i.ItemId, Name = i.ExerciseName, Sets = i.Sets, Reps = i.Reps, DurationMin = i.DurationMin
                        }).ToList()
                    })
                    .Where(d => d.Items.Count > 0).ToList()
            };
        }

        // =====================================================================
        // Diet plan details
        // =====================================================================
        public async Task<IActionResult> DietPlanDetails(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var vm = await BuildDietDetailsAsync(me, id, new DietItemInput());
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddDietItem(int id, [Bind(Prefix = "Input")] DietItemInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var plan = await _db.DietPlans.FirstOrDefaultAsync(p => p.DietPlanId == id && p.TrainerId == me.TrainerId);
            if (plan == null) return NotFound();

            if (!PlanDays.All.Contains(input.DayOfWeek))
                ModelState.AddModelError("Input.DayOfWeek", "Choose a day of the week.");
            if (!PlanDays.Meals.Contains(input.MealType))
                ModelState.AddModelError("Input.MealType", "Choose Breakfast, Lunch, Dinner or Snack.");

            if (!ModelState.IsValid)
                return View("DietPlanDetails", await BuildDietDetailsAsync(me, id, input));

            _db.DietPlanItems.Add(new DietPlanItem
            {
                DietPlanId = id,
                DayOfWeek = input.DayOfWeek,
                MealType = input.MealType,
                FoodDescription = input.FoodDescription.Trim(),
                Calories = input.Calories
            });
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Meal added.";
            return RedirectToAction(nameof(DietPlanDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDietItem(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var item = await _db.DietPlanItems.FirstOrDefaultAsync(i => i.DietItemId == id);
            if (item == null) return RedirectToAction(nameof(Plans));
            var owns = await _db.DietPlans.AnyAsync(p => p.DietPlanId == item.DietPlanId && p.TrainerId == me.TrainerId);
            if (!owns) return Denied();

            _db.DietPlanItems.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Meal removed.";
            return RedirectToAction(nameof(DietPlanDetails), new { id = item.DietPlanId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDietPlan(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var plan = await _db.DietPlans.FirstOrDefaultAsync(p => p.DietPlanId == id && p.TrainerId == me.TrainerId);
            if (plan != null)
            {
                _db.DietPlans.Remove(plan);
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Diet plan deleted.";
            }
            return RedirectToAction(nameof(Plans));
        }

        private async Task<DietPlanDetailsViewModel?> BuildDietDetailsAsync(TrainerProfile me, int id, DietItemInput input)
        {
            var p = await (from pl in _db.DietPlans
                           join c in _db.ClientProfiles on pl.ClientId equals c.ClientId
                           where pl.DietPlanId == id && pl.TrainerId == me.TrainerId
                           select new { pl.DietPlanId, pl.Title, pl.StartDate, pl.EndDate, c.FullName })
                          .FirstOrDefaultAsync();
            if (p == null) return null;

            var items = await _db.DietPlanItems.Where(i => i.DietPlanId == id).ToListAsync();
            var mealOrder = PlanDays.Meals.ToList();
            return new DietPlanDetailsViewModel
            {
                Id = p.DietPlanId, Title = p.Title, ClientName = p.FullName, Start = p.StartDate, End = p.EndDate,
                Input = input,
                Days = PlanDays.All
                    .Select(d =>
                    {
                        var list = items.Where(i => i.DayOfWeek == d)
                            .OrderBy(i => mealOrder.IndexOf(i.MealType)).ThenBy(i => i.DietItemId).ToList();
                        return new PlanDay<DietItemRow>
                        {
                            Day = d,
                            Kcal = list.Sum(i => i.Calories ?? 0),
                            Items = list.Select(i => new DietItemRow
                            {
                                Id = i.DietItemId, MealType = i.MealType, Food = i.FoodDescription, Calories = i.Calories
                            }).ToList()
                        };
                    })
                    .Where(d => d.Items.Count > 0).ToList()
            };
        }
    }
}
