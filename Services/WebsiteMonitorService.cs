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
        private readonly ILogger<WebsiteMonitorService> _logger;

        public WebsiteMonitorService(
            HttpClient httpClient,
            AppDbContext context,
            ILogger<WebsiteMonitorService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheck?> CheckWebsiteAsync(
            int websiteId,
            CancellationToken cancellationToken = default)
        {
            var website = await _context.Websites.FindAsync([websiteId], cancellationToken);

            if (website == null)
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var response = await _httpClient.GetAsync(
                    website.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

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

                await _context.SaveChangesAsync(cancellationToken);

                return healthCheck;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();

                _logger.LogWarning(
                    exception,
                    "{Website} kontrol edilirken bağlantı hatası oluştu.",
                    website.Name);

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

                await _context.SaveChangesAsync(cancellationToken);

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
