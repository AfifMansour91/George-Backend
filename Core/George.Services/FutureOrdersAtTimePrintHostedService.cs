using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Server-side "print future orders at HH:mm" (Site.PrintFutureAtTimeEnabled / PrintFutureAtTime).
/// Runs every minute and asks <see cref="OrderService.EnqueueFutureOrdersAtTimePrintsAsync"/> to enqueue
/// vouchers for today's pre-scheduled orders on every site whose print time has passed. The enqueue is
/// idempotent per (site, order, job type), so re-running each minute - and a kanban page doing the same
/// thing in the browser - never prints an order twice. Before this the feature lived only in the browser
/// and fired only when the Orders page happened to be open at that minute (Hinnawi, 2026-09-09).
/// </summary>
public sealed class FutureOrdersAtTimePrintHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly ILogger<FutureOrdersAtTimePrintHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public FutureOrdersAtTimePrintHostedService(
        ILogger<FutureOrdersAtTimePrintHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Service} scheduled: first run after {StartupDelay}, then every {Interval}",
            nameof(FutureOrdersAtTimePrintHostedService), StartupDelay, Interval);

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
                var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
                var enqueued = await orderService.EnqueueFutureOrdersAtTimePrintsAsync(stoppingToken).ConfigureAwait(false);
                if (enqueued > 0)
                    _logger.LogInformation("FutureAtTime print: enqueued {Count} voucher job(s)", enqueued);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FutureAtTime print run failed");
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
