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
            HealthCheck healthCheck;
            string? incidentReason = null;

            try
            {
                using var response = await _httpClient.GetAsync(
                    website.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                stopwatch.Stop();

                healthCheck = new HealthCheck
                {
                    WebsiteId = website.Id,
                    StatusCode = (int)response.StatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    CheckedAtUtc = DateTime.UtcNow,
                    IsSuccessful = response.IsSuccessStatusCode
                };

                if (!response.IsSuccessStatusCode)
                {
                    incidentReason = $"HTTP {(int)response.StatusCode}";
                }
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

                healthCheck = new HealthCheck
                {
                    WebsiteId = website.Id,
                    StatusCode = 0,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    CheckedAtUtc = DateTime.UtcNow,
                    IsSuccessful = false
                };

                incidentReason = "Connection failed";
            }

            _context.HealthChecks.Add(healthCheck);

            if (incidentReason is not null)
            {
                await OpenIncidentIfNeeded(
                    website.Id,
                    incidentReason,
                    cancellationToken);
            }
            else
            {
                await ResolveIncidentIfNeeded(website.Id, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return healthCheck;
        }

        private async Task OpenIncidentIfNeeded(
            int websiteId,
            string reason,
            CancellationToken cancellationToken)
        {
            var openIncident = await _context.Incidents
                .FirstOrDefaultAsync(x =>
                    x.WebsiteId == websiteId &&
                    !x.IsResolved,
                    cancellationToken);

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

        private async Task ResolveIncidentIfNeeded(
            int websiteId,
            CancellationToken cancellationToken)
        {
            var openIncident = await _context.Incidents
                .FirstOrDefaultAsync(x =>
                    x.WebsiteId == websiteId &&
                    !x.IsResolved,
                    cancellationToken);

            if (openIncident == null)
            {
                return;
            }

            openIncident.IsResolved = true;
            openIncident.ResolvedAtUtc = DateTime.UtcNow;
        }
    }
}
