using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
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

                if (!response.IsSuccessStatusCode)
                {
                    await OpenIncidentIfNeeded(
                        website.Id,
                        $"HTTP {(int)response.StatusCode}"
                    );
                }
                else
                {
                    await ResolveIncidentIfNeeded(website.Id);
                }

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

                await OpenIncidentIfNeeded(
                    website.Id,
                    "Connection failed"
                );

                await _context.SaveChangesAsync();

                return healthCheck;
            }
        }

        private async Task OpenIncidentIfNeeded(int websiteId, string reason)
        {
            var openIncident = await _context.Incidents
                .FirstOrDefaultAsync(x =>
                    x.WebsiteId == websiteId &&
                    !x.IsResolved);

            if (openIncident != null)
            {
                return;
            }

            var incident = new Incident
            {
                WebsiteId = websiteId,
                StartedAtUtc = DateTime.UtcNow,
                Reason = reason,
                IsResolved = false
            };

            _context.Incidents.Add(incident);
        }

        private async Task ResolveIncidentIfNeeded(int websiteId)
        {
            var openIncident = await _context.Incidents
                .FirstOrDefaultAsync(x =>
                    x.WebsiteId == websiteId &&
                    !x.IsResolved);

            if (openIncident == null)
            {
                return;
            }

            openIncident.IsResolved = true;
            openIncident.ResolvedAtUtc = DateTime.UtcNow;
        }
    }
}