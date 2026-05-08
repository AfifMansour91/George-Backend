using George.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Periodically clears <see cref="George.DB.Product.LabelNew"/> / <see cref="George.DB.Product.LabelKosherForPassover"/>
/// when their end dates have passed (DB-only; Woo sync happens on next product save or manual sync).
/// </summary>
public sealed class ExpiredTimedProductLabelsHostedService : BackgroundService
{
    /// <summary>How often to run cleanup after the host starts.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Delay before first run so startup/migrations are not contended.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private readonly ILogger<ExpiredTimedProductLabelsHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExpiredTimedProductLabelsHostedService(
        ILogger<ExpiredTimedProductLabelsHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Service} scheduled: first run after {StartupDelay}, then every {Interval}",
            nameof(ExpiredTimedProductLabelsHostedService),
            StartupDelay,
            Interval);

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
                var storage = scope.ServiceProvider.GetRequiredService<ProductStorage>();
                var (passover, newLabel) = await storage.ClearExpiredTimedProductLabelsAsync(stoppingToken).ConfigureAwait(false);
                if (passover > 0 || newLabel > 0)
                {
                    _logger.LogInformation(
                        "Expired timed product labels cleared: kosherForPassoverRows={Passover}, newLabelRows={NewLabel}",
                        passover,
                        newLabel);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired timed product labels cleanup failed");
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
