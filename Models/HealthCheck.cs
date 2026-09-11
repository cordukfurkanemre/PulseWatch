namespace PulseWatch.Models
{
    public class HealthCheck
    {
        public int Id { get; set; }

        public int WebsiteId { get; set; }

        public Website Website { get; set; } = null!;

        public int StatusCode { get; set; }

        public long ResponseTimeMs { get; set; }

        public DateTime CheckedAtUtc { get; set; }

        public bool IsSuccessful { get; set; }
    }
}
