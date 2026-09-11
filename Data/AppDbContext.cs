using Microsoft.EntityFrameworkCore;
using PulseWatch.Models;

namespace PulseWatch.Data
{  
        public class AppDbContext : DbContext
        {

            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }
            public DbSet<Website> Websites { get; set; }
            public DbSet<HealthCheck> HealthChecks { get; set; }

        }

}
