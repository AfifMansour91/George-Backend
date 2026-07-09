using George.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Daily job for sites with <see cref="George.DB.Site.ExternalPriceManagement"/>: the POS writes prices
/// directly to those WooCommerce stores (George's outbound sync skips price fields there), so this job pulls
/// the stores' current product/variation prices back into George once a day. Can also be triggered on demand
/// via POST WooCommerce/PullPricesFromWooCommerce.
/// </summary>
public sealed class ExternalPriceSyncHostedService : BackgroundService
{
    /// <summary>Local server time of the daily run (early morning: store traffic and POS activity are lowest).</summary>
    private static readonly TimeSpan RunAtLocalTime = new(3, 30, 0);

    /// <summary>Delay before scheduling starts so startup/migrations are not contended.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(3);

    private readonly ILogger<ExternalPriceSyncHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExternalPriceSyncHostedService(
        ILogger<ExternalPriceSyncHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Service} scheduled: daily at {RunAt} (local), first schedule after {StartupDelay}",
            nameof(ExternalPriceSyncHostedService),
            RunAtLocalTime,
            StartupDelay);

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
            var now = DateTime.Now;
            var nextRun = now.Date.Add(RunAtLocalTime);
            if (nextRun <= now)
                nextRun = nextRun.AddDays(1);

            try
            {
                await Task.Delay(nextRun - now, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External price pull run failed");
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancelToken)
    {
        List<int> siteIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var siteStorage = scope.ServiceProvider.GetRequiredService<SiteStorage>();
            siteIds = await siteStorage.GetExternalPriceManagedSiteIdsAsync(cancelToken).ConfigureAwait(false);
        }

        if (siteIds.Count == 0)
        {
            _logger.LogDebug("External price pull: no sites with external price management enabled");
            return;
        }

        _logger.LogInformation("External price pull run starting for {Count} site(s): {SiteIds}", siteIds.Count, string.Join(",", siteIds));

        foreach (var siteId in siteIds)
        {
            cancelToken.ThrowIfCancellationRequested();
            try
            {
                // Fresh scope per site: each pull gets its own DbContext (long runs must not share one context).
                using var scope = _scopeFactory.CreateScope();
                var wooService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                var result = await wooService.PullPricesFromWooCommerceAsync(siteId, cancelToken).ConfigureAwait(false);
                if (result.IsSuccessful)
                    _logger.LogInformation("External price pull for site {SiteId}: {Message}", siteId, result.Data?.Message);
                else
                    _logger.LogWarning("External price pull for site {SiteId} failed: {Error}", siteId, result.DisplayMessage ?? result.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External price pull for site {SiteId} threw", siteId);
            }
        }
    }
}
