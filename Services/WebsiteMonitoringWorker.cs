using Microsoft.EntityFrameworkCore;

namespace PulseWatch.Services;

public class WebsiteMonitoringWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebsiteMonitoringWorker> _logger;

    public WebsiteMonitoringWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<WebsiteMonitoringWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PulseWatch monitoring worker başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();

            var monitorService =
                scope.ServiceProvider.GetRequiredService<WebsiteMonitorService>();

            var websiteService =
                scope.ServiceProvider.GetRequiredService<PulseWatch.Data.AppDbContext>();

            var websites = await websiteService.Websites
                .Where(x => x.IsActive)
                .ToListAsync(stoppingToken); ;

            foreach (var website in websites)
            {
                try
                {
                    await monitorService.CheckWebsiteAsync(
                        website.Id,
                        stoppingToken);

                    _logger.LogInformation(
                        "{Website} kontrol edildi.",
                        website.Name);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "{Website} kontrolü beklenmeyen bir hatayla tamamlanamadı.",
                        website.Name);
                }
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
