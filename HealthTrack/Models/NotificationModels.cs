namespace HealthTrack.Models
{
    public class NotificationItem
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public string Type { get; set; } = "System";
        public DateTime When { get; set; }
        public bool IsRead { get; set; }

        public string Icon => Type switch
        {
            "System" when Message.StartsWith("Feedback", StringComparison.OrdinalIgnoreCase) => "bi-chat-left-heart",
            "Workout" => "bi-lightning-charge",
            "Meal" => "bi-egg-fried",
            "Appointment" => "bi-calendar-check",
            _ => "bi-bell"
        };

        public string Ago
        {
            get
            {
                var span = DateTime.Now - When;
                if (span.TotalMinutes < 1) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays} d ago";
                return When.ToString("dd MMM yyyy");
            }
        }
    }

    public class NotificationRailModel
    {
        public List<NotificationItem> Items { get; set; } = new();
        public int Unread { get; set; }
    }

    public class NotificationsPageViewModel
    {
        public List<NotificationItem> Unread { get; set; } = new();
        public List<NotificationItem> Earlier { get; set; } = new();
        public List<NotificationItem> Upcoming { get; set; } = new();
        public ReminderInput Input { get; set; } = new();
    }
}
