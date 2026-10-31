using George.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Services.AiStudio;

/// <summary>
/// Drains the AiImageJob queue: claims jobs atomically and runs up to <c>AiStudio:MaxConcurrentJobs</c> at once, each in its own
/// DI scope. Same shape as <see cref="Marketing.MarketingDispatchHostedService"/>; a stuck-job sweep fails anything "running"
/// longer than the timeout (a crashed worker) so the UI stops waiting.
/// </summary>
public sealed class AiStudioJobHostedService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PresetRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AiStudioOptions _options;
    private readonly ILogger<AiStudioJobHostedService> _logger;

    public AiStudioJobHostedService(IServiceScopeFactory scopeFactory, IOptions<AiStudioOptions> options, ILogger<AiStudioJobHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
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

        var maxConcurrent = Math.Clamp(_options.MaxConcurrentJobs, 1, 10);
        using var slots = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        var lastSweep = DateTime.MinValue;
        var lastPresetRefresh = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var failed = false;
            var claimedSomething = false;
            try
            {
                if (DateTime.UtcNow - lastSweep > SweepInterval)
                {
                    lastSweep = DateTime.UtcNow;
                    using var sweepScope = _scopeFactory.CreateScope();
                    var storage = sweepScope.ServiceProvider.GetRequiredService<AiStudioStorage>();
                    // The super-admin's look presets (prompt / provider / price / active) overlay the code defaults. An admin save
                    // re-applies them in its own process at once; this keeps every other instance within a few minutes.
                    if (DateTime.UtcNow - lastPresetRefresh > PresetRefreshInterval)
                    {
                        lastPresetRefresh = DateTime.UtcNow;
                        AiStudioLooks.Apply(await storage.GetPresetsAsync(stoppingToken).ConfigureAwait(false));
                    }
                    var n = await storage.FailStuckJobsAsync(TimeSpan.FromSeconds(Math.Max(120, _options.JobTimeoutSeconds) * 2), "העיבוד נתקע ונעצר. נסו שוב.", stoppingToken).ConfigureAwait(false);
                    if (n > 0)
                        _logger.LogWarning("AiStudio: failed {Count} stuck job(s)", n);
                }

                // Claim while there are free slots and queued rows.
                while (slots.CurrentCount > 0 && !stoppingToken.IsCancellationRequested)
                {
                    long? jobId;
                    using (var claimScope = _scopeFactory.CreateScope())
                    {
                        var storage = claimScope.ServiceProvider.GetRequiredService<AiStudioStorage>();
                        jobId = await storage.ClaimNextQueuedAsync(stoppingToken).ConfigureAwait(false);
                    }
                    if (jobId == null)
                        break;

                    claimedSomething = true;
                    await slots.WaitAsync(stoppingToken).ConfigureAwait(false);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var runner = scope.ServiceProvider.GetRequiredService<AiStudioJobRunner>();
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(120, _options.JobTimeoutSeconds)));
                            await runner.RunAsync(jobId.Value, timeout.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("AiStudio job {JobId} timed out after {Seconds}s", jobId, _options.JobTimeoutSeconds);
                            await MarkTimedOutAsync(jobId.Value).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "AiStudio job {JobId} worker crashed", jobId);
                        }
                        finally
                        {
                            slots.Release();
                        }
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one failure kill the loop (e.g. the table is not installed yet on this DB).
                failed = true;
                _logger.LogError(ex, "AiStudio job loop tick failed; backing off.");
            }

            try
            {
                var idle = _options.PollMilliseconds is >= 100 and <= 60_000 ? TimeSpan.FromMilliseconds(_options.PollMilliseconds) : IdleInterval;
                await Task.Delay(failed ? FailureBackoff : claimedSomething ? TimeSpan.FromMilliseconds(250) : idle, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task MarkTimedOutAsync(long jobId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<AiStudioStorage>();
            var job = await storage.GetJobForUpdateAsync(jobId, CancellationToken.None).ConfigureAwait(false);
            if (job == null || job.Status is DB.AiImageJob.StatusDone or DB.AiImageJob.StatusFailed or DB.AiImageJob.StatusCanceled)
                return;
            job.Status = DB.AiImageJob.StatusFailed;
            job.Error = "העיבוד לקח יותר מדי זמן. נסו שוב.";
            job.FinishedAt = DateTime.UtcNow;
            await storage.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            var notifier = scope.ServiceProvider.GetRequiredService<IAiStudioRealtimeNotifier>();
            await notifier.JobUpdatedAsync(job.AccountId, AiStudioService.ToRes(job), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio: could not mark job {JobId} as timed out", jobId);
        }
    }
}
