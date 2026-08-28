using System;
using System.Threading;
using System.Threading.Tasks;
using George.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Daily background job that flips <c>IsActive</c> off on promotions whose end-date has passed,
/// then fires <c>promotion.ended</c> through the webhook dispatcher so storefronts can drop
/// the row from their cache. Spec: <c>Sprint4/מבצעים.md</c> "סיום מבצע אוטומטי" - runs at
/// midnight UTC. We pad +5 minutes so the job starts cleanly inside the new day even if the
/// host clock drifts a bit.
/// </summary>
public class PromotionExpiryService : BackgroundService
{
    private readonly ILogger<PromotionExpiryService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PromotionExpiryService(ILogger<PromotionExpiryService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("**** PromotionExpiryService START at: {At} ****", DateTimeOffset.Now);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Brief warm-up: let the rest of the host (DB context, mappers, …) settle.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PromotionExpiryService run failed");
            }

            // Sleep until the next 00:05 UTC.
            var delay = NextRunUtc(DateTime.UtcNow) - DateTime.UtcNow;
            if (delay <= TimeSpan.Zero) delay = TimeSpan.FromMinutes(1); // defensive guard
            try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("**** PromotionExpiryService STOP at: {At} ****", DateTimeOffset.Now);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One pass: load expired-but-active promos, deactivate each, fire promotion.ended.</summary>
    private async Task RunOnceAsync(CancellationToken cancelToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var promotionStorage = scope.ServiceProvider.GetRequiredService<PromotionStorage>();
        var siteStorage = scope.ServiceProvider.GetRequiredService<SiteStorage>();
        var webhooks = scope.ServiceProvider.GetRequiredService<PromotionWebhookDispatcher>();

        var utcNow = DateTime.UtcNow;
        var expired = await promotionStorage.GetExpiredActivePromotionsAsync(utcNow, cancelToken).ConfigureAwait(false);
        if (expired.Count == 0)
        {
            _logger.LogInformation("PromotionExpiryService: nothing to expire at {Utc}", utcNow);
            return;
        }

        _logger.LogInformation("PromotionExpiryService: deactivating {Count} expired promotion(s)", expired.Count);
        foreach (var p in expired)
        {
            cancelToken.ThrowIfCancellationRequested();
            try
            {
                var deactivated = await promotionStorage
                    .UpdatePromotionAsync(p.Id, db => { db.IsActive = false; }, cancelToken)
                    .ConfigureAwait(false);
                if (deactivated == null) continue;

                var site = await siteStorage.GetSiteAsync(deactivated.SiteId, cancelToken).ConfigureAwait(false);
                await webhooks
                    .FireAsync(PromotionWebhookDispatcher.EventEnded, deactivated, site, cancelToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // One bad row shouldn't poison the whole batch.
                _logger.LogError(ex, "PromotionExpiryService: failed on promotion {PromotionId}", p.Id);
            }
        }
    }

    /// <summary>Next 00:05 UTC after <paramref name="now"/>.</summary>
    internal static DateTime NextRunUtc(DateTime now)
    {
        // Snap to today's 00:05 UTC. If we've already passed it, jump to tomorrow's.
        var todayBoundary = new DateTime(now.Year, now.Month, now.Day, 0, 5, 0, DateTimeKind.Utc);
        return now < todayBoundary ? todayBoundary : todayBoundary.AddDays(1);
    }
}
