using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseWatch.Data;
using PulseWatch.Models;

namespace PulseWatch.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsitesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebsitesController(AppDbContext context)
        {
            _context = context;
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


    }
}