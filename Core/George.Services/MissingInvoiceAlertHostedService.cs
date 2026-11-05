using George.Services.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Runs <see cref="MissingInvoiceAlertService"/> every few minutes (PEPE, 4.11.2026): charged card orders with no
/// invoice document raise one in-app + SMS alert per order. Same shape as <see cref="FutureOrdersAtTimePrintHostedService"/>.
/// </summary>
public sealed class MissingInvoiceAlertHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private readonly ILogger<MissingInvoiceAlertHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public MissingInvoiceAlertHostedService(ILogger<MissingInvoiceAlertHostedService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Service} scheduled: first run after {StartupDelay}, then every {Interval}",
            nameof(MissingInvoiceAlertHostedService), StartupDelay, Interval);
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<MissingInvoiceAlertService>();
                var alerted = await service.RunAsync(stoppingToken).ConfigureAwait(false);
                if (alerted > 0)
                    _logger.LogInformation("Missing-invoice alert: {Count} order(s) alerted", alerted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Missing-invoice alert run failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
