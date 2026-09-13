namespace PulseWatch.Models
{
    public class Incident
    {
        public int Id { get; set; }

        public int WebsiteId { get; set; }

        public DateTime StartedAtUtc { get; set; }

        public DateTime? ResolvedAtUtc { get; set; }

        public string Reason { get; set; } = string.Empty;

        public bool IsResolved { get; set; }
    }
}