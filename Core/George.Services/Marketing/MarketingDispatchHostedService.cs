using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// Drives <see cref="MarketingDispatchService"/>: materializes due sends, sends queued deliveries in batches,
/// attributes orders. "Send now" is just a send whose time has come, so the idle interval is short.
/// Same scoped-per-tick shape as <see cref="FutureOrdersAtTimePrintHostedService"/>.
/// </summary>
public sealed class MarketingDispatchHostedService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BusyInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketingDispatchHostedService> _logger;

    public MarketingDispatchHostedService(IServiceScopeFactory scopeFactory, ILogger<MarketingDispatchHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var moreWork = false;
            var failed = false;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<MarketingDispatchService>();
                moreWork = await dispatcher.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one failure kill the loop (e.g. the tables are not installed yet on this DB).
                failed = true;
                _logger.LogError(ex, "Marketing dispatcher tick failed; backing off.");
            }

            try
            {
                await Task.Delay(failed ? FailureBackoff : moreWork ? BusyInterval : IdleInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
