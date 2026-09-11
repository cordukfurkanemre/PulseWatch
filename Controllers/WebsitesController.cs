using PulseWatch.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        public async Task<IActionResult> GetWebsites()
        {
            var websites = await _context.Websites.ToListAsync();

            return Ok(websites);
        }

        [HttpPost]
        public async Task<IActionResult> CreateWebsite(Website website) { 
            
           _context.Websites.Add(website);
            await _context.SaveChangesAsync();

            return Ok(website);

        }
    }
}
