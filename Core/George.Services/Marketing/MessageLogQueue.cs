using System.Threading.Channels;
using George.Data;
using George.DB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// Non-blocking queue for <see cref="MessageLog"/> rows - same shape as <see cref="IIntegrationLogQueue"/>.
/// SMS sends happen in the middle of order/payment flows that share one scoped DbContext; logging through a
/// queue keeps the log write out of their unit of work and can never fail or slow a send.
/// </summary>
public interface IMessageLogQueue
{
    bool TryEnqueue(MessageLog row);

    ChannelReader<MessageLog> Reader { get; }
}

public sealed class MessageLogQueue : IMessageLogQueue
{
    private const int Capacity = 10_000;

    private readonly Channel<MessageLog> _channel = Channel.CreateBounded<MessageLog>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly ILogger<MessageLogQueue> _logger;
    private long _enqueueFailures;

    public MessageLogQueue(ILogger<MessageLogQueue> logger)
    {
        _logger = logger;
    }

    public ChannelReader<MessageLog> Reader => _channel.Reader;

    public bool TryEnqueue(MessageLog row)
    {
        if (row == null) return false;
        if (_channel.Writer.TryWrite(row)) return true;

        var n = Interlocked.Increment(ref _enqueueFailures);
        if (n % 100 == 1)
            _logger.LogWarning("MessageLog queue saturated; dropped {Count} rows so far.", n);
        return false;
    }
}

/// <summary>Single background consumer of <see cref="IMessageLogQueue"/>; batch-inserts with a fresh DI scope.</summary>
public sealed class MessageLogBackgroundWriter : BackgroundService
{
    private const int BatchSize = 200;

    private readonly IMessageLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageLogBackgroundWriter> _logger;

    public MessageLogBackgroundWriter(IMessageLogQueue queue, IServiceScopeFactory scopeFactory, ILogger<MessageLogBackgroundWriter> logger)
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
                var batch = new List<MessageLog>(BatchSize) { first };
                while (batch.Count < BatchSize && _queue.Reader.TryRead(out var more))
                    batch.Add(more);
                await FlushAsync(batch).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down - fall through to a final drain.
        }

        var tail = new List<MessageLog>();
        while (_queue.Reader.TryRead(out var item))
            tail.Add(item);
        if (tail.Count > 0)
            await FlushAsync(tail).ConfigureAwait(false);
    }

    private async Task FlushAsync(IReadOnlyList<MessageLog> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<MarketingStorage>();
            await storage.AddMessageLogsAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MessageLog batch flush failed; {Count} rows lost.", batch.Count);
        }
    }
}
