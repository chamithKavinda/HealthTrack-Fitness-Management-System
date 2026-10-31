using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientController : Controller
    {
        private static readonly string[] MealTypes = { "Breakfast", "Lunch", "Dinner", "Snack" };
        private static readonly string[] Genders = { "Male", "Female", "Other" };

        private readonly HealthTrackContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly FieldEncryptor _crypto;

        public ClientController(HealthTrackContext db, UserManager<IdentityUser> userManager, FieldEncryptor crypto)
        {
            _db = db;
            _userManager = userManager;
            _crypto = crypto;
        }

        // Ownership: every query below filters on the signed-in client's own ClientId.
        private async Task<ClientProfile?> GetMeAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            return await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Account");

        // =====================================================================
        // Dashboard
        // =====================================================================
        public async Task<IActionResult> Dashboard()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            var userId = me.UserId;

            var vm = new ClientDashboardViewModel { FullName = me.FullName };

            var latestWeight = await _db.HealthMetrics
                .Where(m => m.ClientId == me.ClientId)
                .OrderByDescending(m => m.RecordedDate).ThenByDescending(m => m.MetricId)
                .Select(m => (decimal?)m.WeightKg)
                .FirstOrDefaultAsync();

            if (latestWeight != null)
            {
                vm.LatestWeightKg = latestWeight;
                var bmi = HealthMath.Bmi(latestWeight.Value, me.HeightCm);
                if (bmi != null)
                {
                    vm.Bmi = Math.Round(bmi.Value, 1);
                    vm.BmiCategory = HealthMath.Category(bmi.Value);
                }
                else vm.BmiCategory = "Add your height to see BMI";
            }

            vm.WorkoutPlans = await _db.WorkoutPlans.CountAsync(p => p.ClientId == me.ClientId);
            vm.DietPlans = await _db.DietPlans.CountAsync(p => p.ClientId == me.ClientId);
            vm.WorkoutsLogged = await _db.WorkoutLogs.CountAsync(l => l.ClientId == me.ClientId);
            vm.CaloriesBurned = await _db.WorkoutLogs
                .Where(l => l.ClientId == me.ClientId)
                .SumAsync(l => l.CaloriesBurned ?? 0);
            vm.UnreadNotifications = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            var now = DateTime.Now;
            vm.Upcoming = await (from a in _db.Appointments
                                 join t in _db.TrainerProfiles on a.TrainerId equals t.TrainerId
                                 where a.ClientId == me.ClientId
                                       && a.AppointmentDateTime >= now
                                       && a.Status != "Cancelled"
                                 orderby a.AppointmentDateTime
                                 select new AppointmentRow
                                 {
                                     When = a.AppointmentDateTime,
                                     Person = t.FullName,
                                     Status = a.Status,
                                     Reason = a.Reason
                                 }).Take(5).ToListAsync();

            return View(vm);
        }

        // =====================================================================
        // Health measurements
        // =====================================================================
        public async Task<IActionResult> Health()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            return View(await BuildHealthAsync(me, new MetricInput()));
        }

        [HttpPost]
        public async Task<IActionResult> AddMetric([Bind(Prefix = "Input")] MetricInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            if (input.Date.Date > DateTime.Today)
                ModelState.AddModelError("Input.Date", "The date can't be in the future.");

            if (!ModelState.IsValid)
                return View("Health", await BuildHealthAsync(me, input));

            var weight = input.WeightKg!.Value;
            _db.HealthMetrics.Add(new HealthMetric
            {
                ClientId = me.ClientId,
                RecordedDate = DateOnly.FromDateTime(input.Date),
                WeightKg = weight,
                Bmi = HealthMath.Bmi(weight, me.HeightCm),
                HeartRate = input.HeartRate,
                BloodPressure = string.IsNullOrWhiteSpace(input.BloodPressure) ? null : input.BloodPressure.Trim()
            });
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Measurement saved.";
            return RedirectToAction(nameof(Health));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMetric(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var row = await _db.HealthMetrics.FirstOrDefaultAsync(m => m.MetricId == id && m.ClientId == me.ClientId);
            if (row != null)
            {
                _db.HealthMetrics.Remove(row);
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Measurement deleted.";
            }
            return RedirectToAction(nameof(Health));
        }

        private async Task<HealthPageViewModel> BuildHealthAsync(ClientProfile me, MetricInput input)
        {
            var raw = await _db.HealthMetrics
                .Where(m => m.ClientId == me.ClientId)
                .OrderByDescending(m => m.RecordedDate).ThenByDescending(m => m.MetricId)
                .Take(50)
                .Select(m => new { m.MetricId, m.RecordedDate, m.WeightKg, m.Bmi, m.HeartRate, m.BloodPressure })
                .ToListAsync();

            var rows = raw.Select(m => new MetricRow
            {
                Id = m.MetricId,
                Date = m.RecordedDate,
                WeightKg = m.WeightKg,
                Bmi = m.Bmi,
                HeartRate = m.HeartRate,
                BloodPressure = m.BloodPressure
            }).ToList();

            var vm = new HealthPageViewModel
            {
                Input = input,
                Rows = rows,
                HeightCm = me.HeightCm,
                EntryCount = await _db.HealthMetrics.CountAsync(m => m.ClientId == me.ClientId)
            };

            var latest = rows.FirstOrDefault();
            if (latest != null)
            {
                vm.LatestWeight = latest.WeightKg;
                vm.LatestBmi = latest.Bmi;
                vm.BmiCategory = latest.Bmi is decimal b ? HealthMath.Category(b) : "";
            }
            return vm;
        }

        // =====================================================================
        // Workouts
        // =====================================================================
        public async Task<IActionResult> Workouts(int? itemId)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var input = new WorkoutInput();
            if (itemId != null)
            {
                // Pre-fill from a planned exercise, but only if the plan belongs to this client
                var item = await (from i in _db.WorkoutPlanItems
                                  join p in _db.WorkoutPlans on i.PlanId equals p.PlanId
                                  where i.ItemId == itemId && p.ClientId == me.ClientId
                                  select i).FirstOrDefaultAsync();
                if (item != null)
                {
                    input.ItemId = item.ItemId;
                    input.ExerciseName = item.ExerciseName;
                    input.DurationMin = item.DurationMin;
                }
            }
            return View(await BuildWorkoutsAsync(me, input));
        }

        [HttpPost]
        public async Task<IActionResult> AddWorkout([Bind(Prefix = "Input")] WorkoutInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            if (input.Date.Date > DateTime.Today)
                ModelState.AddModelError("Input.Date", "The date can't be in the future.");

            // A plan link must belong to one of this client's own plans
            if (input.ItemId != null)
            {
                var ok = await (from i in _db.WorkoutPlanItems
                                join p in _db.WorkoutPlans on i.PlanId equals p.PlanId
                                where i.ItemId == input.ItemId && p.ClientId == me.ClientId
                                select i.ItemId).AnyAsync();
                if (!ok) input.ItemId = null;
            }

            if (!ModelState.IsValid)
                return View("Workouts", await BuildWorkoutsAsync(me, input));

            _db.WorkoutLogs.Add(new WorkoutLog
            {
                ClientId = me.ClientId,
                ItemId = input.ItemId,
                LogDate = DateOnly.FromDateTime(input.Date),
                ExerciseName = input.ExerciseName.Trim(),
                DurationMin = input.DurationMin!.Value,
                CaloriesBurned = input.CaloriesBurned
            });
            // Tell the trainer when a planned exercise is completed
            if (input.ItemId != null)
            {
                var trainerUserId = await (from i in _db.WorkoutPlanItems
                                           join p in _db.WorkoutPlans on i.PlanId equals p.PlanId
                                           join t in _db.TrainerProfiles on p.TrainerId equals t.TrainerId
                                           where i.ItemId == input.ItemId
                                           select t.UserId).FirstOrDefaultAsync();
                if (trainerUserId != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = trainerUserId, Type = "Workout", ScheduledFor = DateTime.Now, IsRead = false,
                        Message = $"{me.FullName} completed \"{input.ExerciseName.Trim()}\" from your plan."
                    });
                }
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = "Workout saved.";
            return RedirectToAction(nameof(Workouts));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWorkout(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var row = await _db.WorkoutLogs.FirstOrDefaultAsync(l => l.LogId == id && l.ClientId == me.ClientId);
            if (row != null)
            {
                _db.WorkoutLogs.Remove(row);
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Workout deleted.";
            }
            return RedirectToAction(nameof(Workouts));
        }

        private async Task<WorkoutsPageViewModel> BuildWorkoutsAsync(ClientProfile me, WorkoutInput input)
        {
            var raw = await _db.WorkoutLogs
                .Where(l => l.ClientId == me.ClientId)
                .OrderByDescending(l => l.LogDate).ThenByDescending(l => l.LogId)
                .Take(60)
                .Select(l => new { l.LogId, l.LogDate, l.ExerciseName, l.DurationMin, l.CaloriesBurned })
                .ToListAsync();

            var days = raw.GroupBy(l => l.LogDate).Select(g => new DayGroup<WorkoutRow>
            {
                Date = g.Key,
                Minutes = g.Sum(x => x.DurationMin),
                Kcal = g.Sum(x => x.CaloriesBurned ?? 0),
                Items = g.Select(x => new WorkoutRow
                {
                    Id = x.LogId,
                    Name = x.ExerciseName,
                    DurationMin = x.DurationMin,
                    Calories = x.CaloriesBurned
                }).ToList()
            }).ToList();

            var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));
            var week = await _db.WorkoutLogs
                .Where(l => l.ClientId == me.ClientId && l.LogDate >= from)
                .Select(l => new { l.DurationMin, l.CaloriesBurned })
                .ToListAsync();

            return new WorkoutsPageViewModel
            {
                Input = input,
                Days = days,
                WeekSessions = week.Count,
                WeekMinutes = week.Sum(w => w.DurationMin),
                WeekKcal = week.Sum(w => w.CaloriesBurned ?? 0)
            };
        }

        // =====================================================================
        // Meals
        // =====================================================================
        public async Task<IActionResult> Meals()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            return View(await BuildMealsAsync(me, new MealInput()));
        }

        [HttpPost]
        public async Task<IActionResult> AddMeal([Bind(Prefix = "Input")] MealInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            if (!MealTypes.Contains(input.MealType))
                ModelState.AddModelError("Input.MealType", "Choose Breakfast, Lunch, Dinner or Snack.");
            if (input.Date.Date > DateTime.Today)
                ModelState.AddModelError("Input.Date", "The date can't be in the future.");

            if (!ModelState.IsValid)
                return View("Meals", await BuildMealsAsync(me, input));

            _db.MealLogs.Add(new MealLog
            {
                ClientId = me.ClientId,
                LogDate = DateOnly.FromDateTime(input.Date),
                MealType = input.MealType,
                FoodDescription = input.FoodDescription.Trim(),
                Calories = input.Calories
            });
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Meal saved.";
            return RedirectToAction(nameof(Meals));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMeal(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var row = await _db.MealLogs.FirstOrDefaultAsync(l => l.MealLogId == id && l.ClientId == me.ClientId);
            if (row != null)
            {
                _db.MealLogs.Remove(row);
                await _db.SaveChangesAsync();
                TempData["Toast"] = "Meal deleted.";
            }
            return RedirectToAction(nameof(Meals));
        }

        private async Task<MealsPageViewModel> BuildMealsAsync(ClientProfile me, MealInput input)
        {
            var raw = await _db.MealLogs
                .Where(l => l.ClientId == me.ClientId)
                .OrderByDescending(l => l.LogDate).ThenByDescending(l => l.MealLogId)
                .Take(60)
                .Select(l => new { l.MealLogId, l.LogDate, l.MealType, l.FoodDescription, l.Calories })
                .ToListAsync();

            var days = raw.GroupBy(l => l.LogDate).Select(g => new DayGroup<MealRow>
            {
                Date = g.Key,
                Kcal = g.Sum(x => x.Calories ?? 0),
                Items = g.Select(x => new MealRow
                {
                    Id = x.MealLogId,
                    MealType = x.MealType,
                    Food = x.FoodDescription,
                    Calories = x.Calories
                }).ToList()
            }).ToList();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var todayRows = raw.Where(l => l.LogDate == today).ToList();

            return new MealsPageViewModel
            {
                Input = input,
                Days = days,
                TodayKcal = todayRows.Sum(r => r.Calories ?? 0),
                TodayMeals = todayRows.Count
            };
        }

        // =====================================================================
        // Appointments
        // =====================================================================
        public async Task<IActionResult> Appointments()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();
            return View(await BuildApptsAsync(me, new BookInput()));
        }

        [HttpPost]
        public async Task<IActionResult> BookAppointment([Bind(Prefix = "Input")] BookInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var trainer = await ActiveTrainerAsync(me);
            DateTime start = default;
            if (trainer == null)
            {
                ModelState.AddModelError("", "You don't have a trainer yet. Ask the admin to assign one.");
            }
            else
            {
                var err = AppointmentRules.ValidateSlot(input.Date, input.Time, out start);
                if (err != null) ModelState.AddModelError("Input.Time", err);
                else
                {
                    var clash = await AppointmentRules.ConflictAsync(_db, trainer.TrainerId, me.ClientId, start, null);
                    if (clash != null) ModelState.AddModelError("Input.Time", clash);
                }
            }

            if (!ModelState.IsValid || trainer == null)
                return View("Appointments", await BuildApptsAsync(me, input));

            _db.Appointments.Add(new Appointment
            {
                ClientId = me.ClientId,
                TrainerId = trainer.TrainerId,
                AppointmentDateTime = start,
                Reason = input.Reason.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.Now
            });
            AppointmentRules.Notify(_db, trainer.UserId,
                $"{me.FullName} requested an appointment on {start:dd MMM} at {start:h:mm tt}.");
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Request sent. Your trainer will approve or suggest another time.";
            return RedirectToAction(nameof(Appointments));
        }

        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            // Ownership: only this client's own appointment
            var a = await _db.Appointments.FirstOrDefaultAsync(x => x.AppointmentId == id && x.ClientId == me.ClientId);
            if (a == null || (a.Status != "Pending" && a.Status != "Approved") || a.AppointmentDateTime <= DateTime.Now)
            {
                TempData["Error"] = "That appointment can't be cancelled.";
                return RedirectToAction(nameof(Appointments));
            }

            var trainer = await _db.TrainerProfiles.FirstAsync(t => t.TrainerId == a.TrainerId);
            a.Status = "Cancelled";
            await AppointmentRules.ClearRemindersAsync(_db, me.UserId, trainer.UserId, me.FullName, trainer.FullName, a.AppointmentDateTime);
            AppointmentRules.Notify(_db, trainer.UserId,
                $"{me.FullName} cancelled the appointment on {a.AppointmentDateTime:dd MMM} at {a.AppointmentDateTime:h:mm tt}.");
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Appointment cancelled.";
            return RedirectToAction(nameof(Appointments));
        }

        private async Task<TrainerProfile?> ActiveTrainerAsync(ClientProfile me)
        {
            return await (from a in _db.TrainerAssignments
                          join t in _db.TrainerProfiles on a.TrainerId equals t.TrainerId
                          where a.ClientId == me.ClientId && a.IsActive
                          select t).FirstOrDefaultAsync();
        }

        private async Task<ClientAppointmentsViewModel> BuildApptsAsync(ClientProfile me, BookInput input)
        {
            var trainer = await ActiveTrainerAsync(me);
            var raw = await (from a in _db.Appointments
                             join t in _db.TrainerProfiles on a.TrainerId equals t.TrainerId
                             where a.ClientId == me.ClientId
                             select new { a.AppointmentId, a.AppointmentDateTime, t.FullName, a.Status, a.Reason, a.TrainerRemarks })
                            .ToListAsync();

            var now = DateTime.Now;
            var rows = raw.Select(a => new ClientApptRow
            {
                Id = a.AppointmentId, When = a.AppointmentDateTime, TrainerName = a.FullName, Status = a.Status,
                Reason = a.Reason, Remarks = a.TrainerRemarks,
                CanCancel = (a.Status == "Pending" || a.Status == "Approved") && a.AppointmentDateTime > now
            }).ToList();

            return new ClientAppointmentsViewModel
            {
                Input = input,
                TrainerName = trainer?.FullName,
                Upcoming = rows.Where(r => r.CanCancel).OrderBy(r => r.When).ToList(),
                History = rows.Where(r => !r.CanCancel).OrderByDescending(r => r.When).ToList()
            };
        }

        // =====================================================================
        // Plans written by my trainer
        // =====================================================================
        public async Task<IActionResult> Plans()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var vm = new ClientPlansViewModel();

            var wp = await (from p in _db.WorkoutPlans
                            join t in _db.TrainerProfiles on p.TrainerId equals t.TrainerId
                            where p.ClientId == me.ClientId
                            orderby p.StartDate descending
                            select new { p.PlanId, p.Title, p.Goal, p.StartDate, p.EndDate, Trainer = t.FullName })
                           .ToListAsync();
            var wIds = wp.Select(p => p.PlanId).ToList();
            var wItems = await _db.WorkoutPlanItems.Where(i => wIds.Contains(i.PlanId)).OrderBy(i => i.ItemId).ToListAsync();

            vm.Workout = wp.Select(p => new ClientWorkoutPlanVm
            {
                Title = p.Title, Goal = p.Goal, Trainer = p.Trainer, Start = p.StartDate, End = p.EndDate,
                Active = p.StartDate <= today && (p.EndDate == null || p.EndDate >= today),
                Days = PlanDays.All.Select(d => new PlanDay<WorkoutItemRow>
                {
                    Day = d,
                    Items = wItems.Where(i => i.PlanId == p.PlanId && i.DayOfWeek == d).Select(i => new WorkoutItemRow
                    {
                        Id = i.ItemId, Name = i.ExerciseName, Sets = i.Sets, Reps = i.Reps, DurationMin = i.DurationMin
                    }).ToList()
                }).Where(d => d.Items.Count > 0).ToList()
            }).ToList();

            var dp = await (from p in _db.DietPlans
                            join t in _db.TrainerProfiles on p.TrainerId equals t.TrainerId
                            where p.ClientId == me.ClientId
                            orderby p.StartDate descending
                            select new { p.DietPlanId, p.Title, p.StartDate, p.EndDate, Trainer = t.FullName })
                           .ToListAsync();
            var dIds = dp.Select(p => p.DietPlanId).ToList();
            var dItems = await _db.DietPlanItems.Where(i => dIds.Contains(i.DietPlanId)).ToListAsync();
            var order = PlanDays.Meals.ToList();

            vm.Diet = dp.Select(p => new ClientDietPlanVm
            {
                Title = p.Title, Trainer = p.Trainer, Start = p.StartDate, End = p.EndDate,
                Active = p.StartDate <= today && (p.EndDate == null || p.EndDate >= today),
                Days = PlanDays.All.Select(d =>
                {
                    var list = dItems.Where(i => i.DietPlanId == p.DietPlanId && i.DayOfWeek == d)
                        .OrderBy(i => order.IndexOf(i.MealType)).ThenBy(i => i.DietItemId).ToList();
                    return new PlanDay<DietItemRow>
                    {
                        Day = d,
                        Kcal = list.Sum(i => i.Calories ?? 0),
                        Items = list.Select(i => new DietItemRow
                        {
                            Id = i.DietItemId, MealType = i.MealType, Food = i.FoodDescription, Calories = i.Calories
                        }).ToList()
                    };
                }).Where(d => d.Items.Count > 0).ToList()
            }).ToList();

            return View(vm);
        }

        // =====================================================================
        // Feedback from my trainer
        // =====================================================================
        public async Task<IActionResult> Feedback()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var rows = await (from f in _db.Feedbacks
                              join t in _db.TrainerProfiles on f.TrainerId equals t.TrainerId
                              where f.ClientId == me.ClientId
                              orderby f.CreatedAt descending
                              select new { f.FeedbackId, Trainer = t.FullName, f.Message, At = (DateTime?)f.CreatedAt })
                             .Take(100).ToListAsync();

            return View(new ClientFeedbackViewModel
            {
                Items = rows.Select(r => new FeedbackRow
                {
                    Id = r.FeedbackId, TrainerName = r.Trainer, ClientName = me.FullName,
                    Message = r.Message, When = r.At ?? DateTime.Now
                }).ToList()
            });
        }

        // =====================================================================
        // Progress (charts)
        // =====================================================================
        public async Task<IActionResult> Progress()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            var recent = await _db.HealthMetrics
                .Where(m => m.ClientId == me.ClientId)
                .OrderByDescending(m => m.RecordedDate).ThenByDescending(m => m.MetricId)
                .Take(60)
                .Select(m => new { m.RecordedDate, m.WeightKg, m.Bmi })
                .ToListAsync();
            recent.Reverse();   // oldest first for the chart

            var chart = new ProgressChartData();
            foreach (var m in recent)
            {
                chart.WeightLabels.Add(m.RecordedDate.ToString("dd MMM"));
                chart.Weights.Add(m.WeightKg);
                chart.Bmis.Add(m.Bmi);
            }

            // Last 14 days of calories burned and eaten
            var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-13));
            var burned = await _db.WorkoutLogs
                .Where(l => l.ClientId == me.ClientId && l.LogDate >= start)
                .Select(l => new { l.LogDate, l.CaloriesBurned })
                .ToListAsync();
            var eaten = await _db.MealLogs
                .Where(l => l.ClientId == me.ClientId && l.LogDate >= start)
                .Select(l => new { l.LogDate, l.Calories })
                .ToListAsync();

            for (var i = 0; i < 14; i++)
            {
                var day = start.AddDays(i);
                chart.DayLabels.Add(day.ToString("dd MMM"));
                chart.Burned.Add(burned.Where(b => b.LogDate == day).Sum(b => b.CaloriesBurned ?? 0));
                chart.Eaten.Add(eaten.Where(e => e.LogDate == day).Sum(e => e.Calories ?? 0));
            }

            var firstWeight = await _db.HealthMetrics
                .Where(m => m.ClientId == me.ClientId)
                .OrderBy(m => m.RecordedDate).ThenBy(m => m.MetricId)
                .Select(m => (decimal?)m.WeightKg)
                .FirstOrDefaultAsync();

            var vm = new ProgressViewModel
            {
                Chart = chart,
                Entries = await _db.HealthMetrics.CountAsync(m => m.ClientId == me.ClientId),
                BurnedTotal = chart.Burned.Sum(),
                EatenTotal = chart.Eaten.Sum(),
                WeightChange = (firstWeight != null && recent.Count > 0)
                    ? recent[^1].WeightKg - firstWeight.Value
                    : null
            };
            return View(vm);
        }

        // =====================================================================
        // Profile (height feeds BMI; medical notes are encrypted at rest)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            return View(new ProfileInput
            {
                FullName = me.FullName,
                DateOfBirth = me.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
                Gender = me.Gender,
                HeightCm = me.HeightCm,
                Phone = me.Phone,
                MedicalNotes = _crypto.Unprotect(me.MedicalNotes)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Profile(ProfileInput input)
        {
            var me = await GetMeAsync();
            if (me == null) return Denied();

            if (!string.IsNullOrEmpty(input.Gender) && !Genders.Contains(input.Gender))
                ModelState.AddModelError(nameof(input.Gender), "Choose Male, Female or Other.");
            if (input.DateOfBirth is DateTime dobCheck &&
                (dobCheck.Date > DateTime.Today || dobCheck.Year < 1900))
                ModelState.AddModelError(nameof(input.DateOfBirth), "Enter a valid date of birth.");

            if (!ModelState.IsValid) return View(input);

            me.FullName = input.FullName.Trim();
            me.DateOfBirth = input.DateOfBirth is DateTime dob ? DateOnly.FromDateTime(dob) : null;
            me.Gender = string.IsNullOrEmpty(input.Gender) ? null : input.Gender;
            me.HeightCm = input.HeightCm;
            me.Phone = input.Phone;
            me.MedicalNotes = _crypto.Protect(input.MedicalNotes);   // encrypted before saving

            // Height changed: bring the BMI on earlier measurements up to date
            var metrics = await _db.HealthMetrics.Where(m => m.ClientId == me.ClientId).ToListAsync();
            foreach (var m in metrics) m.Bmi = HealthMath.Bmi(m.WeightKg, me.HeightCm);

            await _db.SaveChangesAsync();

            TempData["Toast"] = "Profile saved.";
            return RedirectToAction(nameof(Profile));
        }
    }
}
