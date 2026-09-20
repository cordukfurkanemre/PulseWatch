using Microsoft.EntityFrameworkCore;

namespace PulseWatch.Services;

public class WebsiteMonitoringWorker : BackgroundService
{
    private static readonly TimeSpan MonitoringInterval = TimeSpan.FromSeconds(30);

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
            try
            {
                await using var queryScope = _scopeFactory.CreateAsyncScope();
                var context = queryScope.ServiceProvider
                    .GetRequiredService<PulseWatch.Data.AppDbContext>();

                var websites = await context.Websites
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .Select(x => new { x.Id, x.Name })
                    .ToListAsync(stoppingToken);

                foreach (var website in websites)
                {
                    try
                    {
                        await using var monitorScope = _scopeFactory.CreateAsyncScope();
                        var monitorService = monitorScope.ServiceProvider
                            .GetRequiredService<WebsiteMonitorService>();

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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Monitoring döngüsü veritabanına erişemedi. Bir sonraki döngüde tekrar denenecek.");
            }

            try
            {
                await Task.Delay(MonitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
