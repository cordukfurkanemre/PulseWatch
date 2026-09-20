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
}
