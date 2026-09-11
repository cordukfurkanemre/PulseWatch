namespace PulseWatch.Models
{
    public class Website
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string? IpAddress { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedUtc { get; set; }

        public List<HealthCheck> HealthChecks { get; set; } = new();
    }
}
