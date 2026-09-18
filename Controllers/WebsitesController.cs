using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseWatch.Data;
using PulseWatch.Models;
using PulseWatch.Services;

namespace PulseWatch.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsitesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WebsiteMonitorService _monitorService;

        public WebsitesController(
            AppDbContext context,
            WebsiteMonitorService monitorService)
        {
            _context = context;
            _monitorService = monitorService;
        }

        // 1. Tüm siteleri getir
        [HttpGet]
        public async Task<IActionResult> GetWebsites()
        {
            var websites = await _context.Websites.ToListAsync();

            return Ok(websites);
        }

        // 2. Tek bir siteyi getir
        [HttpGet("{id}")]
        public async Task<IActionResult> GetWebsite(int id)
        {
            var website = await _context.Websites.FindAsync(id);

            if (website == null)
            {
                return NotFound();
            }

            return Ok(website);
        }

        // 3. Yeni site oluştur
        [HttpPost]
        public async Task<IActionResult> CreateWebsite(Website website)
        {
            _context.Websites.Add(website);
            await _context.SaveChangesAsync();

            return Ok(website);
        }

        // 4. Siteyi güncelle
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWebsite(int id, Website website)
        {
            var existingWebsite = await _context.Websites.FindAsync(id);

            if (existingWebsite == null)
            {
                return NotFound();
            }

            existingWebsite.Name = website.Name;
            existingWebsite.Url = website.Url;
            existingWebsite.IpAddress = website.IpAddress;
            existingWebsite.IsActive = website.IsActive;

            await _context.SaveChangesAsync();

            return Ok(existingWebsite);
        }

        // 5. Siteyi sil

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWebsite(int id)
        {
            var website = await _context.Websites.FindAsync(id);

            if (website == null)
            {
                return NotFound();
            }

            _context.Websites.Remove(website);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/check")]
        public async Task<IActionResult> CheckWebsite(
            int id,
            CancellationToken cancellationToken)
        {
            var result = await _monitorService.CheckWebsiteAsync(
                id,
                cancellationToken);

            if (result == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                result.Id,
                result.WebsiteId,
                result.StatusCode,
                result.ResponseTimeMs,
                result.CheckedAtUtc,
                result.IsSuccessful
            });
        }


        [HttpGet("{id}/checks")]
        public async Task<IActionResult> GetHealthChecks(int id)
        {
            var websiteExists = await _context.Websites.AnyAsync(x => x.Id == id);

            if (!websiteExists)
            {
                return NotFound();
            }

            var checks = await _context.HealthChecks
                .Where(x => x.WebsiteId == id)
                .OrderByDescending(x => x.CheckedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.StatusCode,
                    x.ResponseTimeMs,
                    x.CheckedAtUtc,
                    x.IsSuccessful
                })
                .ToListAsync();

            return Ok(checks);
        }

        [HttpGet("{id}/uptime")]
        public async Task<IActionResult> GetUptime(int id)
        {
            var websiteExists = await _context.Websites.AnyAsync(x => x.Id == id);

            if (!websiteExists)
            {
                return NotFound();
            }

            var totalChecks = await _context.HealthChecks
                .CountAsync(x => x.WebsiteId == id);

            var successfulChecks = await _context.HealthChecks
                .CountAsync(x => x.WebsiteId == id && x.IsSuccessful);

            var uptimePercentage = totalChecks == 0
                ? 0
                : (double)successfulChecks / totalChecks * 100;

            return Ok(new
            {
                WebsiteId = id,
                TotalChecks = totalChecks,
                SuccessfulChecks = successfulChecks,
                UptimePercentage = Math.Round(uptimePercentage, 2)
            });
        }

        [HttpGet("{id}/incidents")]
        public async Task<IActionResult> GetIncidents(int id)
        {
            var websiteExists = await _context.Websites.AnyAsync(x => x.Id == id);

            if (!websiteExists)
            {
                return NotFound();
            }

            var incidents = await _context.Incidents
                .Where(x => x.WebsiteId == id)
                .OrderByDescending(x => x.StartedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.WebsiteId,
                    x.StartedAtUtc,
                    x.ResolvedAtUtc,
                    x.Reason,
                    x.IsResolved
                })
                .ToListAsync();

            return Ok(incidents);
        }


    }
}
