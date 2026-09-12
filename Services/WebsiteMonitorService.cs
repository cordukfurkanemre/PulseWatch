using System.Diagnostics;
using PulseWatch.Data;
using PulseWatch.Models;

namespace PulseWatch.Services
{
    public class WebsiteMonitorService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;

        public WebsiteMonitorService(HttpClient httpClient, AppDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        public async Task<HealthCheck?> CheckWebsiteAsync(int websiteId)
        {
            var website = await _context.Websites.FindAsync(websiteId);

            if (website == null)
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await _httpClient.GetAsync(website.Url);

                stopwatch.Stop();

                var healthCheck = new HealthCheck
                {
                    WebsiteId = website.Id,
                    StatusCode = (int)response.StatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    CheckedAtUtc = DateTime.UtcNow,
                    IsSuccessful = response.IsSuccessStatusCode
                };

                _context.HealthChecks.Add(healthCheck);
                await _context.SaveChangesAsync();

                return healthCheck;
            }
            catch
            {
                stopwatch.Stop();

                var healthCheck = new HealthCheck
                {
                    WebsiteId = website.Id,
                    StatusCode = 0,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    CheckedAtUtc = DateTime.UtcNow,
                    IsSuccessful = false
                };

                _context.HealthChecks.Add(healthCheck);
                await _context.SaveChangesAsync();

                return healthCheck;
            }
        }
    }
}