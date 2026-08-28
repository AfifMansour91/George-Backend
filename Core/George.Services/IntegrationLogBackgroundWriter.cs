using George.Data;
using George.DB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Single background consumer of <see cref="IIntegrationLogQueue"/>: drains queued rows and writes them in
/// batches (one DB round-trip per batch) using a fresh DI scope. Decouples log writes from the operations
/// that produce them, so high-volume sync (e.g. bulk product/category) doesn't spawn a Task.Run per row.
/// </summary>
public sealed class IntegrationLogBackgroundWriter : BackgroundService
{
    private const int BatchSize = 200;

    private readonly IIntegrationLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntegrationLogBackgroundWriter> _logger;

    public IntegrationLogBackgroundWriter(
        IIntegrationLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<IntegrationLogBackgroundWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var first in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                var batch = new List<IntegrationLog>(BatchSize) { first };
                while (batch.Count < BatchSize && _queue.Reader.TryRead(out var more))
                    batch.Add(more);
                await FlushAsync(batch).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down - fall through to a final drain.
        }

        // Best-effort drain of whatever is left so in-flight logs aren't lost on shutdown.
        var tail = new List<IntegrationLog>();
        while (_queue.Reader.TryRead(out var item))
            tail.Add(item);
        if (tail.Count > 0)
            await FlushAsync(tail).ConfigureAwait(false);
    }

    private async Task FlushAsync(IReadOnlyList<IntegrationLog> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IntegrationLogStorage>();
            await storage.AddRangeAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IntegrationLog batch flush failed; {Count} log rows lost.", batch.Count);
        }
    }
}
