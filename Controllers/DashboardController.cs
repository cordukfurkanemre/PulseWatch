using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseWatch.Data;

namespace PulseWatch.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var siteStats = await _context.Websites
            .AsNoTracking()
            .Select(website => new
            {
                LatestStatus = website.HealthChecks
                    .OrderByDescending(check => check.CheckedAtUtc)
                    .Select(check => (bool?)check.IsSuccessful)
                    .FirstOrDefault(),
                TotalChecks = website.HealthChecks.Count,
                SuccessfulChecks = website.HealthChecks.Count(check => check.IsSuccessful)
            })
            .ToListAsync(cancellationToken);

        var totalSites = siteStats.Count;
        var onlineSites = siteStats.Count(site => site.LatestStatus == true);
        var offlineSites = totalSites - onlineSites;

        var checkedSites = siteStats
            .Where(site => site.TotalChecks > 0)
            .ToList();

        var averageUptime = checkedSites.Count == 0
            ? 0
            : Math.Round(
                checkedSites.Average(site =>
                    (double)site.SuccessfulChecks / site.TotalChecks * 100),
                2);

        return Ok(new
        {
            totalSites,
            onlineSites,
            offlineSites,
            averageUptime
        });
    }

    [HttpGet("sites")]
    public async Task<IActionResult> GetSites(CancellationToken cancellationToken)
    {
        var sites = await _context.Websites
            .AsNoTracking()
            .Select(website => new
            {
                website.Id,
                website.Name,
                website.Url,

                LatestCheck = website.HealthChecks
                    .OrderByDescending(check => check.CheckedAtUtc)
                    .Select(check => new
                    {
                        check.IsSuccessful,
                        check.StatusCode,
                        check.ResponseTimeMs,
                        check.CheckedAtUtc
                    })
                    .FirstOrDefault(),

                TotalChecks = website.HealthChecks.Count,

                SuccessfulChecks = website.HealthChecks
                    .Count(check => check.IsSuccessful)
            })
            .ToListAsync(cancellationToken);

        var result = sites.Select(site => new
        {
            site.Id,
            site.Name,
            site.Url,

            IsOnline = site.LatestCheck?.IsSuccessful ?? false,

            StatusCode = site.LatestCheck?.StatusCode,

            ResponseTimeMs = site.LatestCheck?.ResponseTimeMs,

            LastCheckedAtUtc = site.LatestCheck?.CheckedAtUtc,

            Uptime = site.TotalChecks == 0
                ? 0
                : Math.Round(
                    (double)site.SuccessfulChecks / site.TotalChecks * 100,
                    2)
        });

        return Ok(result);
    }

    [HttpGet("sites/{id:int}/response-times")]
    public async Task<IActionResult> GetResponseTimes(
        int id,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var websiteExists = await _context.Websites
            .AsNoTracking()
            .AnyAsync(website => website.Id == id, cancellationToken);

        if (!websiteExists)
        {
            return NotFound();
        }

        var boundedLimit = Math.Clamp(limit, 1, 100);

        var responseTimes = await _context.HealthChecks
            .AsNoTracking()
            .Where(check => check.WebsiteId == id)
            .OrderByDescending(check => check.CheckedAtUtc)
            .Take(boundedLimit)
            .Select(check => new
            {
                check.ResponseTimeMs,
                check.CheckedAtUtc
            })
            .ToListAsync(cancellationToken);

        responseTimes.Reverse();

        return Ok(responseTimes);
    }
}
