using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    // ---------- Health measurements ----------
    public class MetricInput
    {
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Enter your weight.")]
        [Range(10, 500, ErrorMessage = "Weight must be between 10 and 500 kg.")]
        [Display(Name = "Weight (kg)")]
        public decimal? WeightKg { get; set; }

        [Range(20, 250, ErrorMessage = "Heart rate must be between 20 and 250.")]
        [Display(Name = "Heart rate (bpm)")]
        public int? HeartRate { get; set; }

        [RegularExpression(@"^\d{2,3}/\d{2,3}$", ErrorMessage = "Use the format 120/80.")]
        [Display(Name = "Blood pressure")]
        public string? BloodPressure { get; set; }
    }

    public class MetricRow
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal WeightKg { get; set; }
        public decimal? Bmi { get; set; }
        public int? HeartRate { get; set; }
        public string? BloodPressure { get; set; }
    }

    public class HealthPageViewModel
    {
        public MetricInput Input { get; set; } = new();
        public List<MetricRow> Rows { get; set; } = new();
        public decimal? HeightCm { get; set; }
        public decimal? LatestWeight { get; set; }
        public decimal? LatestBmi { get; set; }
        public string BmiCategory { get; set; } = "";
        public int EntryCount { get; set; }
    }

    // ---------- Day grouping used by the workout and meal lists ----------
    public class DayGroup<T>
    {
        public DateOnly Date { get; set; }
        public List<T> Items { get; set; } = new();
        public int Minutes { get; set; }
        public int Kcal { get; set; }
    }

    // ---------- Workouts ----------
    public class WorkoutInput
    {
        /// <summary>Set when the client logs an exercise from a trainer's plan.</summary>
        public int? ItemId { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Enter the exercise.")]
        [StringLength(100)]
        [Display(Name = "Exercise")]
        public string ExerciseName { get; set; } = "";

        [Required(ErrorMessage = "Enter how long it lasted.")]
        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
        [Display(Name = "Duration (minutes)")]
        public int? DurationMin { get; set; }

        [Range(0, 5000, ErrorMessage = "Calories must be between 0 and 5000.")]
        [Display(Name = "Calories burned (optional)")]
        public int? CaloriesBurned { get; set; }
    }

    public class WorkoutRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int DurationMin { get; set; }
        public int? Calories { get; set; }
    }

    public class WorkoutsPageViewModel
    {
        public WorkoutInput Input { get; set; } = new();
        public List<DayGroup<WorkoutRow>> Days { get; set; } = new();
        public int WeekSessions { get; set; }
        public int WeekMinutes { get; set; }
        public int WeekKcal { get; set; }
    }

    // ---------- Meals ----------
    public class MealInput
    {
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Choose a meal type.")]
        [Display(Name = "Meal")]
        public string MealType { get; set; } = "Breakfast";

        [Required(ErrorMessage = "Describe what you ate.")]
        [StringLength(250)]
        [Display(Name = "What you ate")]
        public string FoodDescription { get; set; } = "";

        [Range(0, 5000, ErrorMessage = "Calories must be between 0 and 5000.")]
        [Display(Name = "Calories (optional)")]
        public int? Calories { get; set; }
    }

    public class MealRow
    {
        public int Id { get; set; }
        public string MealType { get; set; } = "";
        public string Food { get; set; } = "";
        public int? Calories { get; set; }
    }

    public class MealsPageViewModel
    {
        public MealInput Input { get; set; } = new();
        public List<DayGroup<MealRow>> Days { get; set; } = new();
        public int TodayKcal { get; set; }
        public int TodayMeals { get; set; }
    }

    // ---------- Progress ----------
    public class ProgressChartData
    {
        public List<string> WeightLabels { get; set; } = new();
        public List<decimal> Weights { get; set; } = new();
        public List<decimal?> Bmis { get; set; } = new();
        public List<string> DayLabels { get; set; } = new();
        public List<int> Burned { get; set; } = new();
        public List<int> Eaten { get; set; } = new();
    }

    public class ProgressViewModel
    {
        public ProgressChartData Chart { get; set; } = new();
        public int Entries { get; set; }
        public decimal? WeightChange { get; set; }
        public int BurnedTotal { get; set; }
        public int EatenTotal { get; set; }
        public bool HasTrend => Chart.Weights.Count >= 2;
    }

    // ---------- Profile ----------
    public class ProfileInput
    {
        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        public string? Gender { get; set; }

        [Range(50, 275, ErrorMessage = "Height must be between 50 and 275 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        [Phone, StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(2000)]
        [Display(Name = "Medical notes")]
        public string? MedicalNotes { get; set; }
    }
}
