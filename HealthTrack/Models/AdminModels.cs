namespace HealthTrack.Models
{
    public class TrainerOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Specialization { get; set; }
        public int ClientCount { get; set; }
    }

    public class AssignmentRow
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = "";
        public string? Phone { get; set; }
        public int? TrainerId { get; set; }
        public string? TrainerName { get; set; }
        public DateOnly? Since { get; set; }
    }

    public class AssignmentsPageViewModel
    {
        public List<AssignmentRow> Clients { get; set; } = new();
        public List<TrainerOption> Trainers { get; set; } = new();
        public bool UnassignedOnly { get; set; }
        public int UnassignedCount { get; set; }
    }
}
